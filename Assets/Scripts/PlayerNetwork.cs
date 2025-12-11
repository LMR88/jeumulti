using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class PlayerNetwork : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float runMoveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private BoxCollider _collider;
    [SerializeField] private PropDatabase database;

    [SerializeField] private GameObject blackScreen;
    [SerializeField] private CanvasGroup hitMarker;

    private bool isFrozen = false;
    private bool isLocked = false;

    private int _id;
    private Transform _cam;
    private Camera _playerCam;

    private bool isLocalPlayer = false;

    public PlayerRole Role => _role.Value;

    private bool Run => Input.GetKey(KeyCode.LeftShift);

    private NetworkVariable<PlayerRole> _role = new NetworkVariable<PlayerRole>(
        PlayerRole.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> currentPropId =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<PlayerData> _playerData = new(
        new PlayerData { Health = 5, Stunned = false },
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        _id = -1;
        isLocalPlayer = (OwnerClientId == NetworkManager.Singleton.LocalClientId);

        currentPropId.OnValueChanged += (_, newValue) =>
        {
            ApplyProp(newValue);
        };

        _role.OnValueChanged += (_, newValue) =>
        {
            if (isLocalPlayer)
                OnRoleAssigned(newValue);
        };

        if (isLocalPlayer)
        {
            CameraController.instance.LookAt(transform);
            _playerCam = CameraController.instance.playerCam;
            _cam = CameraController.instance.transform;

            if (blackScreen != null)
                blackScreen.SetActive(false);

            if (hitMarker != null)
                hitMarker.alpha = 0f;
        }

        if (IsServer)
        {
            if (_role.Value == PlayerRole.None)
            {
                PlayerRole role = Random.value > 0.5f ? PlayerRole.Prop : PlayerRole.Hunter;
                _role.Value = role;
            }

            GameManager.Instance.RegisterPlayer(this);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (isFrozen) return;

        if (_role.Value == PlayerRole.Prop)
        {
            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                isLocked = !isLocked;
            }
        }

        if (isLocked)
        {
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            HandleMovement();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            RequestDestroyServerRpc();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (_role.Value == PlayerRole.Hunter)
            {
                Attack();
            }
            else if (_role.Value == PlayerRole.Prop)
            {
                SelectProp();
            }
        }
    }

    private void OnRoleAssigned(PlayerRole role)
    {
        if (role == PlayerRole.Hunter)
        {
            StartCoroutine(FreezeHunterCoroutine());
        }
        else
        {
            isFrozen = false;
            if (blackScreen != null)
                blackScreen.SetActive(false);
        }
    }

    private IEnumerator FreezeHunterCoroutine()
    {
        isFrozen = true;

        if (blackScreen != null)
            blackScreen.SetActive(true);

        yield return new WaitForSeconds(20f);

        isFrozen = false;

        if (blackScreen != null)
            blackScreen.SetActive(false);
    }

    private void HandleMovement()
    {
        if (_cam == null) return;

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

    private void SelectProp()
    {
        if (_playerCam == null) return;

        Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var prop = hit.collider.gameObject;
            if (prop.CompareTag("Prop"))
            {
                string[] split = prop.name.Split('_');
                int id;
                if (split.Length > 1 && int.TryParse(split[1], out id))
                {
                    ChangePropServerRpc(id);
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangePropServerRpc(int propId)
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

    private IEnumerator ShowHitMarker()
    {
        if (hitMarker == null)
            yield break;

        hitMarker.alpha = 1f;
        yield return new WaitForSeconds(0.1f);
        hitMarker.alpha = 0f;
    }

    private void Attack()
    {
        if (_playerCam == null) return;

        Ray ray = _playerCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Collider target = hit.collider;

            if (target.CompareTag("Player"))
            {
                var enemy = target.GetComponentInParent<PlayerNetwork>();
                if (enemy != null && enemy != this)
                {
                    StartCoroutine(ShowHitMarker());
                    DamagePlayerServerRpc(enemy.OwnerClientId, 1);
                }
            }
            else if (target.CompareTag("Prop"))
            {
                StartCoroutine(ShowHitMarker());
                DamagePlayerServerRpc(OwnerClientId, 1);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DamagePlayerServerRpc(ulong targetClientId, int dmg)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId))
            return;

        var targetObj = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject;
        if (targetObj == null)
            return;

        var target = targetObj.GetComponent<PlayerNetwork>();

        var data = target._playerData.Value;
        data.Health -= dmg;
        target._playerData.Value = data;

        Debug.Log($"Dégâts appliqués à {targetClientId} → {data.Health} HP restants");

        if (data.Health <= 0)
        {
            KillPlayerServerRpc(targetClientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void KillPlayerServerRpc(ulong targetClientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId))
            return;

        var targetObj = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject;
        if (targetObj == null)
            return;

        Debug.Log("KillPlayerServerRpc → " + targetClientId);

        targetObj.Despawn();

        GameManager.Instance.PlayerDied(targetClientId);
    }

    [ServerRpc]
    private void RequestDestroyServerRpc()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetRoleServerRpc(PlayerRole role)
    {
        _role.Value = role;
    }

    // ✅ Respawn appelé par le GameManager au reset de lobby
    public void Respawn()
    {
        if (!NetworkObject.IsSpawned)
            NetworkObject.Spawn(true);

        transform.position = Vector3.zero;

        var data = _playerData.Value;
        data.Health = 5;
        data.Stunned = false;
        _playerData.Value = data;

        ChangePropServerRpc(0);
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

public enum PlayerRole
{
    None,
    Prop,
    Hunter
}