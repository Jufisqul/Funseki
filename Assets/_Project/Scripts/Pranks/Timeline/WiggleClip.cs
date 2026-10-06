using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Funseki.Pranks
{
    // A move in a WiggleTrack (see WiggleMode).
    public class WiggleClip : PlayableAsset, ITimelineClipAsset
    {
        public WiggleMode mode = WiggleMode.Shake;
        [Tooltip("m for Shake/Hop, degrees for LookAround")]
        public float amplitude = 0.05f;
        [Tooltip("Times per second")]
        public float frequency = 8f;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<WiggleBehaviour>.Create(graph);
            var b = playable.GetBehaviour();
            b.mode = mode;
            b.amplitude = amplitude;
            b.frequency = frequency;
            return playable;
        }
    }

    public class WiggleBehaviour : PlayableBehaviour
    {
        public WiggleMode mode;
        public float amplitude;
        public float frequency;

        Transform target;
        Vector3 basePos;
        Quaternion baseRot;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var t = playerData as Transform;
            if (t == null) return;
            if (target != t)
            {
                Restore();
                target = t;
                basePos = t.localPosition;
                baseRot = t.localRotation;
            }

            float time = (float)playable.GetTime();
            float phase = time * frequency * Mathf.PI * 2f;
            switch (mode)
            {
                case WiggleMode.Shake:
                    t.localPosition = basePos + new Vector3(Mathf.Sin(phase) * amplitude, 0f, Mathf.Sin(phase * 1.37f + 1f) * amplitude);
                    t.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.9f) * amplitude * 100f);
                    break;
                case WiggleMode.Hop:
                    t.localPosition = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(phase * 0.5f)) * amplitude;
                    t.localRotation = baseRot;
                    break;
                case WiggleMode.LookAround:
                    t.localPosition = basePos;
                    t.localRotation = baseRot * Quaternion.Euler(0f, Mathf.Sin(phase) * amplitude, 0f);
                    break;
                case WiggleMode.Spin:
                    t.localPosition = basePos;
                    t.localRotation = baseRot * Quaternion.Euler(0f, time * frequency * 360f, 0f);
                    break;
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info) => Restore();
        public override void OnPlayableDestroy(Playable playable) => Restore();

        void Restore()
        {
            if (target == null) return;
            target.localPosition = basePos;
            target.localRotation = baseRot;
            target = null;
        }
    }
}
