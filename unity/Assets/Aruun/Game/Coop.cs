using System;
using System.IO;
using Aruun.Core.Net;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// Co-op settings for this run: hosting (friends can join this world) or joining a friend's.
    /// Set from the title screen or the command line (--host[=port], --join=address[:port],
    /// --name=Name) before the world starts.
    /// </summary>
    public static class Coop
    {
        public static bool Hosting;
        public static int Port = CoopServer.DefaultPort;
        public static string Name = "Player";

        /// <summary>The connection to a friend's world, when joining.</summary>
        public static CoopClient Client;
        public static Welcome Welcome;
        public static string JoinedAddress = "";

        public static bool Joining { get { return Client != null; } }

        /// <summary>
        /// The character you take into friends' worlds. It lives on your machine and is sent to
        /// the host when you join; the host sends it back as it changes.
        /// </summary>
        public static string CharacterPath
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "coop");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "character.txt");
            }
        }

        /// <summary>Connects to a friend's world. Returns null when it worked, or what went wrong.</summary>
        public static string Connect(string address, bool newCharacter)
        {
            address = (address ?? "").Trim();
            if (address.Length == 0) return "Type the host's address (for example 192.168.1.20).";
            int port = CoopServer.DefaultPort;
            int colon = address.LastIndexOf(':');
            if (colon > 0 && int.TryParse(address.Substring(colon + 1), out port)) address = address.Substring(0, colon);
            else port = CoopServer.DefaultPort;
            string text = "";
            if (!newCharacter && File.Exists(CharacterPath))
            {
                try { text = File.ReadAllText(CharacterPath); } catch { text = ""; }
            }
            try
            {
                Welcome w;
                Client = CoopClient.Join(address, port, Name, text, out w);
                Welcome = w;
                JoinedAddress = address + (port != CoopServer.DefaultPort ? ":" + port : "");
                return null;
            }
            catch (Exception e)
            {
                Client = null;
                Welcome = null;
                return e.Message;
            }
        }

        public static void ReadName()
        {
            string n = PlayerPrefs.GetString("aruun.name", "");
            if (n.Length > 0) Name = n;
        }

        public static void SaveName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return;
            if (name.Length > 20) name = name.Substring(0, 20);
            Name = name;
            PlayerPrefs.SetString("aruun.name", name);
        }
    }
}
