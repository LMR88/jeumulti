using Unity.Netcode;
using UnityEngine;

public class GameTimer : NetworkBehaviour
{
    public NetworkVariable<float> timeRemaining = 
        new NetworkVariable<float>(600f);

    private bool running = false;

    public override void OnNetworkSpawn()
    {
        Debug.Log("GameTimer SPAWNED on " + (IsServer ? "SERVER" : "CLIENT"));

        if (IsServer)
        {
            timeRemaining.Value = 600f;
            running = true;
        }
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (!running)
            return;

        timeRemaining.Value -= Time.deltaTime;

        if (timeRemaining.Value <= 0)
        {
            timeRemaining.Value = 0;
            running = false;
        }
    }
}
