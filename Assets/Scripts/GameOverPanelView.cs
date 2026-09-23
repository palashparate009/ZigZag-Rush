using TMPro;
using UnityEngine;

public sealed class GameOverPanelView : MonoBehaviour
{
    [Header("Run result")]
    public TMP_Text scoreValue;
    public TMP_Text scoreCaption;
    public TMP_Text bestValue;
    public TMP_Text gemsValue;
    public TMP_Text comboValue;

    [Header("Personal objective")]
    public GameObject objectivePanel;
    public TMP_Text objectiveText;

    public void SetResults(int score, int best, int gems, int bestCombo, string objective)
    {
        scoreValue.text = score.ToString();
        bestValue.text = best.ToString();
        gemsValue.text = $"+{gems}";
        comboValue.text = bestCombo.ToString();

        bool isNewBest = score > 0 && score >= best;
        scoreCaption.text = isNewBest ? "NEW PERSONAL BEST" : "RUN SCORE";
        scoreCaption.color = isNewBest
            ? new Color(1f, 0.72f, 0.08f)
            : new Color(0.1f, 0.9f, 1f);

        string cleanObjective = CleanObjective(objective);
        bool hasObjective = !string.IsNullOrWhiteSpace(cleanObjective);
        objectivePanel.SetActive(hasObjective);
        if (hasObjective) objectiveText.text = cleanObjective;
    }

    private static string CleanObjective(string objective)
    {
        if (string.IsNullOrWhiteSpace(objective)) return string.Empty;

        string value = objective.Trim();
        const string prefix = "NEXT GOAL:";
        if (value.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            value = value.Substring(prefix.Length).Trim();
        return value;
    }
}
