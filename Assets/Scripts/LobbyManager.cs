using System.Collections.Generic;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager instance;

    [SerializeField] TMP_InputField lobbyCodeTextField;
    [SerializeField] TMP_InputField playerNameTextField;
    [SerializeField] Transform startGameButtonTransform;

    float heartBeatTimer;
    float lobbyUpdateTimer;

    Lobby hostLobby;
    Lobby joinedLobby;

    string playerName;

    readonly string keyGameMode = "GameMode";
    readonly string keyMap = "Map";
    readonly string keyPlayerName = "PlayerName";
    readonly string keyStartGameRelayCode = "StartGameRelayCode";

    void Awake()
    {
        instance = this;
    }

    async void Start()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId);
        };

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        playerName = "Player" + Random.Range(10, 99);
        Debug.Log("Player Name: " + playerName);
    }

    void Update()
    {
        HandleLobbyHeartBeat();
        HandleLobbyPollForUpdates();
    }

    // ---------------------------------------------------------
    // HEARTBEAT
    // ---------------------------------------------------------
    async void HandleLobbyHeartBeat()
    {
        if (hostLobby != null)
        {
            heartBeatTimer -= Time.deltaTime;

            if (heartBeatTimer < 0f)
            {
                heartBeatTimer = 15f;
                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            }
        }
    }

    // ---------------------------------------------------------
    // POLL LOBBY UPDATES
    // ---------------------------------------------------------
    async void HandleLobbyPollForUpdates()
    {
        if (joinedLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;

            if (lobbyUpdateTimer < 0f)
            {
                lobbyUpdateTimer = 1.1f;

                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                joinedLobby = lobby;

                if (joinedLobby.Data[keyStartGameRelayCode].Value != "0")
                {
                    if (!IsLobbyHost())
                    {
                        RelayManager.instance.JoinRelay(joinedLobby.Data[keyStartGameRelayCode].Value);
                        Debug.Log("Joining Relay");
                    }

                    joinedLobby = null;
                }
            }
        }
    }

    // ---------------------------------------------------------
    // CREATE LOBBY
    // ---------------------------------------------------------
    public async void CreateLobby()
    {
        try
        {
            string lobbyName = "MyLobby";
            int maxPlayers = 4;

            CreateLobbyOptions options = new()
            {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    {keyGameMode, new DataObject(DataObject.VisibilityOptions.Public, "PropHunt")},
                    {keyMap, new DataObject(DataObject.VisibilityOptions.Public, "Dust1")},
                    {keyStartGameRelayCode, new DataObject(DataObject.VisibilityOptions.Member, "0")}
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            hostLobby = lobby;
            joinedLobby = hostLobby;

            startGameButtonTransform.gameObject.SetActive(true);

            Debug.Log("Lobby Created: " + lobby.Name + " | Code: " + lobby.LobbyCode);
            PrintPlayers(hostLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    // ---------------------------------------------------------
    // JOIN LOBBY
    // ---------------------------------------------------------
    public async void JoinLobbyByCode(string code)
    {
        try
        {
            JoinLobbyByCodeOptions options = new()
            {
                Player = GetPlayer()
            };

            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);
            joinedLobby = lobby;

            Debug.Log("Joined Lobby with code: " + code);
            PrintPlayers(lobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void QuickJoinLobby()
    {
        try
        {
            QuickJoinLobbyOptions options = new()
            {
                Player = GetPlayer()
            };

            Lobby lobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
            joinedLobby = lobby;

            Debug.Log("Quick Joined Lobby");
            PrintPlayers(lobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    // ---------------------------------------------------------
    // START GAME
    // ---------------------------------------------------------
    public async void StartGame()
    {
        if (IsLobbyHost())
        {
            try
            {
                Debug.Log("Start Game");

                string relayCode = await RelayManager.instance.CreateRelay();

                Lobby lobby = await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {keyStartGameRelayCode, new DataObject(DataObject.VisibilityOptions.Member, relayCode)}
                    }
                });

                joinedLobby = lobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }
    }

    // ---------------------------------------------------------
    // RESTART LOBBY AFTER GAME (AJOUTÉ)
    // ---------------------------------------------------------
    public async void RestartLobbyAfterGame()
    {
        if (joinedLobby == null)
        {
            Debug.Log("Aucun lobby actif à supprimer.");
            return;
        }

        try
        {
            // 🔥 Supprimer le lobby actuel
            await LobbyService.Instance.DeleteLobbyAsync(joinedLobby.Id);
            Debug.Log("Lobby supprimé après fin de partie.");

            hostLobby = null;
            joinedLobby = null;

            // 🔥 Créer un nouveau lobby
            string lobbyName = "NewLobby";
            int maxPlayers = 4;

            CreateLobbyOptions options = new()
            {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    {keyGameMode, new DataObject(DataObject.VisibilityOptions.Public, "PropHunt")},
                    {keyMap, new DataObject(DataObject.VisibilityOptions.Public, "Dust1")},
                    {keyStartGameRelayCode, new DataObject(DataObject.VisibilityOptions.Member, "0")}
                }
            };

            Lobby newLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            hostLobby = newLobby;
            joinedLobby = hostLobby;

            startGameButtonTransform.gameObject.SetActive(true);

            Debug.Log("✅ Nouveau lobby créé : " + newLobby.Name + " | Code: " + newLobby.LobbyCode);
            PrintPlayers(hostLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log("Erreur lors du restart du lobby : " + e);
        }
    }

    // ---------------------------------------------------------
    // UTILS
    // ---------------------------------------------------------
    Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {keyPlayerName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)}
            }
        };
    }

    void PrintPlayers(Lobby lobby)
    {
        Debug.Log("Players in Lobby: " + lobby.Name);
        foreach (Player player in lobby.Players)
        {
            Debug.Log("Player Id: " + player.Id + " | Name: " + player.Data[keyPlayerName].Value);
        }
    }

    bool IsLobbyHost()
    {
        if (hostLobby != null)
        {
            return hostLobby.HostId == AuthenticationService.Instance.PlayerId;
        }
        return false;
    }
}