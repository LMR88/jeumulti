using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private GameTimer timer;

    private void Update()
    {
        float t = timer.timeRemaining.Value;

        int minutes = Mathf.FloorToInt(t / 60);
        int seconds = Mathf.FloorToInt(t % 60);

        text.text = $"{minutes:00}:{seconds:00}";
    }
}