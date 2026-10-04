using UnityEngine;

// Dying fluorescent tube: mostly on, with short random blackouts.
public class LightFlicker : MonoBehaviour
{
    public Light target;
    public Renderer glowRenderer;
    public Color glowColor = new Color(1f, 0.92f, 0.66f);
    public float glowIntensity = 2f;
    public Vector2 onTime = new Vector2(0.6f, 3.5f);
    public Vector2 offTime = new Vector2(0.04f, 0.18f);

    float baseIntensity, timer;
    bool on = true;
    MaterialPropertyBlock block;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (target) baseIntensity = target.intensity;
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0) return;
        on = !on;
        timer = on ? Random.Range(onTime.x, onTime.y) : Random.Range(offTime.x, offTime.y);
        if (target) target.intensity = on ? baseIntensity : baseIntensity * 0.1f;
        if (glowRenderer)
        {
            glowRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, glowColor * (on ? glowIntensity : 0.15f));
            glowRenderer.SetPropertyBlock(block);
        }
    }
}
