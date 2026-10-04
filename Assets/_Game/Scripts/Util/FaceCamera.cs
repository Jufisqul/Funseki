using UnityEngine;

// Turns a world-space label toward the camera around the vertical axis (NPC names, grey-box tags).
public class FaceCamera : MonoBehaviour
{
    Transform cam;

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }
        Vector3 away = transform.position - cam.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away);
    }
}
