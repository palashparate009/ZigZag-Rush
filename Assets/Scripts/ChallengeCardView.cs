using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChallengeCardView : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text progressText;
    public Slider progressSlider;
    public Button claimButton;
    public TMP_Text claimText;
    public Image claimedTick;

    private Action<int, string> onClaim;
    private int slot;
    private string period;
    private ChallengeProgress.Entry previous;
    private bool hasPrevious;
    private Graphic[] cardGraphics;
    private Color[] originalColors;

    private void CacheOriginalColors()
    {
        if (cardGraphics != null) return;
        // Cache once: pooled cards must restore their original colours on tab changes.
        cardGraphics = GetComponentsInChildren<Graphic>(true);
        originalColors = new Color[cardGraphics.Length];
        for (int i = 0; i < cardGraphics.Length; i++)
            originalColors[i] = cardGraphics[i].color;
    }

    public void Initialize(int index, Action<int, string> callback)
    {
        slot = index;
        onClaim = callback;
        claimButton.onClick.AddListener(Claim);
    }

    private void Claim() => onClaim?.Invoke(slot, period);

    public void Bind(ChallengeProgress.Entry entry)
    {
        // Cards are prewarmed under an inactive panel, before Awake may run.
        CacheOriginalColors();
        period = entry.Period;
        if (hasPrevious && previous.Title == entry.Title && previous.Current == entry.Current &&
            previous.Target == entry.Target && previous.Reward == entry.Reward &&
            previous.Claimed == entry.Claimed && previous.Period == entry.Period) return;
        previous = entry;
        hasPrevious = true;
        titleText.text = entry.Title;
        descriptionText.text = entry.Description;
        progressSlider.SetValueWithoutNotify((float)entry.Current / entry.Target);
        progressText.text = $"{entry.Current} / {entry.Target}   -   {entry.Remaining} LEFT";
        claimText.text = entry.Claimed ? "CLAIMED" : entry.CanClaim
            ? $"CLAIM\n{entry.Reward} GEMS" : $"{entry.Reward} GEMS\nIN PROGRESS";
        claimButton.interactable = entry.CanClaim;
        if (claimedTick != null)
        {
            claimedTick.gameObject.SetActive(entry.Claimed);
            claimText.gameObject.SetActive(!entry.Claimed);
        }
        for (int i = 0; i < cardGraphics.Length; i++)
        {
            Color original = originalColors[i];
            float grey = original.grayscale * 0.75f;
            cardGraphics[i].color = entry.Claimed
                ? new Color(grey, grey, grey, original.a) : original;
        }
    }

    private void OnDestroy() => claimButton.onClick.RemoveListener(Claim);
}
