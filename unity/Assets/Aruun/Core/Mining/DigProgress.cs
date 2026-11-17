using System.Collections.Generic;
using Aruun.Core.Content;

namespace Aruun.Core.Mining
{
    /// <summary>
    /// The host's record of how far each digger has worn down the block they are working on.
    /// Holding dig on a cell calls Hit every tick; looking away or switching tools starts over,
    /// and damage left alone for a while fades. Keyed by digger id so co-op players dig separately.
    /// </summary>
    public sealed class DigProgress
    {
        /// <summary>Seconds without a hit before the damage on a cell is forgotten.</summary>
        public const double ForgetAfter = 1.5;

        struct Work
        {
            public int X, Y, Z;
            public ushort Block;
            public string Tool;
            public double Done;
            public double LastHit;
        }

        readonly Dictionary<int, Work> _work = new Dictionary<int, Work>();

        public enum Outcome { Refused, Working, Broken }

        public struct HitResult
        {
            public Outcome Outcome;
            /// <summary>How worn the block is, 0 to 1. Use it for cracks on the block.</summary>
            public double Fraction;
            /// <summary>Why it was refused, for the player.</summary>
            public string Message;
        }

        /// <summary>
        /// One tick of digging by `digger` at cell (x, y, z), which holds `block`, with `tool`,
        /// for `dt` seconds, at world time `now` (seconds). When it reports Broken the host sends
        /// the edit op and the record is cleared.
        /// </summary>
        public HitResult Hit(int digger, int x, int y, int z, BlockDef block, ToolDef tool, double dt, double now)
        {
            string why = DigRules.WhyNot(block, tool);
            if (block.Id == 0 || why.Length > 0)
            {
                _work.Remove(digger);
                return new HitResult { Outcome = Outcome.Refused, Fraction = 0, Message = why };
            }
            Work w;
            bool same = _work.TryGetValue(digger, out w) && w.X == x && w.Y == y && w.Z == z &&
                        w.Block == block.Id && w.Tool == tool.Key && now - w.LastHit <= ForgetAfter;
            if (!same) w = new Work { X = x, Y = y, Z = z, Block = block.Id, Tool = tool.Key };
            w.Done += DigRules.Rate(block, tool) * dt;
            w.LastHit = now;
            if (w.Done >= block.Health)
            {
                _work.Remove(digger);
                return new HitResult { Outcome = Outcome.Broken, Fraction = 1 };
            }
            _work[digger] = w;
            return new HitResult { Outcome = Outcome.Working, Fraction = block.Health > 0 ? w.Done / block.Health : 1 };
        }

        /// <summary>The worn fraction of whatever this digger is working on, 0 when nothing.</summary>
        public double FractionFor(int digger, BlockRegistry blocks)
        {
            Work w;
            if (!_work.TryGetValue(digger, out w)) return 0;
            var b = blocks.Get(w.Block);
            return b.Health > 0 ? w.Done / b.Health : 0;
        }

        /// <summary>Forget a digger who stopped digging or left the world.</summary>
        public void Stop(int digger) { _work.Remove(digger); }
    }
}
