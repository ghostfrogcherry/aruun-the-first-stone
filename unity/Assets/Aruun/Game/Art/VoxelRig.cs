using System.Collections.Generic;
using Aruun.Core.Art;
using UnityEngine;

namespace Aruun.Game.Art
{
    /// <summary>
    /// A voxel creature or person made of parts that swing around pivots, built by
    /// ModelLibrary.Instantiate from the "rig" block in content/models/models.json.
    ///
    ///   rig.Play("walk");              // names: rig.ClipNames
    ///   rig.Part("arm_r");             // the pivot transform of a part, e.g. to hold an item
    ///   rig.SetManual("arm_r", true);  // stop animating a part so game code can swing it
    ///
    /// Clips are small and procedural (sine swings and bobs). Good enough for walk cycles until
    /// real animation exists.
    /// </summary>
    public sealed class VoxelRig : MonoBehaviour
    {
        public string ModelId { get; private set; }
        public string CurrentClip { get; private set; }
        public float SpeedScale = 1f;

        ModelInfo _info;
        readonly Dictionary<string, Transform> _parts = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> _rest = new Dictionary<string, Vector3>();
        readonly HashSet<string> _manual = new HashSet<string>();
        readonly Dictionary<string, PartPose> _pose = new Dictionary<string, PartPose>();
        float _time;

        public IEnumerable<string> ClipNames { get { return _info.Clips.Keys; } }

        public void Setup(string id)
        {
            ModelId = id;
            _info = ModelLibrary.Info(id);
            float scale = _info.VoxelMeters;
            var pivots = new Dictionary<string, Vector3>();
            foreach (RigPart p in _info.Parts) // parents come first
            {
                var pivot = new Vector3(p.PivotX, p.PivotY, p.PivotZ);
                pivots[p.Name] = pivot;
                var node = new GameObject(p.Name).transform;
                Transform parent = p.Parent != "" ? _parts[p.Parent] : transform;
                node.SetParent(parent, false);
                node.localPosition = (pivot - (p.Parent != "" ? pivots[p.Parent] : Vector3.zero)) * scale;
                var meshObject = new GameObject("Mesh");
                meshObject.transform.SetParent(node, false);
                ModelLibrary.AddRenderer(meshObject, ModelLibrary.GetPartMesh(id, p));
                _parts[p.Name] = node;
                _rest[p.Name] = node.localPosition;
            }
            if (_info.Clips.ContainsKey("idle")) Play("idle");
        }

        public bool HasClip(string clip) { return _info != null && _info.Clips.ContainsKey(clip); }

        public void Play(string clip)
        {
            if (HasClip(clip)) CurrentClip = clip;
            else Debug.LogWarning("VoxelRig: '" + ModelId + "' has no clip '" + clip + "'");
        }

        public Transform Part(string name)
        {
            Transform t;
            return _parts.TryGetValue(name, out t) ? t : null;
        }

        public void SetManual(string part, bool manual)
        {
            if (manual) _manual.Add(part); else _manual.Remove(part);
        }

        void Update()
        {
            if (_info == null) return;
            _time += Time.deltaTime * SpeedScale;
            List<RigChannel> clip = null;
            if (CurrentClip != null) _info.Clips.TryGetValue(CurrentClip, out clip);
            RigPose.Evaluate(clip, _time, _pose);
            float blend = Mathf.Clamp01(Time.deltaTime * 8f); // ease between clips instead of snapping
            float scale = _info.VoxelMeters;
            foreach (var kv in _parts)
            {
                if (_manual.Contains(kv.Key)) continue;
                PartPose p;
                _pose.TryGetValue(kv.Key, out p);
                Transform t = kv.Value;
                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(p.X, p.Y, p.Z), blend);
                t.localPosition = _rest[kv.Key] + new Vector3(0f, p.Lift * scale, 0f);
            }
        }
    }
}
