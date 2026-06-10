using UnityEngine;

public class RotateY : MonoBehaviour
{
    [Tooltip("초당 회전 속도 (도)")]
    public float rotationSpeed = 90f;

    void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
    }
}
