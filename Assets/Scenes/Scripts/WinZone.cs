using UnityEngine;

/// <summary>
/// Финиш уровня: когда герой касается этого триггера, он замирает и появляется экран
/// «YOU WIN» с кнопкой Restart (WinScreen). Сейчас висит на WallClimbing в конце тестовой карты.
/// </summary>
[DisallowMultipleComponent]
public class WinZone : MonoBehaviour
{
    [Tooltip("Большая надпись")]
    public string title = "YOU WIN";
    [Tooltip("Строка под ней")]
    public string subtitle = "[end of test map]";

    private void OnTriggerEnter2D(Collider2D other)
    {
        var move = other.GetComponentInParent<PlayerMove>();
        if (move == null || move.IsDead || move.HasWon) return;
        move.Win();
        WinScreen.Show(title, subtitle);
    }
}
