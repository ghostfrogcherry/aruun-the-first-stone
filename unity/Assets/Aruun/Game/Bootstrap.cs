using Aruun.Core;
using Aruun.Core.Content;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// Starts the game from code, in any scene, including an empty one. Nothing is wired up by
    /// hand in the editor: that keeps scenes out of merge conflicts and lets the whole game be
    /// started from the command line (see LaunchOptions for flags).
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindFirstObjectByType<WorldView>() != null) return;
            LaunchOptions options = LaunchOptions.FromCommandLine();
            BlockRegistry blocks = GameContent.LoadBlocks();
            var world = new VoxelWorld(options.Seed, blocks);

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camObject = new GameObject("Camera") { tag = "MainCamera" };
                cam = camObject.AddComponent<Camera>();
                camObject.AddComponent<AudioListener>();
            }
            float viewMeters = options.ViewMeters;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = viewMeters * 1.5f;
            cam.fieldOfView = 70f;
            // A clear blue sky over a living world. A real sky and day-night cycle come later.
            var sky = new Color(0.56f, 0.74f, 0.92f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = viewMeters * 0.55f;
            RenderSettings.fogEndDistance = viewMeters * 0.98f;

            // Start a little above dry land near the origin, looking across it.
            HeightField field = world.Generator.Field;
            int sx = 0, sz = 0;
            for (int i = 0; i < 400 && field.HeightAt(sx, sz) <= HeightField.SeaLevel + 2; i++) { sx += 16; sz += 9; }
            float ground = field.HeightAt(sx, sz) * WorldConfig.VoxelSize;
            cam.transform.position = new Vector3(sx * WorldConfig.VoxelSize, ground + 12f, sz * WorldConfig.VoxelSize);
            cam.transform.rotation = Quaternion.Euler(20f, 45f, 0f);

            var root = new GameObject("World");
            var view = root.AddComponent<WorldView>();
            view.ViewChunks = Mathf.CeilToInt(viewMeters / (WorldConfig.ChunkSize * WorldConfig.VoxelSize));
            view.Init(world, cam.transform);

            cam.gameObject.AddComponent<DevPlayer>().Init(view, options.Seed);
            Debug.Log("Aruun started. Seed " + options.Seed + ", view " + viewMeters + " m, content at " + GameContent.Root);
        }
    }
}
