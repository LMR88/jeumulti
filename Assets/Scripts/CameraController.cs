using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController instance;

    [Header("Target")]
    public Transform target;

    [Header("Camera Settings")]
    public Camera playerCam;

    [Header("Orbit Settings")]
    public float sensitivityX = 200f;
    public float sensitivityY = 120f;
    public float minYAngle = -40f;
    public float maxYAngle = 80f;
    public float height = 2f;

    [Header("Distances par rôle")]
    public float hunterDistance = 6f;
    public float propDistance = 3f;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        instance = this;

        if (playerCam == null)
            playerCam = GetComponentInChildren<Camera>();
    }

    public void LookAt(Transform newTarget)
    {
        target = newTarget;

        Vector3 dir = (transform.position - target.position).normalized;
        Quaternion rot = Quaternion.LookRotation(dir);

        Vector3 angles = rot.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * sensitivityX * Time.deltaTime;
        pitch -= mouseY * sensitivityY * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minYAngle, maxYAngle);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // ✅ Distance dynamique selon le rôle
        float finalDistance = 4f;
        var player = target.GetComponent<PlayerNetwork>();

        if (player != null)
        {
            if (player.Role == PlayerRole.Hunter)
                finalDistance = hunterDistance;
            else
                finalDistance = propDistance;
        }

        Vector3 offset = rotation * new Vector3(0, 0, -finalDistance);
        offset.y += height;

        transform.position = target.position + offset;
        transform.rotation = rotation;
    }
}