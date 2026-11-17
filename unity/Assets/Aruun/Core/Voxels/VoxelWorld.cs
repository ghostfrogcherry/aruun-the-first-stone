using System.Collections.Concurrent;
using System.Collections.Generic;
using Aruun.Core.Content;
using Aruun.Core.Edits;
using Aruun.Core.Worldgen;

namespace Aruun.Core.Voxels
{
    /// <summary>What an applied op changed: which chunks need new meshes and what was dug out.</summary>
    public sealed class EditResult
    {
        public readonly HashSet<ChunkCoord> Dirty = new HashSet<ChunkCoord>();
        /// <summary>Block id to number of voxels removed, for drops.</summary>
        public readonly Dictionary<ushort, int> Removed = new Dictionary<ushort, int>();
        public int Changed;
    }

    /// <summary>
    /// The voxels of one world: terrain from the seed plus every edit made since.
    /// Only the edits are stored, so a chunk can be dropped from memory and rebuilt at any time.
    ///
    /// Threads: any thread may read (GetChunk, Get, FillPadded). Apply must be called from one
    /// thread at a time (the simulation thread).
    /// </summary>
    public sealed class VoxelWorld
    {
        public readonly BlockRegistry Blocks;
        public readonly TerrainGenerator Generator;

        readonly ConcurrentDictionary<ChunkCoord, VoxelChunk> _chunks = new ConcurrentDictionary<ChunkCoord, VoxelChunk>();
        readonly Dictionary<ChunkCoord, Dictionary<int, ushort>> _edits = new Dictionary<ChunkCoord, Dictionary<int, ushort>>();
        readonly object _editLock = new object();

        public VoxelWorld(int seed, BlockRegistry blocks)
        {
            Blocks = blocks;
            Generator = new TerrainGenerator(seed, blocks);
        }

        public int LoadedChunks { get { return _chunks.Count; } }
        public int EditedChunks { get { lock (_editLock) return _edits.Count; } }

        public VoxelChunk GetChunk(ChunkCoord coord)
        {
            VoxelChunk chunk;
            if (_chunks.TryGetValue(coord, out chunk)) return chunk;
            chunk = Generator.Generate(coord);
            lock (_editLock)
            {
                // Put the edits back and publish under one lock, so an edit made while this
                // chunk was generating cannot fall between the two.
                Dictionary<int, ushort> edits;
                if (_edits.TryGetValue(coord, out edits))
                    foreach (var e in edits) chunk.Ids[e.Key] = e.Value;
                return _chunks.GetOrAdd(coord, chunk);
            }
        }

        /// <summary>Drops a chunk from memory. Its edits are kept and return when it is needed again.</summary>
        public void Unload(ChunkCoord coord)
        {
            VoxelChunk ignored;
            _chunks.TryRemove(coord, out ignored);
        }

        public ushort Get(int x, int y, int z)
        {
            return GetChunk(ChunkCoord.FromVoxel(x, y, z))
                .Get(x & WorldConfig.ChunkMask, y & WorldConfig.ChunkMask, z & WorldConfig.ChunkMask);
        }

        /// <summary>Copies a chunk and one voxel of every neighbour into a 34x34x34 buffer for the mesher.</summary>
        public void FillPadded(ChunkCoord coord, ushort[] padded)
        {
            const int p = GreedyMesher.Padded;
            var near = new VoxelChunk[27];
            for (int py = 0; py < p; py++)
            {
                int cy = py == 0 ? -1 : py == p - 1 ? 1 : 0;
                int ly = (py - 1) & WorldConfig.ChunkMask;
                for (int pz = 0; pz < p; pz++)
                {
                    int cz = pz == 0 ? -1 : pz == p - 1 ? 1 : 0;
                    int lz = (pz - 1) & WorldConfig.ChunkMask;
                    for (int px = 0; px < p; px++)
                    {
                        int cx = px == 0 ? -1 : px == p - 1 ? 1 : 0;
                        int lx = (px - 1) & WorldConfig.ChunkMask;
                        int slot = (cx + 1) + (cz + 1) * 3 + (cy + 1) * 9;
                        VoxelChunk c = near[slot] ?? (near[slot] = GetChunk(coord.Offset(cx, cy, cz)));
                        padded[GreedyMesher.PaddedIndex(px, py, pz)] = c.Ids[VoxelChunk.Index(lx, ly, lz)];
                    }
                }
            }
        }

        /// <summary>Applies one op. Every machine must apply the same ops in the same order.</summary>
        public EditResult Apply(EditOp op)
        {
            var result = new EditResult();
            if (!op.IsValid()) return result;
            lock (_editLock)
            {
                op.ForEachCell((x, y, z) => SetCell(x, y, z, op.Block, op.Power, result));
            }
            return result;
        }

        void SetCell(int x, int y, int z, ushort block, int power, EditResult result)
        {
            var coord = ChunkCoord.FromVoxel(x, y, z);
            VoxelChunk chunk;
            if (!_chunks.TryGetValue(coord, out chunk))
            {
                // Generate without publishing twice: GetChunk takes the same (re-entrant) lock.
                chunk = GetChunk(coord);
            }
            int lx = x & WorldConfig.ChunkMask, ly = y & WorldConfig.ChunkMask, lz = z & WorldConfig.ChunkMask;
            int index = VoxelChunk.Index(lx, ly, lz);
            ushort old = chunk.Ids[index];
            if (old == block) return;
            BlockDef was = Blocks.Get(old);
            if (block == 0 && !EditRules.Breaks(was.Hardness, power)) return;
            // Building fills empty space (air, water, plants). It never overwrites something solid.
            if (block != 0 && was.Collision) return;

            chunk.Ids[index] = block;
            Dictionary<int, ushort> edits;
            if (!_edits.TryGetValue(coord, out edits)) _edits[coord] = edits = new Dictionary<int, ushort>();
            edits[index] = block;

            result.Changed++;
            if (block == 0)
            {
                int count;
                result.Removed.TryGetValue(old, out count);
                result.Removed[old] = count + 1;
            }
            // The chunk needs a new mesh, and so does any neighbour this cell touches.
            const int last = WorldConfig.ChunkSize - 1;
            result.Dirty.Add(coord);
            if (lx == 0) result.Dirty.Add(coord.Offset(-1, 0, 0));
            if (lx == last) result.Dirty.Add(coord.Offset(1, 0, 0));
            if (ly == 0) result.Dirty.Add(coord.Offset(0, -1, 0));
            if (ly == last) result.Dirty.Add(coord.Offset(0, 1, 0));
            if (lz == 0) result.Dirty.Add(coord.Offset(0, 0, -1));
            if (lz == last) result.Dirty.Add(coord.Offset(0, 0, 1));
        }
    }
}
