using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Interaction
{
    // Автомат с газировкой (GDD 5.9): E drops a physical can from the slot. With VendingMachineData.jamChance it jams
    // and spits jamCanCount cans; with breakChance it dies for good («Старая рухлядь»). Cans come from a pool and
    // vanish after canLifetime. Saved: broken or not.
    public class VendingMachine : InteractableObject
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Where cans come out; +Z points out of the machine")]
        [SerializeField] Transform slot;
        [Tooltip("Renderers that go dark when the machine breaks (screen, lights)")]
        [SerializeField] Renderer[] lights;
        [SerializeField] Color brokenLightColor = new(0.08f, 0.08f, 0.08f);

        class Can { public GameObject go; public Rigidbody body; public float hideAt; }

        VendingMachineData D => (VendingMachineData)data;
        readonly List<Can> active = new();
        readonly Stack<Can> pool = new();
        Transform canRoot;
        Material canMaterial;
        bool jamming;

        bool Broken => Flags.GetFlag(Key("broken"));

        public override string Prompt => Broken ? D.brokenPrompt : data.prompt;

        protected override bool CanUse(GameObject hero) => !jamming && (!Broken || !string.IsNullOrEmpty(D.brokenPrompt));

        protected override void RestoreState()
        {
            if (Broken) ShowBroken();
        }

        protected override void OnUsed(GameObject hero, bool first, GameObject witness)
        {
            if (Broken)
            {
                Say(hero, InteractableData.Pick(D.brokenLines));
                return;
            }

            float roll = Random.value;
            if (roll < D.jamChance)
            {
                Report(hero, "machine_jammed");
                Say(hero, InteractableData.Pick(D.jamLines));
                StartCoroutine(Jam());
            }
            else if (roll < D.jamChance + D.breakChance)
            {
                Flags.SetFlag(Key("broken"));
                ShowBroken();
                Report(hero, "machine_broken");
                Say(hero, InteractableData.Pick(D.breakLines));
            }
            else
            {
                Eject(D.ejectSpeed);
                Report(hero, "can_dropped");
                SayUsualLine(hero, first, witness);
            }
        }

        IEnumerator Jam()
        {
            jamming = true;
            var wait = new WaitForSeconds(D.jamInterval);
            for (int i = 0; i < D.jamCanCount; i++)
            {
                Eject(D.jamEjectSpeed);
                yield return wait;
            }
            jamming = false;
        }

        void Update()
        {
            float now = Time.time;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var can = active[i];
                if (now < can.hideAt) continue;
                can.go.SetActive(false);
                active.RemoveAt(i);
                pool.Push(can);
            }
        }

        void Eject(float speed)
        {
            var can = pool.Count > 0 ? pool.Pop() : CreateCan();
            Transform from = slot != null ? slot : transform;
            can.go.transform.SetPositionAndRotation(from.position, from.rotation * Quaternion.Euler(0f, 0f, 90f));
            can.go.SetActive(true);
            Vector3 dir = Quaternion.AngleAxis(Random.Range(-D.ejectSpread, D.ejectSpread), Vector3.up) * from.forward;
            can.body.linearVelocity = (dir + Vector3.up * 0.3f).normalized * speed;
            can.body.angularVelocity = Random.insideUnitSphere * 6f;
            can.hideAt = Time.time + D.canLifetime;
            active.Add(can);
        }

        Can CreateCan()
        {
            if (canRoot == null)
            {
                // Outside the machine, so cans are not part of it for the interactor and the NavMesh carving.
                canRoot = new GameObject($"Cans_{objectId}").transform;
                canRoot.SetParent(transform.parent, false);
            }
            GameObject go;
            if (D.canPrefab != null) go = Instantiate(D.canPrefab, canRoot);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.SetParent(canRoot, false);
                // The primitive's own CapsuleCollider already fits the cylinder.
                go.transform.localScale = new Vector3(D.canSize.x, D.canSize.y * 0.5f, D.canSize.z);
                var r = go.GetComponent<Renderer>();
                if (canMaterial == null)
                {
                    canMaterial = new Material(r.sharedMaterial);
                    canMaterial.SetColor(BaseColor, D.canColor);
                    canMaterial.SetColor(ColorId, D.canColor);
                }
                r.sharedMaterial = canMaterial;
            }
            go.name = "Can";
            if (!go.TryGetComponent<Rigidbody>(out var body)) body = go.AddComponent<Rigidbody>();
            body.mass = D.canMass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return new Can { go = go, body = body };
        }

        void ShowBroken()
        {
            if (lights == null) return;
            var block = new MaterialPropertyBlock();
            foreach (var r in lights)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, brokenLightColor);
                block.SetColor(ColorId, brokenLightColor);
                block.SetColor("_EmissionColor", Color.black);
                r.SetPropertyBlock(block);
            }
        }

        void OnDestroy()
        {
            if (canRoot != null) Destroy(canRoot.gameObject);
            if (canMaterial != null) Destroy(canMaterial);
        }
    }
}
