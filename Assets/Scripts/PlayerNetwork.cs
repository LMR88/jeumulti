using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    [SerializeField] private NetworkObject prefab;
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float runMoveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private BoxCollider _collider;
    [SerializeField] private PropDatabase database;

    private int _id;
    private Transform _cam;
    private Camera _playerCam;

    private bool Run => Input.GetKey(KeyCode.LeftShift);
    private bool _hunter;

    private NetworkVariable<int> currentPropId =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // PlayerData : SEUL LE SERVEUR PEUT ÉCRIRE (meilleur choix)
    public NetworkVariable<PlayerData> _playerData = new(
        new PlayerData { Health = 5, Stunned = false },
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        _id = -1;

        _playerData.OnValueChanged += (_, newValue) =>
        {
            Debug.Log("PLAYER " + OwnerClientId + " HP = " + newValue.Health);
        };

        currentPropId.OnValueChanged += (_, newValue) =>
        {
            ApplyProp(newValue);
        };

        if (IsOwner)
        {
            CameraController.instance.LookAt(transform);
            _playerCam = CameraController.instance.playerCam;
            _cam = CameraController.instance.transform;
        }

        _hunter = IsHost; // Le host est le hunter
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();

        if (Input.GetKeyDown(KeyCode.Q))
        {
            RequestDestroyServerRpc();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (_hunter)
                Attack();
            else
                SelectProp();
        }
    }

    // ----------------------------
    //  MOVEMENT
    // ----------------------------
    private void HandleMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 camForward = _cam.forward;
        Vector3 move = verticalInput * camForward + horizontalInput * _cam.right;
        move.y = 0;

        Vector3 moveFinal = Vector3.ClampMagnitude(move, 1) * (Run ? runMoveSpeed : moveSpeed);
        rb.linearVelocity = new Vector3(moveFinal.x, rb.linearVelocity.y, moveFinal.z);

        if (move != Vector3.zero)
        {
            rb.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(move),
                Time.deltaTime * rotationSpeed);
        }
    }

    // ----------------------------
    //  PROP SELECTION
    // ----------------------------
    private void SelectProp()
    {
        Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var prop = hit.collider.gameObject;
            if (prop.CompareTag("Prop"))
            {
                string[] split = prop.name.Split('_');
                int id = int.Parse(split[1]);

                ChangePropServerRpc(id);
            }
        }
    }

    [ServerRpc]
    private void ChangePropServerRpc(int propId)
    {
        currentPropId.Value = propId;
    }

    private void ApplyProp(int id)
    {
        if (id < 0 || id >= database.meshes.Length || _id == id) return;

        _id = id;

        _meshFilter.sharedMesh = database.meshes[id];
        _meshRenderer.sharedMaterial = database.materials[id];

        _collider.size = database.colliderSizes[id];
        _collider.center = database.colliderCenter[id];
    }

    // ----------------------------
    //  ATTACK SYSTEM
    // ----------------------------
    private void Attack()
    {
        Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Collider target = hit.collider;

            if (target.CompareTag("Player"))
            {
                var enemy = target.GetComponentInParent<PlayerNetwork>();
                if (enemy != null && enemy != this)
                {
                    DamagePlayerServerRpc(enemy.OwnerClientId, 1);
                }
            }
            else if (target.CompareTag("Prop"))
            {
                DamagePlayerServerRpc(OwnerClientId, 1); // se blesse lui-même
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DamagePlayerServerRpc(ulong targetClientId, int dmg)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId)) return;

        var targetObj = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject;
        if (targetObj == null) return;

        var target = targetObj.GetComponent<PlayerNetwork>();

        var data = target._playerData.Value;
        data.Health -= dmg;
        target._playerData.Value = data;

        if (data.Health <= 0)
            KillPlayerServerRpc(targetClientId);
    }

    [ServerRpc]
    private void KillPlayerServerRpc(ulong targetClientId)
    {
        var target = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject;
        if (target != null)
            target.Despawn();
    }

    // ----------------------------
    //  DESTROY SELF
    // ----------------------------
    [ServerRpc]
    private void RequestDestroyServerRpc()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }
}


public struct PlayerData : INetworkSerializable
{
    public int Health;
    public bool Stunned;
    public FixedString32Bytes Name;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Health);
        serializer.SerializeValue(ref Stunned);
        serializer.SerializeValue(ref Name);
    }
}
