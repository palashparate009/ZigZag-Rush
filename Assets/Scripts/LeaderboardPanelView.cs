using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LeaderboardPanelView : MonoBehaviour
{
    public Button dailyButton;
    public Button weeklyButton;
    public Button allTimeButton;
    public Button backButton;
    public Button previousPageButton;
    public Button nextPageButton;
    public TMP_Text pageText;
    public TMP_Text statusText;
    public ScrollRect scrollRect;
    public RectTransform content;
    public LeaderboardRowView rowPrefab;
}
