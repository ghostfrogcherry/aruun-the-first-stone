using System;
using System.Collections.Generic;

namespace Aruun.Core.Art
{
    /// <summary>Where one part should be at a moment of a clip.</summary>
    public struct PartPose
    {
        /// <summary>Euler angles in degrees, already in Unity's handedness.</summary>
        public float X, Y, Z;
        /// <summary>Lift in model voxels.</summary>
        public float Lift;
    }

    /// <summary>
    /// Evaluates the procedural clips from models.json. The clips were authored in Godot, whose
    /// axes are mirrored on z relative to Unity, so swings around x and y change sign.
    /// </summary>
    public static class RigPose
    {
        public static void Evaluate(List<RigChannel> clip, float time, Dictionary<string, PartPose> into)
        {
            into.Clear();
            if (clip == null) return;
            foreach (var ch in clip)
            {
                float wave = (float)Math.Sin(2.0 * Math.PI * (ch.Freq * time + ch.Phase));
                PartPose p;
                into.TryGetValue(ch.Part, out p);
                if (ch.Bob.HasValue)
                    p.Lift += ch.Bob.Value * wave;
                else
                {
                    float deg = ch.Offset + ch.Amp * wave;
                    switch (ch.Axis)
                    {
                        case 'x': p.X -= deg; break;
                        case 'y': p.Y -= deg; break;
                        default: p.Z += deg; break;
                    }
                }
                into[ch.Part] = p;
            }
        }
    }
}
