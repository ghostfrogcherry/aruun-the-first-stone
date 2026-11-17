using System;
using System.Collections.Generic;
using Aruun.Core.Edits;

namespace Aruun.Core.Language
{
    /// <summary>Why a phrase did nothing.</summary>
    public enum SpeakError
    {
        None,
        Empty,
        UnknownWord,
        NotLearned,
        NoAction,
        TwoActions,
        TwoThings,
        WrongThing,
        OutOfRange,
        NoDirection,
        NoHandler,
    }

    /// <summary>A push, pull or weight on creatures and loose things. Units are metres and seconds.</summary>
    public struct ForceEffect
    {
        public string Kind;            // push, pull, weigh
        public double OriginX, OriginY, OriginZ;
        public double DirX, DirY, DirZ; // normalised
        public double RangeM;
        public double ConeDeg;
        public double ImpulseMps;
        public double DurationS;
    }

    /// <summary>What a spoken phrase does, once the host has checked it.</summary>
    public sealed class SpeakResult
    {
        public SpeakError Error;
        /// <summary>The first word that caused the error, if one did.</summary>
        public string Word = "";
        public readonly List<EditOp> Edits = new List<EditOp>();
        public readonly List<ForceEffect> Forces = new List<ForceEffect>();
        /// <summary>Gravitational debt the speaker takes on.</summary>
        public double Debt;
        public double CooldownSeconds;
        public string Action = "";
        public string Thing = "";

        public bool Ok { get { return Error == SpeakError.None; } }
    }

    /// <summary>
    /// Turns a spoken phrase into effects. A phrase is one action word, at most one thing word
    /// and any number of modifiers, in any order. It never runs anything open-ended: every
    /// action maps to a fixed effect handler, and every number is clamped to that effect's
    /// limits in the lexicon, however many modifiers are stacked. The host runs this; clients
    /// only ever see the resulting edits and forces.
    /// </summary>
    public static class Interpreter
    {
        /// <summary>Above this total scale, extra modifiers stop adding strength (debt still grows).</summary>
        public const double MaxTotalScale = 3.0;

        /// <summary>
        /// caster and aim are in voxels (aim need not be normalised). target is the voxel the
        /// speaker is looking at, for words that act on the ground.
        /// </summary>
        public static SpeakResult Speak(Lexicon lex, WordKnowledge known, IList<string> phrase,
            double casterX, double casterY, double casterZ, double aimX, double aimY, double aimZ,
            int targetX, int targetY, int targetZ, Func<string, ushort> blockId)
        {
            var r = new SpeakResult();
            if (phrase == null || phrase.Count == 0) return Fail(r, SpeakError.Empty, "");

            WordDef action = null, thing = null;
            double scale = 1, debtScale = 1;
            foreach (string id in phrase)
            {
                WordDef w = lex.Word(id);
                if (w == null) return Fail(r, SpeakError.UnknownWord, id);
                if (!known.Knows(id)) return Fail(r, SpeakError.NotLearned, id);
                switch (w.Role)
                {
                    case WordRole.Action:
                        if (action != null) return Fail(r, SpeakError.TwoActions, id);
                        action = w;
                        break;
                    case WordRole.Thing:
                        if (thing != null) return Fail(r, SpeakError.TwoThings, id);
                        thing = w;
                        break;
                    default:
                        scale *= w.Scale;
                        debtScale *= w.DebtScale;
                        break;
                }
            }
            if (action == null) return Fail(r, SpeakError.NoAction, phrase[0]);
            EffectDef effect = lex.Effect(action.Effect);
            if (effect == null || Array.IndexOf(Lexicon.Handlers, action.Effect) < 0) return Fail(r, SpeakError.NoHandler, action.Id);
            // A thing word must name something this action can work on.
            if (thing != null && !Accepts(effect.Targets, thing.Effect)) return Fail(r, SpeakError.WrongThing, thing.Id);

            scale = Math.Min(scale, MaxTotalScale);
            r.Action = action.Id;
            r.Thing = thing != null ? thing.Id : "";
            r.Debt = action.Debt * debtScale;
            r.CooldownSeconds = action.CooldownSeconds;

            if (action.Effect == "raise_column")
            {
                double dx = targetX - casterX, dy = targetY - casterY, dz = targetZ - casterZ;
                double reach = action.RangeVoxels + WorldConfig.ServerReachSlack;
                if (dx * dx + dy * dy + dz * dz > reach * reach) return Fail(r, SpeakError.OutOfRange, action.Id);
                int height = Clamp((int)Math.Round(action.Num("height") * scale), 1, (int)effect.Max("max_height"));
                int radius = Clamp((int)Math.Round(action.Num("radius") * Math.Sqrt(scale)), 0, (int)effect.Max("max_radius"));
                ushort block = blockId(thing != null && thing.Str("block") != "" ? thing.Str("block") : action.Str("block"));
                // One box per column of the disc. Building never overwrites solid ground, so
                // the column rises out of the ground through air, water and plants only.
                for (int ox = -radius; ox <= radius; ox++)
                    for (int oz = -radius; oz <= radius; oz++)
                        if (ox * ox + oz * oz <= radius * radius)
                            r.Edits.Add(EditOp.Box(targetX + ox, targetY + 1, targetZ + oz, targetX + ox, targetY + height, targetZ + oz, block));
                return r;
            }

            // Force words.
            double len = Math.Sqrt(aimX * aimX + aimY * aimY + aimZ * aimZ);
            if (len < 1e-6) return Fail(r, SpeakError.NoDirection, action.Id);
            var f = new ForceEffect
            {
                Kind = action.Effect.Substring("force_".Length),
                OriginX = casterX * WorldConfig.VoxelSize,
                OriginY = casterY * WorldConfig.VoxelSize,
                OriginZ = casterZ * WorldConfig.VoxelSize,
                DirX = aimX / len, DirY = aimY / len, DirZ = aimZ / len,
                RangeM = Math.Min(action.Num("range_m") * scale, effect.Max("max_range_m")),
                ConeDeg = action.Num("cone_deg", 30),
                ImpulseMps = Math.Min(action.Num("impulse_mps") * scale, effect.Max("max_impulse_mps")),
                DurationS = Math.Min(action.Num("duration_s") * scale, effect.Max("max_duration_s", 0)),
            };
            r.Forces.Add(f);
            return r;
        }

        /// <summary>Does an effect whose targets are `targets` accept a thing word naming `kind`?</summary>
        static bool Accepts(string targets, string kind)
        {
            if (targets == kind) return true;
            if (targets == "creatures" && kind == "creature") return true;
            if (targets == "creature" && kind == "creatures") return true;
            if (targets == "solid_voxel" && kind == "stone") return true;
            return false;
        }

        static SpeakResult Fail(SpeakResult r, SpeakError e, string word) { r.Error = e; r.Word = word; return r; }
        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }
    }
}
