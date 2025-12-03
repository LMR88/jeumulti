using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float runMoveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private Collider _collider;
    private Transform _transformCam;
    private Camera _playerCam;
    
    private bool Run => Input.GetKey(KeyCode.LeftShift);

    private void Start()
    {
        _transformCam = CameraController.instance.transform;
        
        CameraController.instance.LookAt(transform);
    }
    
    private void FixedUpdate()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 camForward = _transformCam.forward;
        Vector3 move = verticalInput * camForward + horizontalInput * _transformCam.right;
        move.y = 0;

        Vector3 moveFinal = Vector3.ClampMagnitude(move, 1) * (Run ? runMoveSpeed : moveSpeed);
        rb.linearVelocity = new Vector3(moveFinal.x, rb.linearVelocity.y, moveFinal.z);

        if (move != Vector3.zero)
        {
            rb.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(move),
                Time.deltaTime * rotationSpeed);
        }
    }

   
}
