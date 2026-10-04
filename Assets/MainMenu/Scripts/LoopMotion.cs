using UnityEngine;

// Endless idle motion for menu mini-scenes: bobbing, swaying (door creak, crow) and hopping.
public class LoopMotion : MonoBehaviour
{
    public Vector3 bob;                 // local position amplitude
    public Vector3 swayAxis = Vector3.up;
    public float swayDegrees;           // rotation amplitude around swayAxis
    public float frequency = 0.5f;
    public float phase;
    [Tooltip("Hop: |sin| instead of sin, for small jumps.")]
    public bool hop;

    Vector3 startPos;
    Quaternion startRot;

    void Awake()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
    }

    void Update()
    {
        float s = Mathf.Sin((Time.time * frequency + phase) * Mathf.PI * 2f);
        float b = hop ? Mathf.Abs(s) : s;
        transform.localPosition = startPos + bob * b;
        transform.localRotation = startRot * Quaternion.AngleAxis(swayDegrees * s, swayAxis);
    }
}
