using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Aruun.Game
{
    /// <summary>
    /// --bench[=seconds]: flies the camera along the same route every time and measures how
    /// smoothly the game runs, so a change can be checked against the last one with numbers
    /// instead of a feeling. Writes bench.txt in the game's data folder (and the log), then quits.
    ///
    /// The route: a slow look around at the spawn, a long fast flight north (chunks stream in
    /// all the way), a turn, and a dash east at sprint-flying speed (the hardest case). It saves
    /// nothing: no character, no world.
    ///
    /// What the numbers mean: "1% low" is the frame rate of the slowest 1 in 100 frames, which
    /// is what you feel as stutter. A "hitch" is any frame over 33 ms (it drops below 30 fps).
    /// </summary>
    public sealed class Bench : MonoBehaviour
    {
        public static bool Running { get; private set; }

        float _seconds;
        WorldView _view;
        DevPlayer _player;
        FarTerrain _far;

        struct Frame
        {
            public float T, Ms;
            public int Gc0, Gc2, Meshes, Far;
        }

        public void Init(float seconds, WorldView view, DevPlayer player, FarTerrain far)
        {
            Running = true;
            _seconds = seconds;
            _view = view;
            _player = player;
            _far = far;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Vector3 start = _player.transform.position + Vector3.up * 6f;
            // Settle: let the first view finish building (at most 30 s) so the start is not counted.
            float t0 = Time.realtimeSinceStartup;
            _player.BenchPose(start, 45f, 8f);
            while (_view.PendingChunks > 0 && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            float settle = Time.realtimeSinceStartup - t0;
            for (int i = 0; i < 30; i++) yield return null;   // a few frames for the far terrain and shaders

            var frames = new List<Frame>(20000);
            long memStart = GC.GetTotalMemory(false);
            long nativeStart = Profiler.GetTotalAllocatedMemoryLong();
            int loadedPeak = 0, chunksStart = _view.World.LoadedChunks;
            int gc0 = GC.CollectionCount(0), gc2 = GC.CollectionCount(2), meshes = _view.MeshesUploaded, far = _far != null ? _far.Rebuilds : 0;
            float begin = Time.realtimeSinceStartup;
            float lookSecs = _seconds * 0.15f, northSecs = _seconds * 0.5f, turnSecs = _seconds * 0.05f;
            Vector3 pos = start;
            float lastT = 0f;
            while (true)
            {
                yield return null;
                float t = Time.realtimeSinceStartup - begin;
                if (t >= _seconds) break;
                float dt = t - lastT; lastT = t;
                float yaw, pitch = 4f;
                if (t < lookSecs) yaw = 45f + 360f * (t / lookSecs);                         // look around
                else if (t < lookSecs + northSecs) { yaw = 0f; pos += Vector3.forward * 20f * dt; }   // north at 20 m/s
                else if (t < lookSecs + northSecs + turnSecs) yaw = 90f * ((t - lookSecs - northSecs) / turnSecs);
                else { yaw = 90f; pos += Vector3.right * 45f * dt; }                            // east at 45 m/s
                // Keep a steady height over the land so the camera never flies into a hill.
                float ground = GroundMetres(pos.x, pos.z);
                pos.y = Mathf.Lerp(pos.y, ground + 6f, Mathf.Clamp01(dt * 2f));
                if (pos.y < ground + 2f) pos.y = ground + 2f;
                _player.BenchPose(pos, yaw, pitch);

                int g0 = GC.CollectionCount(0), g2 = GC.CollectionCount(2), m = _view.MeshesUploaded, f = _far != null ? _far.Rebuilds : 0;
                frames.Add(new Frame { T = t, Ms = Time.unscaledDeltaTime * 1000f, Gc0 = g0 - gc0, Gc2 = g2 - gc2, Meshes = m - meshes, Far = f - far });
                gc0 = g0; gc2 = g2; meshes = m; far = f;
                loadedPeak = Mathf.Max(loadedPeak, _view.World.LoadedChunks);
            }

            string report = Report(frames, settle, memStart, nativeStart, chunksStart, loadedPeak);
            string path = Path.Combine(Application.persistentDataPath, "bench.txt");
            try { File.WriteAllText(path, report); } catch (Exception e) { Debug.LogWarning("Could not write " + path + ": " + e.Message); }
            Debug.Log("[bench]\n" + report + "\n[bench] written to " + path);
            Running = false;
            Application.Quit();
        }

        float GroundMetres(float x, float z)
        {
            var field = _view.World.Generator.Field;
            float v = Aruun.Core.WorldConfig.VoxelSize;
            int h = Math.Max(field.HeightAt(Mathf.FloorToInt(x / v), Mathf.FloorToInt(z / v)), Aruun.Core.Worldgen.HeightField.SeaLevel);
            return (h + 1) * v;
        }

        string Report(List<Frame> frames, float settle, long memStart, long nativeStart, int chunksStart, int loadedPeak)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("Aruun bench, ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", ci)).Append('\n');
            sb.Append(SystemInfo.graphicsDeviceName).Append(", ").Append(SystemInfo.processorType).Append(" (").Append(SystemInfo.processorCount)
              .Append(" threads), ").Append(Screen.width).Append('x').Append(Screen.height).Append(", ").Append(Application.unityVersion).Append('\n');
            sb.Append("view ").Append(_view.ViewChunks).Append(" chunks, first view built in ").Append(settle.ToString("0.0", ci)).Append(" s\n\n");
            if (frames.Count == 0) return sb.Append("no frames measured\n").ToString();

            var ms = new float[frames.Count];
            double total = 0;
            for (int i = 0; i < ms.Length; i++) { ms[i] = frames[i].Ms; total += ms[i]; }
            Array.Sort(ms);
            float avgMs = (float)(total / ms.Length);
            float p50 = ms[ms.Length / 2], p99 = ms[Math.Min(ms.Length - 1, (int)(ms.Length * 0.99))];
            // 1% low: the average of the slowest 1% of frames, as frames per second.
            int n1 = Math.Max(1, ms.Length / 100);
            double slow = 0; for (int i = ms.Length - n1; i < ms.Length; i++) slow += ms[i];
            float low1 = (float)(1000.0 / (slow / n1));
            int hitches = 0, gc0 = 0, gc2 = 0, meshes = 0, far = 0;
            foreach (Frame f in frames) { if (f.Ms > 33.3f) hitches++; gc0 += f.Gc0; gc2 += f.Gc2; meshes += f.Meshes; far += f.Far; }

            sb.Append("frames      ").Append(frames.Count).Append(" in ").Append(_seconds.ToString("0", ci)).Append(" s\n");
            sb.Append("average     ").Append((1000f / avgMs).ToString("0", ci)).Append(" fps (").Append(avgMs.ToString("0.00", ci)).Append(" ms)\n");
            sb.Append("median      ").Append(p50.ToString("0.00", ci)).Append(" ms\n");
            sb.Append("99th pct    ").Append(p99.ToString("0.00", ci)).Append(" ms\n");
            sb.Append("1% low      ").Append(low1.ToString("0", ci)).Append(" fps\n");
            sb.Append("worst       ").Append(ms[ms.Length - 1].ToString("0.0", ci)).Append(" ms\n");
            sb.Append("hitches     ").Append(hitches).Append(" frames over 33 ms\n");
            sb.Append("GC          ").Append(gc0).Append(" small collections, ").Append(gc2).Append(" full\n");
            sb.Append("chunks      ").Append(meshes).Append(" meshes uploaded, far terrain rebuilt ").Append(far).Append(" times\n");
            sb.Append("loaded      ").Append(chunksStart).Append(" chunks at start, ").Append(_view.World.LoadedChunks).Append(" at end, peak ").Append(loadedPeak).Append('\n');
            sb.Append("memory      managed ").Append(Mb(memStart)).Append(" -> ").Append(Mb(GC.GetTotalMemory(false)))
              .Append(" MB, Unity ").Append(Mb(nativeStart)).Append(" -> ").Append(Mb(Profiler.GetTotalAllocatedMemoryLong())).Append(" MB\n\n");

            // The ten worst frames, with what else happened in them, to point at the cause.
            var worst = new List<Frame>(frames);
            worst.Sort((a, b) => b.Ms.CompareTo(a.Ms));
            sb.Append("worst frames (time, ms, what happened):\n");
            for (int i = 0; i < Math.Min(10, worst.Count); i++)
            {
                Frame f = worst[i];
                sb.Append("  ").Append(f.T.ToString("0.00", ci).PadLeft(6)).Append(" s ").Append(f.Ms.ToString("0.0", ci).PadLeft(6)).Append(" ms ");
                if (f.Gc2 > 0) sb.Append(" full-GC");
                else if (f.Gc0 > 0) sb.Append(" GC");
                if (f.Far > 0) sb.Append(" far-terrain");
                if (f.Meshes > 0) sb.Append(' ').Append(f.Meshes).Append("-meshes");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static string Mb(long bytes) { return (bytes / (1024.0 * 1024.0)).ToString("0", CultureInfo.InvariantCulture); }

        void OnDestroy() { Running = false; }
    }
}
