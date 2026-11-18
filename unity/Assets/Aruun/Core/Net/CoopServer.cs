using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Aruun.Core.Combat;
using Aruun.Core.Play;
using Aruun.Core.Survival;

namespace Aruun.Core.Net
{
    /// <summary>
    /// Hosting co-op: the player who hosts runs the real world (the Host); friends connect over
    /// TCP. Each friend keeps their own character, sent when they join and sent back as it changes.
    /// Their actions arrive as small commands and run on the Host exactly as the host's own do;
    /// the world comes back to them as a stream: edits, creatures, people, other players, and
    /// what concerns them alone (messages, their bag and quests, the conversation they are in).
    ///
    /// Threads: one reader per friend queues frames; everything else (Pump, AfterTick) runs on
    /// the game's own thread, between and after the Host's ticks.
    /// </summary>
    public sealed class CoopServer : IDisposable
    {
        public const int DefaultPort = 27461;
        public const int MaxPlayers = 4;
        /// <summary>World state goes out this often, per second.</summary>
        public const double SendRate = 20;
        /// <summary>The first id given to a friend; the hosting player is 1.</summary>
        public const int FirstGuestId = 2;

        sealed class Guest
        {
            public int Id;
            public string Name = "";
            public TcpClient Tcp;
            public System.IO.Stream Stream;
            public bool Welcomed, Dead;
            public readonly object WriteLock = new object();
            public readonly StringBuilder Pending = new StringBuilder();
            public string LastChar = "", LastTalk = "", LastFlags = "", LastPlants = "", LastStructs = "";
            public double Dig;
        }

        readonly Host _host;
        readonly int _seed;
        readonly List<string> _journal = new List<string>();
        readonly ConcurrentQueue<KeyValuePair<Guest, Frame>> _inbox = new ConcurrentQueue<KeyValuePair<Guest, Frame>>();
        readonly List<Guest> _guests = new List<Guest>();
        readonly object _guestLock = new object();
        TcpListener _listener;
        Thread _accept;
        volatile bool _stopping;
        double _sendAcc;
        string _lastWorldFlags = "";

        /// <summary>Name shown over the hosting player's head.</summary>
        public string HostName = "Host";
        /// <summary>The port actually listened on (useful with port 0 in tests).</summary>
        public int Port { get; private set; }
        /// <summary>Lines for the log ("Ana joined").</summary>
        public readonly ConcurrentQueue<string> Log = new ConcurrentQueue<string>();

        /// <param name="savedEdits">The world's edit journal so far (EditOp lines), so friends see the same world.</param>
        public CoopServer(Host host, int seed, IEnumerable<string> savedEdits)
        {
            _host = host;
            _seed = seed;
            if (savedEdits != null) _journal.AddRange(savedEdits.Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        public int GuestCount { get { lock (_guestLock) return _guests.Count(g => g.Welcomed && !g.Dead); } }

        public void Start(int port = DefaultPort)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _accept = new Thread(AcceptLoop) { IsBackground = true, Name = "co-op accept" };
            _accept.Start();
        }

        void AcceptLoop()
        {
            while (!_stopping)
            {
                TcpClient tcp;
                try { tcp = _listener.AcceptTcpClient(); }
                catch { return; }
                tcp.NoDelay = true;
                var g = new Guest { Tcp = tcp, Stream = tcp.GetStream() };
                lock (_guestLock) _guests.Add(g);
                new Thread(() => ReadLoop(g)) { IsBackground = true, Name = "co-op reader" }.Start();
            }
        }

        void ReadLoop(Guest g)
        {
            try
            {
                while (!_stopping)
                {
                    Frame f = Frames.Read(g.Stream);
                    if (f == null) break;
                    _inbox.Enqueue(new KeyValuePair<Guest, Frame>(g, f));
                }
            }
            catch { }
            _inbox.Enqueue(new KeyValuePair<Guest, Frame>(g, new Frame("bye", "")));
        }

        // ------------------------------------------------------------------ commands in

        /// <summary>Runs every command that came in since the last call. Call before Host.Tick.</summary>
        public void Pump(double dt)
        {
            KeyValuePair<Guest, Frame> item;
            while (_inbox.TryDequeue(out item))
            {
                Guest g = item.Key;
                Frame f = item.Value;
                try { Handle(g, f, dt); }
                catch (Exception e) { Log.Enqueue("co-op: bad '" + f.Kind + "' from " + g.Name + ": " + e.Message); }
            }
        }

        void Handle(Guest g, Frame f, double dt)
        {
            if (f.Kind == "bye") { Drop(g, "left"); return; }
            if (g.Dead) return;
            if (f.Kind == "hello") { Hello(g, f); return; }
            if (!g.Welcomed) return;
            string[] a = f.Body.Length > 0 ? Wire.Split(f.Body) : new string[0];
            int id = g.Id;
            switch (f.Kind)
            {
                case "pose":
                    {
                        PlayerRec p;
                        if (_host.Players.TryGetValue(id, out p) && a.Length > 7) p.C.Selected = Wire.I(a[7]);
                        _host.SetPose(id, Wire.V(a, 0), Wire.V(a, 3), Wire.D(a[6]));
                        break;
                    }
                case "dig":
                    {
                        DigTick t = _host.Dig(id, Wire.I(a[0]), Wire.I(a[1]), Wire.I(a[2]), Wire.D(a[3]));
                        g.Dig = t.Fraction;
                        if (!string.IsNullOrEmpty(t.Message)) Queue(g, "M", Wire.Clean(t.Message));
                        break;
                    }
                case "stopdig": _host.StopDigging(id); g.Dig = 0; break;
                case "use": _host.UseHeld(id, a[0] == "1", Wire.I(a[1]), Wire.I(a[2]), Wire.I(a[3]), Wire.I(a[4]), Wire.I(a[5]), Wire.I(a[6])); break;
                case "interact": _host.Interact(id, Wire.V(a, 0), Wire.V(a, 3)); break;
                case "craft":
                    {
                        string err = _host.Craft(id, f.Body);
                        if (!string.IsNullOrEmpty(err)) Queue(g, "M", Wire.Clean(err));
                        break;
                    }
                case "attack": _host.Attack(id, a[0] == "1"); break;
                case "guard": _host.SetGuard(id, a[0] == "1"); break;
                case "dodge": _host.Dodge(id); break;
                case "fall": _host.ReportFall(id, Wire.D(a[0])); break;
                case "choose": _host.Choose(id, Wire.I(a[0])); break;
                case "endtalk": _host.EndTalk(id); break;
                case "skill": _host.LearnSkill(id, a[0]); break;
                case "speak": _host.Speak(id, a.Skip(6).ToList(), Wire.V(a, 0), Wire.V(a, 3)); break;
            }
        }

        void Hello(Guest g, Frame f)
        {
            if (g.Welcomed) return;
            string[] lines = f.Lines;
            string[] head = Wire.Split(lines.Length > 0 ? lines[0] : "");
            int version = head.Length > 0 ? Wire.I(head[0]) : 0;
            g.Name = head.Length > 1 ? head[1] : "Friend";
            string charText = head.Length > 2 ? Wire.Unpack(head[2]) : "";
            if (version != Protocol.Version) { Refuse(g, "This host runs a different version of Aruun."); return; }
            int used = 0;
            lock (_guestLock) used = _guests.Count(o => o.Welcomed && !o.Dead);
            if (used + 1 >= MaxPlayers) { Refuse(g, "This world is full (" + MaxPlayers + " players)."); return; }
            int id = FirstGuestId;
            while (_host.Players.ContainsKey(id)) id++;
            g.Id = id;
            var c = new CharacterState(_host.Items, _host.Rules);
            bool fresh = charText.Trim().Length == 0;
            if (!fresh) c.FromText(charText);
            // Arrive a few steps in front of the host, facing them, so friends see each other at once.
            PlayerRec hostP;
            Vec3 at, look;
            if (_host.Players.TryGetValue(1, out hostP) && hostP.Alive && (_host.Village == null || !_host.Village.InLibrary(hostP.Pos)))
            {
                Vec3 fwd = hostP.Facing.Flat.Length > 0.01 ? hostP.Facing.Flat.Normalized : Vec3.Forward;
                Vec3 side = new Vec3(fwd.Z, 0, -fwd.X);
                at = hostP.Pos + fwd * 3.5 + side * ((id - FirstGuestId) * 1.5);
                look = hostP.Pos;
            }
            else
            {
                at = _host.SpawnPoint + new Vec3((id - 1) * 1.5, 0, 0);
                look = at + Vec3.Forward;
            }
            at = new Vec3(at.X, _host.GroundHeight(at.X, at.Z), at.Z);
            PlayerRec joined = _host.Join(id, c, at, fresh);
            joined.ArriveYaw = Math.Atan2(look.X - at.X, look.Z - at.Z);
            if (!fresh && c.Dead) { c.Dead = false; c.Hp = Math.Max(c.Hp, c.HpMax * 0.5); }

            var sb = new StringBuilder();
            sb.Append(Wire.Join(id, _seed, _host.Time, at)).Append('\n');
            sb.Append("F\t").Append(string.Join(",", _host.WorldFlags)).Append('\n');
            foreach (string line in _journal) sb.Append("J\t").Append(line).Append('\n');
            Send(g, "welcome", sb.ToString().TrimEnd('\n'));
            g.Welcomed = true;
            g.LastChar = "";
            Log.Enqueue(g.Name + " joined the world.");
            Announce(g.Name + " joined the world.");
        }

        void Refuse(Guest g, string why)
        {
            Send(g, "refused", why);
            Drop(g, "refused");
        }

        void Drop(Guest g, string why)
        {
            if (g.Dead) return;
            g.Dead = true;
            try { g.Tcp.Close(); } catch { }
            lock (_guestLock) _guests.Remove(g);
            if (g.Welcomed)
            {
                _host.Leave(g.Id);
                Log.Enqueue(g.Name + " " + why + ".");
                Announce(g.Name + " left the world.");
            }
        }

        /// <summary>A line every player sees (the host's own through the Host's messages).</summary>
        void Announce(string text)
        {
            _host.Messages.Add(new KeyValuePair<int, string>(1, text));
        }

        // ------------------------------------------------------------------ world out

        /// <summary>
        /// Collects what the Host did this tick and, at the send rate, streams it to each friend.
        /// Call after Host.Tick and before the game drains the Host's edits, events and messages.
        /// </summary>
        public void AfterTick(double dt)
        {
            List<Guest> guests;
            lock (_guestLock) guests = _guests.Where(g => g.Welcomed && !g.Dead).ToList();

            // Edits go into the journal (for friends who join later) and to everyone.
            foreach (var e in _host.Edits)
            {
                string line = e.Op.ToLine();
                _journal.Add(line);
                foreach (var g in guests) Queue(g, "E", line);
            }
            foreach (var m in _host.Messages)
                foreach (var g in guests)
                    if (m.Key == g.Id) Queue(g, "M", Wire.Clean(m.Value));
            foreach (var v in _host.Events)
                foreach (var g in guests)
                    Queue(g, "V", Wire.Join(v.Kind, v.Player, v.Creature, v.Value, v.At));

            _sendAcc += dt;
            if (_sendAcc < 1.0 / SendRate || guests.Count == 0) return;
            _sendAcc = 0;

            string shared = SharedState();
            string flags = string.Join(",", _host.WorldFlags.OrderBy(x => x));
            bool flagsChanged = flags != _lastWorldFlags;
            _lastWorldFlags = flags;
            string plants = PlantsState(), structs = StructuresState();
            foreach (var g in guests)
            {
                PlayerRec p;
                if (!_host.Players.TryGetValue(g.Id, out p)) continue;
                var sb = new StringBuilder(shared);
                if (flagsChanged) sb.Append("F\t").Append(flags).Append('\n');
                if (plants != g.LastPlants) { g.LastPlants = plants; sb.Append(plants); }
                if (structs != g.LastStructs) { g.LastStructs = structs; sb.Append(structs); }
                sb.Append("D\t").Append(Wire.N(g.Dig)).Append('\n');
                string pf = string.Join(",", p.Flags.OrderBy(x => x));
                if (pf != g.LastFlags) { g.LastFlags = pf; sb.Append("G\t").Append(pf).Append('\n'); }
                string ch = p.C.ToText();
                if (ch != g.LastChar) { g.LastChar = ch; sb.Append("H\t").Append(Wire.Pack(ch)).Append('\n'); }
                string talk = TalkLine(p.Talk);
                if (talk != g.LastTalk) { g.LastTalk = talk; sb.Append(talk).Append('\n'); }
                if (p.RespawnAt.HasValue) { sb.Append("R\t").Append(Wire.V(p.RespawnAt.Value)).Append('\n'); p.RespawnAt = null; }
                if (p.ArriveYaw.HasValue) { sb.Append("Y\t").Append(Wire.N(p.ArriveYaw.Value)).Append('\n'); p.ArriveYaw = null; }
                if (p.Shove.HasValue) { sb.Append("S\t").Append(Wire.V(p.Shove.Value)).Append('\n'); p.Shove = null; }
                sb.Append(g.Pending);
                g.Pending.Clear();
                Send(g, "state", sb.ToString().TrimEnd('\n'));
            }
        }

        string SharedState()
        {
            var sb = new StringBuilder();
            sb.Append("T\t").Append(Wire.N(_host.Time)).Append('\n');
            foreach (var p in _host.Players.Values)
                sb.Append("P\t").Append(Wire.Join(p.Id, p.Pos, p.Facing, p.Guard, p.Alive, NameOf(p.Id))).Append('\n');
            foreach (var c in _host.Creatures)
                sb.Append("C\t").Append(Wire.Join(c.Id, c.Def.Key, c.Pos, c.Vel, c.Yaw, c.State, c.Hp, c.HpMax, (int)c.AnimForce, c.Grounded)).Append('\n');
            foreach (var r in _host.People)
                sb.Append("N\t").Append(Wire.Join(r.Id, r.Pos, r.Yaw, r.TalkingTo, r.Walking)).Append('\n');
            return sb.ToString();
        }

        string PlantsState()
        {
            var sb = new StringBuilder("L-\n");
            foreach (var r in _host.PlantsInWorld)
                sb.Append("L\t").Append(Wire.Join(r.Id, r.Plant, r.X, r.Y, r.Z, r.PlantedAt, r.Mult, r.WildId)).Append('\n');
            return sb.ToString();
        }

        string StructuresState()
        {
            var sb = new StringBuilder("U-\n");
            foreach (var s in _host.Structures)
                sb.Append("U\t").Append(Wire.Join(s.Id, s.Key, s.Pos, s.Owner)).Append('\n');
            return sb.ToString();
        }

        static string TalkLine(Conversation c)
        {
            if (c == null) return "K";
            var parts = new List<object> { c.Person, c.Name, c.Say };
            parts.AddRange(c.Choices);
            return "K\t" + Wire.Join(parts.ToArray());
        }

        public string NameOf(int id)
        {
            if (id == 1) return HostName;
            lock (_guestLock) foreach (var g in _guests) if (g.Id == id) return g.Name;
            return "";
        }

        void Queue(Guest g, string tag, string line) { g.Pending.Append(tag).Append('\t').Append(line).Append('\n'); }

        void Send(Guest g, string kind, string body)
        {
            try { lock (g.WriteLock) Frames.Write(g.Stream, kind, body); }
            catch { _inbox.Enqueue(new KeyValuePair<Guest, Frame>(g, new Frame("bye", ""))); }
        }

        public void Dispose()
        {
            _stopping = true;
            try { _listener?.Stop(); } catch { }
            lock (_guestLock) foreach (var g in _guests) { try { g.Tcp.Close(); } catch { } }
        }
    }

    public static class Protocol
    {
        /// <summary>Bumped whenever the wire format changes, so mismatched builds refuse each other.</summary>
        public const int Version = 1;
    }
}
