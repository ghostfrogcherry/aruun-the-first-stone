namespace Aruun.Core.Voxels
{
    /// <summary>
    /// A 32x32x32 block of voxel ids. Index order is x + z*32 + y*1024.
    ///
    /// Stored small, because the world keeps thousands of these in memory: a chunk of one
    /// block (open sky, solid rock) holds no array at all; a chunk with up to 16 kinds of block
    /// (nearly every chunk) keeps a 16-entry palette and half a byte per voxel (16 KB); only a
    /// chunk with more kinds keeps a full ushort per voxel (64 KB). That cut memory about six
    /// times and most of the garbage collection pauses that came with it.
    ///
    /// Threads: one writer at a time (worldgen before publishing, then edits under the world's
    /// lock), any number of readers. The palette never shrinks or moves, so a reader that sees
    /// a new index also sees its entry.
    /// </summary>
    public sealed class VoxelChunk
    {
        public readonly ChunkCoord Coord;
        const int Size = WorldConfig.ChunkVolume;

        ushort[] _full;                              // set once there are more than 16 kinds
        readonly ushort[] _palette = new ushort[16];
        int _count = 1;                              // palette entries in use; entry 0 is the start block (air)
        byte[] _packed;                              // two palette indices per byte; null = every voxel is entry 0

        public VoxelChunk(ChunkCoord coord) { Coord = coord; }

        /// <summary>A chunk that writes straight into a full array (worldgen's scratch). Call Compact when done.</summary>
        public VoxelChunk(ChunkCoord coord, ushort[] full) { Coord = coord; _full = full; }

        public static int Index(int lx, int ly, int lz)
        {
            return lx | (lz << WorldConfig.ChunkShift) | (ly << (2 * WorldConfig.ChunkShift));
        }

        public ushort Get(int lx, int ly, int lz) { return Get(Index(lx, ly, lz)); }
        public void Set(int lx, int ly, int lz, ushort id) { Set(Index(lx, ly, lz), id); }

        public ushort Get(int index)
        {
            ushort[] full = _full;
            if (full != null) return full[index];
            byte[] packed = _packed;
            if (packed == null) return _palette[0];
            int b = packed[index >> 1];
            return _palette[(index & 1) == 0 ? b & 15 : b >> 4];
        }

        public void Set(int index, ushort id)
        {
            if (_full != null) { _full[index] = id; return; }
            int slot = -1;
            for (int i = 0; i < _count; i++) if (_palette[i] == id) { slot = i; break; }
            if (slot < 0)
            {
                if (_count == 16) { Expand()[index] = id; return; }
                slot = _count;
                _palette[slot] = id;
                _count = slot + 1;          // published after the entry is written
            }
            if (_packed == null)
            {
                if (slot == 0) return;      // already every voxel
                _packed = new byte[Size / 2];
            }
            int at = index >> 1;
            int b = _packed[at];
            _packed[at] = (byte)((index & 1) == 0 ? (b & 0xF0) | slot : (b & 0x0F) | (slot << 4));
        }

        ushort[] Expand()
        {
            var full = new ushort[Size];
            for (int i = 0; i < Size; i++) full[i] = Get(i);
            _full = full;
            return full;
        }

        /// <summary>Every voxel, in index order (a copy).</summary>
        public ushort[] ToArray()
        {
            var a = new ushort[Size];
            for (int i = 0; i < Size; i++) a[i] = Get(i);
            return a;
        }

        /// <summary>Bytes of voxel storage this chunk holds (for stats).</summary>
        public int StorageBytes { get { return _full != null ? Size * 2 : _packed != null ? Size / 2 : 0; } }

        /// <summary>A small copy of this chunk (uniform, palette or full, whichever fits).</summary>
        public VoxelChunk Compact()
        {
            ushort[] src = _full;
            if (src == null) return this;
            var c = new VoxelChunk(Coord);   // already compact
            // Find the kinds of block, at most 16.
            int count = 0;
            var pal = c._palette;
            bool many = false;
            for (int i = 0; i < Size && !many; i++)
            {
                ushort v = src[i];
                int k = 0;
                while (k < count && pal[k] != v) k++;
                if (k == count)
                {
                    if (count == 16) many = true;
                    else pal[count++] = v;
                }
            }
            if (many)
            {
                c._full = (ushort[])src.Clone();
                return c;
            }
            c._count = count;
            if (count == 1) return c;
            var packed = new byte[Size / 2];
            for (int i = 0; i < Size; i += 2)
            {
                int a = Slot(pal, count, src[i]), b = Slot(pal, count, src[i + 1]);
                packed[i >> 1] = (byte)(a | (b << 4));
            }
            c._packed = packed;
            return c;
        }

        static int Slot(ushort[] pal, int count, ushort v)
        {
            for (int k = 0; k < count; k++) if (pal[k] == v) return k;
            return 0;
        }
    }
}
