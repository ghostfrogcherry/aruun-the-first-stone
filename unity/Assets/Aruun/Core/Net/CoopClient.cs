using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Play;
using Aruun.Core.Survival;

namespace Aruun.Core.Net
{
    /// <summary>What the host says when a friend joins.</summary>
    public sealed class Welcome
    {
        public int Id, Seed;
        public double Time;
        /// <summary>Where the friend's character starts, metres.</summary>
        public Vec3 Spawn;
        public readonly List<string> Flags = new List<string>();
        /// <summary>Every edit made to the world so far: replay them before showing it.</summary>
        public readonly List<EditOp> Edits = new List<EditOp>();
    }

    /// <summary>
    /// Joining a friend's world. Connect, build the world from the host's seed and edits, then
    /// attach a local Host as a mirror: the Game layer reads it as usual, its actions travel to
    /// the real host, and Pump copies the real host's world into it each frame. Movement stays
    /// on this machine (the host trusts where friends say they are).
    /// </summary>
    public sealed class CoopClient : ICoopLink, IDisposable
    {
        readonly TcpClient _tcp;
        readonly Stream _stream;
        readonly object _writeLock = new object();
        readonly ConcurrentQueue<Frame> _inbox = new ConcurrentQueue<Frame>();
        Host _host;
        int _id;
        volatile bool _closed;

        /// <summary>Names of the other players, by id.</summary>
        public readonly Dictionary<int, string> Names = new Dictionary<int, string>();
        /// <summary>Set when the connection drops, with the reason.</summary>
        public string Lost { get; private set; }
        /// <summary>The character as the host last sent it, to save on this machine.</summary>
        public string CharacterText { get; private set; }

        CoopClient(TcpClient tcp)
        {
            _tcp = tcp;
            _tcp.NoDelay = true;
            _stream = tcp.GetStream();
        }

        /// <summary>
        /// Connects and waits for the host's welcome. Throws with a readable message when the
        /// host can't be reached or says no.
        /// </summary>
        public static CoopClient Join(string address, int port, string name, string characterText, out Welcome welcome, int timeoutMs = 8000)
        {
            var tcp = new TcpClient();
            try
            {
                if (!tcp.ConnectAsync(address, port).Wait(timeoutMs)) throw new IOException("No answer from " + address + ":" + port + ".");
            }
            catch (AggregateException e) { tcp.Close(); throw new IOException("Could not reach " + address + ":" + port + " (" + e.InnerException?.Message + ")."); }
            var c = new CoopClient(tcp);
            c.Write("hello", Wire.Join(Protocol.Version, name, Wire.Pack(characterText ?? "")));
            tcp.ReceiveTimeout = timeoutMs;
            Frame f;
            try { f = Frames.Read(c._stream); }
            catch (IOException) { tcp.Close(); throw new IOException("The host did not answer in time."); }
            tcp.ReceiveTimeout = 0;
            if (f == null) { tcp.Close(); throw new IOException("The host closed the connection."); }
            if (f.Kind == "refused") { tcp.Close(); throw new IOException(f.Body); }
            if (f.Kind != "welcome") { tcp.Close(); throw new IOException("Unexpected answer from the host: " + f.Kind); }
            welcome = ParseWelcome(f);
            c._id = welcome.Id;
            new Thread(c.ReadLoop) { IsBackground = true, Name = "co-op client reader" }.Start();
            return c;
        }

        static Welcome ParseWelcome(Frame f)
        {
            var w = new Welcome();
            string[] lines = f.Lines;
            string[] head = Wire.Split(lines[0]);
            w.Id = Wire.I(head[0]);
            w.Seed = Wire.I(head[1]);
            w.Time = Wire.D(head[2]);
            w.Spawn = Wire.V(head, 3);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("F\t", StringComparison.Ordinal))
                {
                    foreach (var flag in line.Substring(2).Split(',')) if (flag.Length > 0) w.Flags.Add(flag);
                }
                else if (line.StartsWith("J\t", StringComparison.Ordinal))
                {
                    EditOp op;
                    if (EditOp.TryParse(line.Substring(2), out op)) w.Edits.Add(op);
                }
            }
            return w;
        }

        void ReadLoop()
        {
            try
            {
                while (!_closed)
                {
                    Frame f = Frames.Read(_stream);
                    if (f == null) break;
                    _inbox.Enqueue(f);
                }
            }
            catch { }
            if (!_closed) _inbox.Enqueue(new Frame("lost", "The connection to the host was lost."));
        }

        /// <summary>Makes `host` a mirror of the real one. The local player must already have joined it with the welcome's id.</summary>
        public void Attach(Host host, Welcome welcome)
        {
            _host = host;
            host.Link = this;
            host.Time = welcome.Time;
            host.SpawnCreatures = false;
            host.WorldFlags.Clear();
            foreach (var flag in welcome.Flags) host.WorldFlags.Add(flag);
        }

        public void Send(string kind, string body)
        {
            if (_closed || Lost != null) return;
            try { Write(kind, body); }
            catch (Exception e) { Lost = "The connection to the host was lost (" + e.Message + ")."; }
        }

        void Write(string kind, string body) { lock (_writeLock) Frames.Write(_stream, kind, body); }

        // ------------------------------------------------------------------ world in

        /// <summary>Copies everything the host sent since the last call into the mirror. Call once a frame, after Host.Tick (which only moves the clock on).</summary>
        public void Pump(double dt)
        {
            if (_host == null) return;
            Frame f;
            while (_inbox.TryDequeue(out f))
            {
                if (f.Kind == "lost") { Lost = f.Body; continue; }
                if (f.Kind == "state") ApplyState(f);
            }
        }

        void ApplyState(Frame f)
        {
            var seenPlayers = new HashSet<int>();
            var seenCreatures = new HashSet<int>();
            bool creaturesListed = false;
            List<PlantRec> plants = null;
            List<StructureRec> structs = null;
            PlayerRec me;
            _host.Players.TryGetValue(_id, out me);
            foreach (string line in f.Lines)
            {
                if (line.Length == 0) continue;
                string[] a = Wire.Split(line);
                switch (a[0])
                {
                    case "T": _host.Time = Wire.D(a[1]); break;
                    case "P":
                        {
                            int id = Wire.I(a[1]);
                            seenPlayers.Add(id);
                            Names[id] = a.Length > 10 ? a[10] : "";
                            if (id == _id) break;
                            PlayerRec p;
                            if (!_host.Players.TryGetValue(id, out p))
                                p = _host.Join(id, new CharacterState(_host.Items, _host.Rules), Wire.V(a, 2), false);
                            p.Pos = Wire.V(a, 2);
                            p.Facing = Wire.V(a, 5);
                            p.Guard = a[8] == "1";
                            p.C.Dead = a[9] != "1";
                            break;
                        }
                    case "C":
                        {
                            creaturesListed = true;
                            int id = Wire.I(a[1]);
                            seenCreatures.Add(id);
                            Creature c = _host.Creatures.Find(x => x.Id == id);
                            if (c == null)
                            {
                                CreatureDef def;
                                if (!_host.CreatureDefs.TryGetValue(a[2], out def)) break;
                                c = new Creature(id, def, Wire.V(a, 3));
                                _host.Creatures.Add(c);
                            }
                            c.Pos = Wire.V(a, 3);
                            c.Vel = Wire.V(a, 6);
                            c.Yaw = Wire.D(a[9]);
                            c.State = a[10];
                            c.Hp = Wire.D(a[11]);
                            c.HpMax = Wire.D(a[12]);
                            c.AnimForce = (Anim)Wire.I(a[13]);
                            c.Grounded = a[14] == "1";
                            break;
                        }
                    case "N":
                        {
                            int id = Wire.I(a[1]);
                            PersonRec r = _host.People.Find(x => x.Id == id);
                            if (r == null) break;
                            r.Pos = Wire.V(a, 2);
                            r.Yaw = Wire.D(a[5]);
                            r.TalkingTo = Wire.I(a[6]);
                            r.Walking = a[7] == "1";
                            break;
                        }
                    case "E":
                        {
                            EditOp op;
                            if (EditOp.TryParse(line.Substring(2), out op)) _host.ApplyRemote(op);
                            break;
                        }
                    case "M": _host.Messages.Add(new KeyValuePair<int, string>(_id, line.Substring(2))); break;
                    case "V":
                        _host.Events.Add(new HostEvent { Kind = a[1], Player = Wire.I(a[2]), Creature = Wire.I(a[3]), Value = Wire.D(a[4]), At = Wire.V(a, 5) });
                        break;
                    case "F":
                        _host.WorldFlags.Clear();
                        foreach (var flag in (a.Length > 1 ? a[1] : "").Split(',')) if (flag.Length > 0) _host.WorldFlags.Add(flag);
                        break;
                    case "L-": plants = new List<PlantRec>(); break;
                    case "L":
                        if (plants != null)
                            plants.Add(new PlantRec { Id = Wire.I(a[1]), Plant = a[2], X = Wire.I(a[3]), Y = Wire.I(a[4]), Z = Wire.I(a[5]), PlantedAt = Wire.D(a[6]), Mult = Wire.D(a[7]), WildId = a.Length > 8 ? a[8] : "" });
                        break;
                    case "U-": structs = new List<StructureRec>(); break;
                    case "U":
                        if (structs != null) structs.Add(new StructureRec { Id = Wire.I(a[1]), Key = a[2], Pos = Wire.V(a, 3), Owner = Wire.I(a[6]) });
                        break;
                    case "D": _host.RemoteDig = Wire.D(a[1]); break;
                    case "G":
                        if (me != null) me.Flags = new HashSet<string>((a.Length > 1 ? a[1] : "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                        break;
                    case "H":
                        CharacterText = Wire.Unpack(a[1]);
                        if (me != null)
                        {
                            int selected = me.C.Selected;
                            me.C.FromText(CharacterText);
                            me.C.Selected = selected;
                        }
                        break;
                    case "K":
                        if (me == null) break;
                        if (a.Length < 4) { me.Talk = null; break; }
                        var talk = new Conversation { Person = Wire.I(a[1]), Name = a[2], Say = a[3] };
                        for (int i = 4; i < a.Length; i++) talk.Choices.Add(a[i]);
                        me.Talk = talk;
                        break;
                    case "R": if (me != null) { me.RespawnAt = Wire.V(a, 1); me.Pos = me.RespawnAt.Value; } break;
                    case "Y": if (me != null) me.ArriveYaw = Wire.D(a[1]); break;
                    case "S": if (me != null) me.Shove = Wire.V(a, 1); break;
                }
            }
            // Whoever wasn't listed has left (players) or is gone (creatures).
            var gone = new List<int>();
            foreach (var id in _host.Players.Keys) if (id != _id && !seenPlayers.Contains(id)) gone.Add(id);
            foreach (var id in gone) _host.Players.Remove(id);
            if (creaturesListed || seenPlayers.Count > 0) _host.Creatures.RemoveAll(c => !seenCreatures.Contains(c.Id));
            if (plants != null) { _host.PlantsInWorld.Clear(); _host.PlantsInWorld.AddRange(plants); }
            if (structs != null) { _host.Structures.Clear(); _host.Structures.AddRange(structs); }
        }

        public void Dispose()
        {
            _closed = true;
            try { Write("bye", ""); } catch { }
            try { _tcp.Close(); } catch { }
        }
    }
}
