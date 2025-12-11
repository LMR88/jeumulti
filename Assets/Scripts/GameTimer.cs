using Unity.Netcode;
using UnityEngine;
using TMPro;

public class CountdownTimerSynchronise : NetworkBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private int countdownDuration = 60;

    private NetworkVariable<double> startTime = new NetworkVariable<double>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartNewTimer();
        }
    }

    void Update()
    {
        if (!IsSpawned) return;

        // Utilise ServerTime pour une référence commune
        double elapsed = NetworkManager.Singleton.ServerTime.Time - startTime.Value;
        double remaining = countdownDuration - elapsed;
        if (remaining < 0) remaining = 0;

        int minutes = Mathf.FloorToInt((float)remaining / 60f);
        int seconds = Mathf.FloorToInt((float)remaining % 60f);

        timerText.text = $"{minutes:00}:{seconds:00}";

        if (IsServer && remaining <= 0)
        {
            TimerFinishedClientRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RestartTimerServerRpc()
    {
        StartNewTimer();
    }

    private void StartNewTimer()
    {
        startTime.Value = NetworkManager.Singleton.ServerTime.Time;
    }

    [ClientRpc]
    private void TimerFinishedClientRpc()
    {
        timerText.text = "FIN DU TIMER";
    }
}