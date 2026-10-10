using UnityEngine;

namespace Funseki.Heroes
{
    // Кайто's «Голоса» marker on an item: follows the item (without inheriting its scale), pulses its light,
    // and disappears as soon as the item is picked up.
    public class VoicesGlow : MonoBehaviour
    {
        Transform target;
        Light glowLight;
        float intensity, pulse;

        public void Setup(Transform item, Light light, float maxIntensity, float pulsesPerSecond)
        {
            target = item;
            glowLight = light;
            intensity = maxIntensity;
            pulse = pulsesPerSecond;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }
            // Just above the item, so the light falls on it and around it, not inside the mesh.
            transform.position = target.position + Vector3.up * 0.3f;
            if (glowLight == null) return;
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulse * Mathf.PI * 2f);
            glowLight.intensity = Mathf.Lerp(intensity * 0.35f, intensity, t);
        }
    }
}
