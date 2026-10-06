using Unity.Cinemachine;
using UnityEngine;

namespace Funseki.Dialogue
{
    // A Cinemachine camera that frames the hero and the NPC from the side during a conversation (GDD 5.3).
    // It takes over from the gameplay cameras by priority; the Brain blends in and out in cameraBlendTime.
    public class DialogueCamera
    {
        const string AnyCamera = "**ANY CAMERA**";

        readonly DialogueSettings settings;
        readonly CinemachineCamera cam;
        readonly CinemachineTargetGroup group;
        readonly CinemachineGroupFraming framing;
        readonly Transform heroPoint, npcPoint;
        Transform hero, npc;
        CinemachineBlenderSettings blends;

        public DialogueCamera(DialogueSettings settings, Transform parent)
        {
            this.settings = settings;
            var rig = new GameObject("DialogueCameraRig").transform;
            rig.SetParent(parent, false);

            heroPoint = new GameObject("HeroPoint").transform;
            heroPoint.SetParent(rig, false);
            npcPoint = new GameObject("NpcPoint").transform;
            npcPoint.SetParent(rig, false);
            group = new GameObject("Targets").AddComponent<CinemachineTargetGroup>();
            group.transform.SetParent(rig, false);

            var go = new GameObject("CM Dialogue");
            go.transform.SetParent(rig, false);
            go.SetActive(false);
            cam = go.AddComponent<CinemachineCamera>();
            go.AddComponent<CinemachineRotationComposer>();
            framing = go.AddComponent<CinemachineGroupFraming>();
            framing.FramingMode = CinemachineGroupFraming.FramingModes.Horizontal;
            framing.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.ZoomOnly;
            framing.LateralAdjustment = CinemachineGroupFraming.LateralAdjustmentModes.ChangeRotation;
            cam.LookAt = group.transform;
        }

        public void Begin(Transform hero, Transform npc)
        {
            this.hero = hero;
            this.npc = npc;
            if (hero == null && npc == null) return;

            group.Targets.Clear();
            if (hero != null) group.AddMember(heroPoint, 1f, 0.6f);
            if (npc != null) group.AddMember(npcPoint, 1f, 0.6f);
            Tick();

            framing.FramingSize = settings.cameraFramingSize;
            framing.FovRange = settings.cameraFovRange;
            cam.Priority = settings.cameraPriority;
            PlaceCamera();
            EnsureBlends();
            cam.gameObject.SetActive(true);
        }

        public void End() => cam.gameObject.SetActive(false);

        // Keeps the framed points at face height while the pair moves (call every LateUpdate).
        public void Tick()
        {
            Vector3 up = Vector3.up * settings.cameraLookHeight;
            if (hero != null) heroPoint.position = hero.position + up;
            if (npc != null) npcPoint.position = npc.position + up;
        }

        // To the side of the pair, on the side the gameplay camera already was, slightly behind the hero.
        void PlaceCamera()
        {
            Vector3 h = hero != null ? hero.position : npc.position;
            Vector3 n = npc != null ? npc.position : h + (hero != null ? hero.forward : Vector3.forward) * 1.5f;
            Vector3 d = n - h;
            d.y = 0f;
            float dist = d.magnitude;
            Vector3 dir = dist > 0.01f ? d / dist : Vector3.forward;
            Vector3 mid = (h + n) * 0.5f;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            var main = Camera.main;
            if (main != null && Vector3.Dot(main.transform.position - mid, side) < 0f) side = -side;

            Vector3 pos = mid + side * (settings.cameraSideDistance + dist * 0.5f) - dir * settings.cameraShoulderShift;
            pos.y = mid.y + settings.cameraHeight;
            Vector3 look = mid + Vector3.up * settings.cameraLookHeight - pos;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look, Vector3.up));
        }

        // The gameplay rig only sets the Brain's default blend; a custom blend makes entering a dialogue softer.
        void EnsureBlends()
        {
            var main = Camera.main;
            var brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
            if (brain == null) return;
            if (brain.CustomBlends != null && brain.CustomBlends != blends) return; // someone else's blend asset: leave it

            if (blends == null) blends = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            var def = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, settings.cameraBlendTime);
            blends.CustomBlends = new[]
            {
                new CinemachineBlenderSettings.CustomBlend { From = AnyCamera, To = cam.name, Blend = def },
                new CinemachineBlenderSettings.CustomBlend { From = cam.name, To = AnyCamera, Blend = def }
            };
            brain.CustomBlends = blends;
        }
    }
}
