using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum PlayerRole { None, Prop, Hunter }

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    // Stocke le rôle de chaque joueur
    private Dictionary<ulong, PlayerRole> playerRoles = new Dictionary<ulong, PlayerRole>();

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerConnected;
        }
    }

    private void OnPlayerConnected(ulong clientId)
    {
        // Attribution du rôle
        PlayerRole role = Random.value > 0.5f ? PlayerRole.Prop : PlayerRole.Hunter;
        playerRoles[clientId] = role;

        AssignRoleClientRpc(clientId, role);
    }

    [ClientRpc]
    private void AssignRoleClientRpc(ulong clientId, PlayerRole role)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log("Ton rôle est : " + role);
        }
    }

    // Appelé par PlayerNetwork quand un joueur meurt
    public void PlayerDied(ulong clientId)
    {
        if (playerRoles.ContainsKey(clientId))
            playerRoles.Remove(clientId);

        CheckGameEnd();
    }

    private void CheckGameEnd()
    {
        bool propsAlive = playerRoles.Values.Any(r => r == PlayerRole.Prop);
        bool huntersAlive = playerRoles.Values.Any(r => r == PlayerRole.Hunter);

        // ✅ Si plus de Props → Hunters gagnent
        if (!propsAlive)
        {
            EndGame("Les Hunters gagnent !");
            return;
        }

        // ✅ Si plus de Hunters → Props gagnent
        if (!huntersAlive)
        {
            EndGame("Les Props gagnent !");
            return;
        }

        // ✅ Sinon la partie continue
    }

    private void EndGame(string message)
    {
        Debug.Log("FIN DE PARTIE : " + message);

        StartCoroutine(RestartLobbyCoroutine());
    }

    private System.Collections.IEnumerator RestartLobbyCoroutine()
    {
        yield return new WaitForSeconds(5f);

        // 🔥 Restart du lobby
        LobbyManager.instance.RestartLobbyAfterGame();

        // 🔥 Stop du NetworkManager
        NetworkManager.Singleton.Shutdown();
    }
}