using UnityEngine;

/// <summary>Small idle bob and sway so the killer looks alive. Stops when the game is paused.</summary>
public class KillerBob : MonoBehaviour
{
    public float bobHeight = 0.06f;
    public float bobSpeed = 2.2f;
    public float swayAngle = 3f;

    Vector3 basePosition;
    Quaternion baseRotation;
    float seed;

    void Start()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        seed = Random.value * 10f;
    }

    void Update()
    {
        float t = Time.time * bobSpeed + seed; // Time.time stops while timeScale is 0
        transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t) * bobHeight);
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * swayAngle);
    }
}
