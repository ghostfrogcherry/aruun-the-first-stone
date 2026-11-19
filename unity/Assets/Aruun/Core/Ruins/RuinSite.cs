using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Edits;
using Aruun.Core.Voxels;

namespace Aruun.Core.Ruins
{
    /// <summary>
    /// A place the old builders left in the ground: boxes of stone stamped into the world at
    /// generation, words carved into it, and mechanisms that answer spoken words. The echo ruin
    /// and the sealed vault are both built from this.
    ///
    /// Layout numbers are in voxels (4 per metre), in local axes: u to the right (east), v
    /// forward (south, world -Z), y up, measured from the site's origin (Ox, Oz).
    /// </summary>
    public abstract class RuinSite
    {
        protected struct Box
        {
            public int X0, Y0, Z0, X1, Y1, Z1;
            public ushort Block;
            /// <summary>Weathering: some of the box becomes this instead (moss), 0 for none.</summary>
            public ushort Wear;
        }

        readonly List<Box> _boxes = new List<Box>();
        protected int _minX = int.MaxValue, _maxX = int.MinValue, _minY = int.MaxValue, _maxY = int.MinValue, _minZ = int.MaxValue, _maxZ = int.MinValue;

        /// <summary>The site's origin and ground height there, in world voxels.</summary>
        public readonly int Ox, Oz, Ground;
        public readonly List<RuinInscription> Inscriptions = new List<RuinInscription>();
        public readonly List<RuinMechanism> Mechanisms = new List<RuinMechanism>();
        /// <summary>Glowing lamps, in metres, for the Game layer to light.</summary>
        public readonly List<Vec3> Lamps = new List<Vec3>();

        /// <summary>The block that carved words are cut into.</summary>
        protected ushort InscribedBlock;
        protected ushort LampBlock;

        protected RuinSite(int ox, int oz, int ground)
        {
            Ox = ox;
            Oz = oz;
            Ground = ground;
        }

        // ------------------------------------------------------------------ coordinates

        protected int WX(int u) { return Ox + u; }
        protected int WZ(int v) { return Oz - v; }
        protected Vec3 ToM(double u, double y, double v) { return new Vec3((Ox + u + 0.5) / WorldConfig.VoxelsPerMeter, y / (double)WorldConfig.VoxelsPerMeter, (Oz - v + 0.5) / WorldConfig.VoxelsPerMeter); }

        protected void Put(int u0, int y0, int v0, int u1, int y1, int v1, ushort block, ushort wear = 0)
        {
            var b = new Box
            {
                X0 = WX(Math.Min(u0, u1)), X1 = WX(Math.Max(u0, u1)),
                Y0 = Math.Min(y0, y1), Y1 = Math.Max(y0, y1),
                Z0 = WZ(Math.Max(v0, v1)), Z1 = WZ(Math.Min(v0, v1)),
                Block = block, Wear = wear,
            };
            _boxes.Add(b);
            _minX = Math.Min(_minX, b.X0); _maxX = Math.Max(_maxX, b.X1);
            _minY = Math.Min(_minY, b.Y0); _maxY = Math.Max(_maxY, b.Y1);
            _minZ = Math.Min(_minZ, b.Z0); _maxZ = Math.Max(_maxZ, b.Z1);
        }

        protected EditOp Op(int u0, int y0, int v0, int u1, int y1, int v1, ushort block)
        {
            return EditOp.Box(WX(Math.Min(u0, u1)), Math.Min(y0, y1), WZ(Math.Max(v0, v1)), WX(Math.Max(u0, u1)), Math.Max(y0, y1), WZ(Math.Min(v0, v1)), block, 98);
        }

        protected void Carve(int u0, int y0, int v0, int u1, int y1, int v1, string place, string word)
        {
            Put(u0, y0, v0, u1, y1, v1, InscribedBlock);
            Inscriptions.Add(new RuinInscription
            {
                Place = place, Word = word,
                X0 = WX(Math.Min(u0, u1)), X1 = WX(Math.Max(u0, u1)), Y0 = y0, Y1 = y1,
                Z0 = WZ(Math.Max(v0, v1)), Z1 = WZ(Math.Min(v0, v1)),
            });
        }

        protected void Lamp(int u, int y, int v)
        {
            Put(u, y, v, u + 1, y + 1, v + 1, LampBlock);
            Lamps.Add(ToM(u + 0.5, y + 1, v + 0.5));
        }

        // ------------------------------------------------------------------ queries

        public RuinMechanism Mechanism(string id)
        {
            foreach (var m in Mechanisms) if (m.Id == id) return m;
            return null;
        }

        /// <summary>Has this mechanism already answered, judging by the world?</summary>
        public static bool Done(VoxelWorld world, RuinMechanism m)
        {
            bool solid = world.Blocks.Get(world.Get(m.CheckX, m.CheckY, m.CheckZ)).Collision;
            return solid == m.DoneSolid;
        }

        public RuinInscription InscriptionAt(int x, int y, int z)
        {
            foreach (var i in Inscriptions) if (i.Contains(x, y, z)) return i;
            return null;
        }

        /// <summary>Is this voxel inside the site's outer box?</summary>
        protected bool InBounds(int x, int y, int z)
        {
            return x >= _minX && x <= _maxX && y >= _minY && y <= _maxY && z >= _minZ && z <= _maxZ;
        }

        // ------------------------------------------------------------------ worldgen

        /// <summary>Writes the site's parts into a freshly generated chunk, in order.</summary>
        public virtual void Stamp(VoxelChunk chunk)
        {
            const int n = WorldConfig.ChunkSize;
            int ox = chunk.Coord.OriginX, oy = chunk.Coord.OriginY, oz = chunk.Coord.OriginZ;
            if (ox > _maxX || ox + n <= _minX || oy > _maxY || oy + n <= _minY || oz > _maxZ || oz + n <= _minZ) return;
            foreach (var b in _boxes)
            {
                int x0 = Math.Max(b.X0, ox), x1 = Math.Min(b.X1, ox + n - 1);
                int y0 = Math.Max(b.Y0, oy), y1 = Math.Min(b.Y1, oy + n - 1);
                int z0 = Math.Max(b.Z0, oz), z1 = Math.Min(b.Z1, oz + n - 1);
                if (x0 > x1 || y0 > y1 || z0 > z1) continue;
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                            chunk.Set(x - ox, y - oy, z - oz, b.Wear != 0 && Town.Blueprint.Hash(x, y, z) % 5 == 0 ? b.Wear : b.Block);
            }
        }
    }
}
