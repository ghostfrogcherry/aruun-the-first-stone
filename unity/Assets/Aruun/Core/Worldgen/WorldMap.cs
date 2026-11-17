using System;
using System.Collections.Generic;
using Aruun.Core.Content;

namespace Aruun.Core.Worldgen
{
    /// <summary>One biome of the world map. Heights are voxels relative to sea level; weights are 16.16.</summary>
    public sealed class MapBiome
    {
        public int Id;
        public string Key = "", Name = "", Surface = "grass";
        /// <summary>Plant that grows thickly here (e.g. violet_bloom), or null.</summary>
        public string Flowers;
        public int Base, Relief;
        public int Lush, Trees, Mesas, Canyons;
    }

    /// <summary>A named place. X and Z are voxel coordinates (north is +Z).</summary>
    public sealed class MapPlace
    {
        public string Key = "", Name = "", Kind = "", Region = "";
        public int X, Z;
    }

    /// <summary>A great volcano: a smooth shield cone with a crater.</summary>
    public sealed class MapPeak
    {
        public string Key = "", Name = "";
        public int X, Z, Radius, Height;
        /// <summary>Crater radius as a fraction of the cone radius, 16.16.</summary>
        public int Crater;
    }

    /// <summary>
    /// The world of Aruun from content/world: map.json (biomes, volcanoes, places) and
    /// aruun_biomes.txt (one character per map cell, the biome id in base 36, rows north to
    /// south). The spawn place sits at voxel (0, 0); north is +Z and east is +X, so places keep
    /// their directions from each other whatever the seed.
    ///
    /// All numbers are turned into integers once, here, so worldgen stays integer only.
    /// </summary>
    public sealed class WorldMap
    {
        public readonly int Width, Height;
        /// <summary>Voxels per map cell.</summary>
        public readonly int Cell;
        public readonly MapBiome[] Biomes;
        public readonly IReadOnlyList<MapPlace> Places;
        public readonly IReadOnlyList<MapPeak> Peaks;
        public readonly MapPlace Spawn;
        readonly byte[] _cells;
        readonly int _spawnPx, _spawnPy;
        /// <summary>The spawn place's cell in the map image (pixels, y down).</summary>
        public int SpawnPx { get { return _spawnPx; } }
        public int SpawnPy { get { return _spawnPy; } }

        WorldMap(int w, int h, int cell, byte[] cells, MapBiome[] biomes, List<MapPlace> places,
                 List<MapPeak> peaks, MapPlace spawn, int spx, int spy)
        {
            Width = w; Height = h; Cell = cell; _cells = cells; Biomes = biomes;
            Places = places; Peaks = peaks; Spawn = spawn; _spawnPx = spx; _spawnPy = spy;
        }

        /// <summary>Biome of map cell (px, py) in image pixels; outside the map is the first biome (deep ocean).</summary>
        public MapBiome CellBiome(int px, int py)
        {
            if (px < 0 || py < 0 || px >= Width || py >= Height) return Biomes[0];
            return Biomes[_cells[py * Width + px]];
        }

        /// <summary>Biome of the map cell whose centre is at grid (gx, gz), counted from the spawn cell.</summary>
        public MapBiome GridBiome(int gx, int gz) { return CellBiome(_spawnPx + gx, _spawnPy - gz); }

        /// <summary>Biome of the cell that holds voxel column (x, z).</summary>
        public MapBiome BiomeAt(int x, int z)
        {
            return GridBiome(FloorDiv(x + Cell / 2, Cell), FloorDiv(z + Cell / 2, Cell));
        }

        public MapPlace Place(string key)
        {
            foreach (var p in Places) if (p.Key == key) return p;
            return null;
        }

        public static WorldMap FromText(string mapJson, string cellsText)
        {
            var root = MiniJson.ParseObject(mapJson);
            int cell = (int)Math.Round(MiniJson.Num(root, "cell_m", 32) * WorldConfig.VoxelsPerMeter);
            if (cell < 4) throw new FormatException("map.json: cell_m is too small");

            var biomeList = MiniJson.List(root, "biomes");
            var biomes = new MapBiome[biomeList.Count];
            foreach (var item in biomeList)
            {
                var o = (Dictionary<string, object>)item;
                var b = new MapBiome();
                b.Id = MiniJson.Int(o, "id", -1);
                if (b.Id < 0 || b.Id >= biomes.Length || biomes[b.Id] != null)
                    throw new FormatException("map.json: biome ids must run 0.." + (biomes.Length - 1) + " once each");
                b.Key = MiniJson.Str(o, "key");
                b.Name = MiniJson.Str(o, "name", b.Key);
                b.Surface = MiniJson.Str(o, "surface", "grass");
                string fl = MiniJson.Str(o, "flowers", "");
                b.Flowers = fl.Length > 0 ? fl : null;
                b.Base = Vox(MiniJson.Num(o, "height_m"));
                b.Relief = Vox(MiniJson.Num(o, "relief_m"));
                b.Lush = Fix(MiniJson.Num(o, "lush"));
                b.Trees = Fix(MiniJson.Num(o, "trees", 1));
                b.Mesas = Fix(MiniJson.Num(o, "mesas"));
                b.Canyons = Fix(MiniJson.Num(o, "canyons"));
                biomes[b.Id] = b;
            }
            if (biomes.Length == 0) throw new FormatException("map.json: no biomes");

            var rows = new List<string>();
            foreach (var line in cellsText.Split('\n'))
            {
                string t = line.TrimEnd('\r');
                if (t.Length > 0) rows.Add(t);
            }
            if (rows.Count == 0) throw new FormatException("aruun_biomes.txt is empty");
            int w = rows[0].Length, h = rows.Count;
            var cells = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                if (rows[y].Length != w) throw new FormatException("aruun_biomes.txt: row " + y + " has a different length");
                for (int x = 0; x < w; x++)
                {
                    int id = Base36(rows[y][x]);
                    if (id < 0 || id >= biomes.Length) throw new FormatException("aruun_biomes.txt: unknown biome '" + rows[y][x] + "' at " + x + "," + y);
                    cells[y * w + x] = (byte)id;
                }
            }

            string spawnKey = MiniJson.Str(root, "spawn");
            Dictionary<string, object> spawnObj = null;
            foreach (var item in MiniJson.List(root, "places"))
            {
                var o = (Dictionary<string, object>)item;
                if (MiniJson.Str(o, "key") == spawnKey) spawnObj = o;
            }
            if (spawnObj == null) throw new FormatException("map.json: spawn '" + spawnKey + "' is not a place");
            int spx = MiniJson.Int(spawnObj, "x"), spy = MiniJson.Int(spawnObj, "y");

            var places = new List<MapPlace>();
            MapPlace spawn = null;
            foreach (var item in MiniJson.List(root, "places"))
            {
                var o = (Dictionary<string, object>)item;
                var p = new MapPlace
                {
                    Key = MiniJson.Str(o, "key"), Name = MiniJson.Str(o, "name"),
                    Kind = MiniJson.Str(o, "kind"), Region = MiniJson.Str(o, "region"),
                    X = (MiniJson.Int(o, "x") - spx) * cell,
                    Z = (spy - MiniJson.Int(o, "y")) * cell,
                };
                if (p.Key == spawnKey) spawn = p;
                places.Add(p);
            }

            var peaks = new List<MapPeak>();
            foreach (var item in MiniJson.List(root, "peaks"))
            {
                var o = (Dictionary<string, object>)item;
                peaks.Add(new MapPeak
                {
                    Key = MiniJson.Str(o, "key"), Name = MiniJson.Str(o, "name"),
                    X = (MiniJson.Int(o, "x") - spx) * cell,
                    Z = (spy - MiniJson.Int(o, "y")) * cell,
                    Radius = Math.Max(1, (int)Math.Round(MiniJson.Num(o, "radius_cells") * cell)),
                    Height = Vox(MiniJson.Num(o, "height_m")),
                    Crater = Fix(MiniJson.Num(o, "crater")),
                });
            }
            return new WorldMap(w, h, cell, cells, biomes, places, peaks, spawn, spx, spy);
        }

        static int Vox(double metres) { return (int)Math.Round(metres * WorldConfig.VoxelsPerMeter); }
        static int Fix(double v) { return (int)Math.Round(v * 65536.0); }

        static int Base36(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'z') return c - 'a' + 10;
            return -1;
        }

        internal static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
