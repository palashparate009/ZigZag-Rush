using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LeaderboardRowView : MonoBehaviour
{
    public Image background;
    public TMP_Text rankText;
    public TMP_Text playerText;
    public TMP_Text scoreText;

    public void Bind(PlayGamesLeaderboardManager.LeaderboardEntry entry, int visibleIndex)
    {
        background.color = entry.IsLocalPlayer
            ? new Color(0.95f, 0.04f, 0.48f, 1f)
            : visibleIndex % 2 == 0
                ? new Color(0.16f, 0.02f, 0.62f, 1f)
                : new Color(0.22f, 0.025f, 0.72f, 1f);
        // Google can return the local PlayerScore before assigning a public rank.
        // Do not show the SDK's sentinel rank (-1) to the player.
        rankText.text = entry.Rank > 0 ? $"#{entry.Rank}" : "--";
        playerText.text = TruncateName(entry.PlayerName) + (entry.IsLocalPlayer ? "  (YOU)" : string.Empty);
        scoreText.text = entry.FormattedScore;
    }

    private static string TruncateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "PLAYER";
        value = value.Trim();
        return value.Length <= 18 ? value : value.Substring(0, 17) + "...";
    }
}
