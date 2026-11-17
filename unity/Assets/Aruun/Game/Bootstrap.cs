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
            MainCamera();
            if (!options.SkipTitle)
            {
                new GameObject("Title").AddComponent<TitleScreen>().Init(options);
                return;
            }
            StartWorld(options);
        }

        static Camera MainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camObject = new GameObject("Camera") { tag = "MainCamera" };
                cam = camObject.AddComponent<Camera>();
                camObject.AddComponent<AudioListener>();
            }
            return cam;
        }

        /// <summary>Loads the map and the world for options.Seed and drops the player at the spawn place.</summary>
        public static void StartWorld(LaunchOptions options)
        {
            BlockRegistry blocks = GameContent.LoadBlocks();
            WorldMap map = null;
            try
            {
                map = WorldMap.FromText(GameContent.ReadText("world", "map.json"), GameContent.ReadText("world", "aruun_biomes.txt"));
            }
            catch (System.Exception e)
            {
                Debug.LogError("Could not load the world map, using endless land instead: " + e.Message);
            }
            var world = new VoxelWorld(options.Seed, blocks, map);
            Camera cam = MainCamera();
            float viewMeters = options.ViewMeters;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = viewMeters * 1.5f;
            cam.fieldOfView = 70f;
            // A clear blue sky over a living world. A real sky and day-night cycle come later.
            var sky = new Color(0.56f, 0.74f, 0.92f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            // The voxel shader draws its own fog, so it works the same in every build.
            Shader.SetGlobalColor("_AruunFogColor", sky);
            Shader.SetGlobalVector("_AruunFog", new Vector4(viewMeters * 0.5f, viewMeters * 0.95f, 1f, 0f));

            // Start on dry land at the spawn place (the map puts it at the origin), looking north-east.
            HeightField field = world.Generator.Field;
            int sx = 0, sz = 0;
            for (int i = 0; i < 400 && field.HeightAt(sx, sz) <= HeightField.SeaLevel + 2; i++) { sx += 16; sz += 9; }
            float ground = field.HeightAt(sx, sz) * WorldConfig.VoxelSize;
            cam.transform.position = new Vector3(sx * WorldConfig.VoxelSize, ground + 1.7f, sz * WorldConfig.VoxelSize);
            cam.transform.rotation = Quaternion.Euler(5f, 45f, 0f);

            if (options.Pose != null)
            {
                cam.transform.position = new Vector3(options.Pose[0], options.Pose[1], options.Pose[2]);
                if (options.Pose.Length == 5) cam.transform.rotation = Quaternion.Euler(options.Pose[4], options.Pose[3], 0f);
            }

            // A sun for anything drawn with Unity's own lit shaders (the voxel shader lights itself).
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(-new Vector3(0.4f, 0.8f, 0.3f));

            var root = new GameObject("World");
            var view = root.AddComponent<WorldView>();
            view.ViewChunks = Mathf.CeilToInt(viewMeters / (WorldConfig.ChunkSize * WorldConfig.VoxelSize));
            view.Init(world, cam.transform);

            cam.gameObject.AddComponent<DevPlayer>().Init(view, map, options.Seed, options.Fly || options.Pose != null);
            if (!string.IsNullOrEmpty(options.ShotPath)) DevInput.Enabled = false;
            if (!string.IsNullOrEmpty(options.ShotPath))
                cam.gameObject.AddComponent<AutoScreenshot>().Init(options.ShotPath, options.ShotAfter, view);
            Debug.Log("Aruun started. Seed " + options.Seed + ", view " + viewMeters + " m, content at " + GameContent.Root);
        }
    }
}
