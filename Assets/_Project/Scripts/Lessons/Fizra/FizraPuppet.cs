using UnityEngine;

namespace Funseki.Lessons.Fizra
{
    // Placeholder animation of one body in the PE line until the real «lazy» animations exist: moves a visual
    // transform (the hero's "Model" child, a student's "Body") by local offsets and puts it back afterwards.
    // The character's own root, controller and Animator are not touched.
    public class FizraPuppet
    {
        readonly Transform body;
        readonly Vector3 basePos, baseScale;
        readonly Quaternion baseRot;
        readonly float jumpHeight;

        FizraMove move;
        Vector3 stepDir;
        float t, duration;
        bool mistake;
        Vector3 tiltAxis;
        bool fallen;

        public Transform Body => body;

        public FizraPuppet(Transform body, float jumpHeight)
        {
            this.body = body;
            this.jumpHeight = jumpHeight;
            if (body == null) return;
            basePos = body.localPosition;
            baseRot = body.localRotation;
            baseScale = body.localScale;
        }

        public void Play(FizraMove m, Vector3 localDir, float time)
        {
            move = m;
            stepDir = localDir;
            duration = Mathf.Max(0.1f, time);
            mistake = false;
            t = 0f;
        }

        /// <summary>The mistake gag: a clumsy wobble.</summary>
        public void Mistake(float time, float tiltDegrees)
        {
            move = FizraMove.None;
            mistake = true;
            duration = Mathf.Max(0.1f, time);
            tiltAxis = Random.value < 0.5f ? Vector3.forward * tiltDegrees : Vector3.right * tiltDegrees;
            t = 0f;
        }

        /// <summary>Falls over and stays down (the «voice anthem» gag) until Reset.</summary>
        public void FallOver()
        {
            fallen = true;
            if (body == null) return;
            body.localRotation = baseRot * Quaternion.Euler(Random.value < 0.5f ? 85f : -85f, 0f, 0f);
            body.localPosition = basePos + Vector3.up * 0.25f;
        }

        public void Tick(float dt)
        {
            if (body == null || fallen) return;
            if (move == FizraMove.None && !mistake) return;
            t += dt;
            float k = Mathf.Clamp01(t / duration);
            float arc = Mathf.Sin(k * Mathf.PI);

            Vector3 pos = basePos;
            Vector3 scale = baseScale;
            Quaternion rot = baseRot;
            if (mistake) rot = baseRot * Quaternion.Euler(tiltAxis * arc * Mathf.Sin(k * Mathf.PI * 3f));
            else switch (move)
            {
                case FizraMove.Jump: pos += Vector3.up * jumpHeight * arc; break;
                case FizraMove.Step: pos += stepDir * arc; break;
                case FizraMove.Sit:
                    scale = new Vector3(baseScale.x * (1f + 0.15f * arc), baseScale.y * Mathf.Lerp(1f, sitScale, arc), baseScale.z);
                    break;
                case FizraMove.Catch: rot = baseRot * Quaternion.Euler(-12f * arc, 0f, 0f); break;
            }
            body.localPosition = pos;
            body.localScale = scale;
            body.localRotation = rot;
            if (k >= 1f) { move = FizraMove.None; mistake = false; }
        }

        public float sitScale = 0.6f;

        public void Reset()
        {
            move = FizraMove.None;
            mistake = false;
            fallen = false;
            if (body == null) return;
            body.localPosition = basePos;
            body.localRotation = baseRot;
            body.localScale = baseScale;
        }
    }
}
