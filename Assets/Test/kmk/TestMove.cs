using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TestMove : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 7f;

    private Rigidbody _rigidbody;
    private Vector3 _movement;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.freezeRotation = true; 
    }

    private void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        _movement = new Vector3(moveX, 0f, moveZ).normalized;
    }

    private void FixedUpdate()
    {
        var nextPosition = _rigidbody.position + _movement * (moveSpeed * Time.fixedDeltaTime);
        _rigidbody.MovePosition(nextPosition);
    }
}
