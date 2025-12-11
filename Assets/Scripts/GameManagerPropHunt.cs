using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private Dictionary<ulong, PlayerNetwork> players = new Dictionary<ulong, PlayerNetwork>();

    private int huntersAlive = 0;
    private int propsAlive = 0;

    private bool gameEnded = false;

    [SerializeField] private float victoryDelay = 3f; // ✅ délai avant shutdown

    private void Awake()
    {
        Instance = this;

        // ✅ Reset complet du GameManager quand on relance un lobby
        players.Clear();
        huntersAlive = 0;
        propsAlive = 0;
        gameEnded = false;
    }

    public void RegisterPlayer(PlayerNetwork player)
    {
        ulong id = player.OwnerClientId;

        if (!players.ContainsKey(id))
            players.Add(id, player);

        if (player.Role == PlayerRole.Hunter)
            huntersAlive++;

        if (player.Role == PlayerRole.Prop)
            propsAlive++;

        Debug.Log($"RegisterPlayer → Hunters:{huntersAlive} | Props:{propsAlive}");
    }

    public void PlayerDied(ulong clientId)
    {
        if (gameEnded)
            return;

        if (!players.ContainsKey(clientId))
            return;

        PlayerNetwork player = players[clientId];

        Debug.Log($"Player mort : {clientId} | Role = {player.Role}");

        // ✅ Si un hunter meurt
        if (player.Role == PlayerRole.Hunter)
        {
            huntersAlive--;
            Debug.Log("Hunter mort → restants : " + huntersAlive);

            if (huntersAlive <= 0)
            {
                Debug.Log("Tous les hunters sont morts → Victoire des props");
                EndGame();
            }

            return;
        }

        // ✅ Si un prop meurt
        if (player.Role == PlayerRole.Prop)
        {
            propsAlive--;
            Debug.Log("Prop mort → restants : " + propsAlive);

            if (propsAlive <= 0)
            {
                Debug.Log("Tous les props sont morts → Victoire des hunters");
                EndGame();
            }

            return;
        }
    }

    // ✅ Fin de partie → délai → shutdown
    public void EndGame()
    {
        if (gameEnded)
            return;

        gameEnded = true;

        Debug.Log("=== FIN DE PARTIE ===");
        Debug.Log("Victoire ! Shutdown dans " + victoryDelay + " secondes...");

        StartCoroutine(DelayedShutdown());
    }

    private IEnumerator DelayedShutdown()
    {
        // ✅ Laisser profiter de la victoire
        yield return new WaitForSeconds(victoryDelay);

        Debug.Log("Shutdown du réseau...");

        // ✅ vider la liste des joueurs pour éviter les respawn fantômes
        players.Clear();

        // ✅ reset compteurs
        huntersAlive = 0;
        propsAlive = 0;

        // ✅ shutdown propre
        NetworkManager.Singleton.Shutdown();

        Debug.Log("Réseau arrêté. Recrée un lobby pour relancer une partie.");
    }
}