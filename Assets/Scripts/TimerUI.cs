using UnityEngine;
public class TimerUIController : MonoBehaviour
{
    public CountdownTimerSynchronise countdownTimer;

    public void OnRestartButtonClicked()
    {
        countdownTimer.RestartTimerServerRpc();
    }
}