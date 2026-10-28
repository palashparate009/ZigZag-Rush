using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using DG.Tweening;

public class RuntimeGameUI : MonoBehaviour
{
    private sealed class PopupSlot
    {
        public int PoolNumber;
        public TMP_Text Text;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 BasePosition;
        public float BaseFontSize;
    }

    private const int PopupPoolSize = 6;
    private const int MenuPlayerNameVisibleCharacters = 10;
    private const float DefaultPopupAnchorY = 0.68f;
    private const float GemStorePopupAnchorY = 0.56f;
    private GameManager manager;
    private GemStoreManager gemStore;
    [SerializeField] private RectTransform safeRoot;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject gemStorePanel;
    [SerializeField] private GameObject privacyPanel;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private GameObject challengePanel;
    [SerializeField] private GameObject deleteAccountPanel;
    [SerializeField] private LeaderboardPanelView leaderboardView;
    [SerializeField] private GameOverPanelView gameOverView;
    private GameObject loginPanel;
    private GameObject namePanel;
    private GameObject quitPanel;
    private GameObject networkPanel;
    private GameObject specialTileFtuePanel;
    private GameObject leaderboardTimeframePanel;
    private GameObject fullScreenBackdrop;
    [SerializeField] private TMP_Text menuBestText;
    [SerializeField] private TMP_Text menuGemsText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text hudBestText;
    [SerializeField] private TMP_Text gemsText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private TMP_Text skinText;
    [SerializeField] private TMP_Text themeText;
    [SerializeField] private TMP_Text storeBalanceText;
    [SerializeField] private TMP_Text storeStatusText;
    [SerializeField] private TMP_Text dailyGiftButtonText;
    [SerializeField] private TMP_Text musicButtonText;
    [SerializeField] private TMP_Text hapticsButtonText;
    [SerializeField] private TMP_Text challengeText;
    [SerializeField] private TMP_Text accountButtonText;
    [SerializeField] private TMP_Text newBestText;
    [Header("Main Menu Buttons")]
    [SerializeField] private Button openGemStoreButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button challengesButton;
    [SerializeField] private Button storeButton;
    [SerializeField] private Button garageButton;
    [SerializeField] private Button musicButton;
    [SerializeField] private Button hapticsButton;
    [SerializeField] private Button privacyButton;
    [SerializeField] private Button accountButton;
    [Header("Game Buttons")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button reviveButton;
    [SerializeField] private Button doubleButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button gameOverHomeButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseHomeButton;
    [Header("Garage Buttons")]
    [SerializeField] private Button previousSkinButton;
    [SerializeField] private Button nextSkinButton;
    [SerializeField] private Button chooseSkinButton;
    [SerializeField] private Button previousThemeButton;
    [SerializeField] private Button nextThemeButton;
    [SerializeField] private Button chooseThemeButton;
    [SerializeField] private Button shopBackButton;
    [Header("Store And Dialog Buttons")]
    [SerializeField] private Button dailyGiftButton;
    [SerializeField] private Button storeBackButton;
    [SerializeField] private Button privacyPolicyButton;
    [SerializeField] private Button privacyBackButton;
    private Button privacyChoicesButton;
    [SerializeField] private Button tutorialStartButton;
    [SerializeField] private Button challengeStartButton;
    [SerializeField] private Button challengeBackButton;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;
    [SerializeField] private Button[] challengeTabButtons = new Button[3];
    [SerializeField] private Button[] gemPackButtons = new Button[4];
    [SerializeField] private TMP_Text[] gemPackButtonTexts = new TMP_Text[4];
    [SerializeField] private Font currencyFallbackFont;
    private static TMP_FontAsset currencyFallbackFontAsset;
    private int previewSkin;
    private int previewTheme;
    private Rect lastSafeArea;
    private float bannerInsetPixels;
    private Color hudBestBaseColor;
    private Color gemsBaseColor;
    private Vector3 hudBestBaseScale;
    private Vector3 gemsBaseScale;
    private bool hudFeedbackBaselineReady;
    private Coroutine newBestRoutine;
    private Coroutine reviveCountdownRoutine;
    private Coroutine loginWaitRoutine;
    private TMP_Text loginStatusText;
    private Button loginPlayGamesButton;
    private Button loginGuestButton;
    private TMP_InputField playerNameInput;
    private TMP_Text playerNameStatusText;
    private TMP_Text menuPlayerNameText;
    private Button confirmPlayerNameButton;
    private Button quitButton;
    private Button confirmQuitButton;
    private Button cancelQuitButton;
    private Button networkRetryButton;
    private Button networkExitButton;
    private TMP_Text networkMessageText;
    [SerializeField] private Button menuLeaderboardButton;
    [SerializeField] private Button gameOverLeaderboardButton;
    private Button leaderboardDailyButton;
    private Button leaderboardWeeklyButton;
    private Button leaderboardAllTimeButton;
    private Button leaderboardCloseButton;
    private TMP_Text leaderboardStatusText;
    private Button leaderboardPreviousPageButton;
    private Button leaderboardNextPageButton;
    private TMP_Text leaderboardPageText;
    private ScrollRect leaderboardScrollRect;
    private RectTransform leaderboardContent;
    private LeaderboardRowView leaderboardRowPrefab;
    private readonly List<LeaderboardRowView> leaderboardRows = new List<LeaderboardRowView>();
    private object leaderboardPreviousPageToken;
    private object leaderboardNextPageToken;
    private int leaderboardPageNumber = 1;
    private int selectedLeaderboardTab;
    private int leaderboardLoadVersion;
    private bool leaderboardOpenedFromGameOver;
    private Button watchAdUnlockCarButton;
    private TMP_Text watchAdUnlockCarButtonText;
    private TMP_Text carOwnershipText;
    private Slider carSpeedSlider;
    private Slider carHandlingSlider;
    private TMP_Text carSpeedValueText;
    private TMP_Text carHandlingValueText;
    private RawImage garagePreviewImage;
    private CarGaragePreview garagePreview;
    private TMP_Text specialTileFtueTitle;
    private TMP_Text specialTileFtueBody;
    private CanvasGroup specialTileFtueGroup;
    private RectTransform specialTileFtueCardRect;
    private Button specialTileFtueDismissButton;
    private Vector2 specialTileFtueBasePosition;
    private float timeScaleBeforeFtue = 1f;
    private bool specialTileFtuePausedGame;
    private float timeScaleBeforeQuit = 1f;
    private bool quitConfirmationPausedGame;
    private float timeScaleBeforeNetworkPopup = 1f;
    private bool networkPopupPausedGame;
    private readonly List<PopupSlot> popupPool = new List<PopupSlot>(PopupPoolSize);
    private int nextPopupIndex;
    private int selectedChallengeTab;
    private ChallengeListView challengeList;
    private readonly ChallengeCardView[] challengeCards = new ChallengeCardView[ChallengeProgress.CardsPerTab];
    private float nextChallengeRefresh;
    private bool pendingDailyRun;
    private bool storeRewardAdInProgress;
    private bool carUnlockAdInProgress;
    private bool initialScreenPresented;

    private readonly Color card = new Color(0.08f, 0.1f, 0.18f, 0.96f);
    private readonly Color pink = new Color(1f, 0.18f, 0.57f, 1f);
    private readonly Color gold = new Color(1f, 0.72f, 0.12f, 1f);

    public static RuntimeGameUI Create(GameManager gameManager)
    {
        RuntimeGameUI existing = FindFirstObjectByType<RuntimeGameUI>();
        if (existing != null) return existing;

        RuntimeGameUI template = Resources.Load<RuntimeGameUI>("UI/ZigZagRuntimeUI");
        if (template == null)
            throw new MissingReferenceException("Missing Resources/UI/ZigZagRuntimeUI prefab.");

        RuntimeGameUI ui = Instantiate(template);
        ui.name = "ZigZag Runtime UI";
        ui.Initialize(gameManager);
        return ui;
    }

    private void Initialize(GameManager gameManager)
    {
        manager = gameManager;
        CloudSaveManager.ProgressSynchronized -= HandleCloudProgressSynchronized;
        CloudSaveManager.ProgressSynchronized += HandleCloudProgressSynchronized;
        NetworkConnectivityMonitor.ConnectivityChanged -= HandleConnectivityChanged;
        NetworkConnectivityMonitor.ConnectivityChanged += HandleConnectivityChanged;

        InitializeLeaderboardReferences();
        if (gameOverView == null && gameOverPanel != null)
            gameOverView = gameOverPanel.GetComponent<GameOverPanelView>();
        ValidatePrefabReferences();
        CaptureHudFeedbackBaseline();
        ConfigureCurrencyGlyphFallback();
        EnsureFullScreenBackdrop();
        EnsureMenuPlayerName();
        BuildQuitControls();
        BuildNetworkControls();
        BuildPrivacyControls();
        ApplySafeArea();
        HideAll();
        BindButtons();
        if (!NetworkConnectivityMonitor.IsConnected) ShowNetworkPopup();
        NetworkConnectivityMonitor.CheckNow(HandleConnectivityChanged);
        StartCoroutine(CompleteDeferredInitialization());
    }

    private IEnumerator CompleteDeferredInitialization()
    {
        // Let the first menu/login frame render before preparing non-critical UI.
        yield return null;
        BuildPopupPool();
        yield return null;
        ConfigureRequiredServices();
    }

    private void ValidatePrefabReferences()
    {
        if (safeRoot == null || menuPanel == null || hudPanel == null || gameOverPanel == null ||
            pausePanel == null || shopPanel == null || gemStorePanel == null || privacyPanel == null ||
            tutorialPanel == null || challengePanel == null || deleteAccountPanel == null ||
            leaderboardView == null || gameOverView == null || menuLeaderboardButton == null ||
            gameOverLeaderboardButton == null ||
            openGemStoreButton == null || playButton == null || challengesButton == null ||
            storeButton == null || garageButton == null || musicButton == null || hapticsButton == null ||
            privacyButton == null || accountButton == null || pauseButton == null || reviveButton == null ||
            doubleButton == null || retryButton == null || gameOverHomeButton == null || resumeButton == null ||
            pauseHomeButton == null || previousSkinButton == null || nextSkinButton == null ||
            chooseSkinButton == null || previousThemeButton == null || nextThemeButton == null ||
            chooseThemeButton == null || shopBackButton == null || dailyGiftButton == null ||
            storeBackButton == null || privacyPolicyButton == null || privacyBackButton == null ||
            tutorialStartButton == null || challengeStartButton == null || challengeBackButton == null ||
            confirmDeleteButton == null || cancelDeleteButton == null ||
            challengeTabButtons == null || challengeTabButtons.Length != 3 ||
            gemPackButtons == null || gemPackButtons.Length != GemStoreManager.ProductIds.Length ||
            gemPackButtonTexts == null || gemPackButtonTexts.Length != GemStoreManager.ProductIds.Length)
            throw new MissingReferenceException("ZigZagRuntimeUI prefab has missing serialized references.");
    }

    private void ConfigureCurrencyGlyphFallback()
    {
        if (currencyFallbackFont == null)
        {
            Debug.LogWarning("Currency fallback font is missing. Store currency symbols may not render.");
            return;
        }

        if (currencyFallbackFontAsset == null)
        {
            currencyFallbackFontAsset = TMP_FontAsset.CreateFontAsset(
                currencyFallbackFont, 64, 5, GlyphRenderMode.SDFAA, 256, 256,
                AtlasPopulationMode.Dynamic, false);
            if (currencyFallbackFontAsset == null)
            {
                Debug.LogWarning("Could not create the store currency fallback font asset.");
                return;
            }

            currencyFallbackFontAsset.name = "Store Currency Fallback (Runtime)";
            if (!currencyFallbackFontAsset.TryAddCharacters("\u20B9"))
                Debug.LogWarning("The store currency fallback font could not render the Indian rupee symbol.");
        }

        foreach (TMP_Text priceText in gemPackButtonTexts)
        {
            if (priceText == null || priceText.font == null) continue;
            List<TMP_FontAsset> fallbacks = priceText.font.fallbackFontAssetTable;
            if (!fallbacks.Contains(currencyFallbackFontAsset))
                fallbacks.Add(currencyFallbackFontAsset);
        }
    }

    private void BindButtons()
    {
        BindButton(openGemStoreButton, menuPanel.name, OpenGemStore);
        BindButton(playButton, menuPanel.name, () => RequestStart(false));
        BindButton(challengesButton, menuPanel.name, OpenChallenges);
        BindButton(storeButton, menuPanel.name, OpenGemStore);
        BindButton(garageButton, menuPanel.name, OpenShop);
        BindButton(musicButton, menuPanel.name, manager.ToggleMusic);
        BindButton(hapticsButton, menuPanel.name, manager.ToggleHaptics);
        BindButton(privacyButton, menuPanel.name, ShowPrivacySettings);
        BindButton(accountButton, menuPanel.name, HandleAccountButton);
        BindButton(menuLeaderboardButton, menuPanel.name, ShowLeaderboardChoices);

        BindButton(pauseButton, hudPanel.name, manager.TogglePause);
        BindButton(reviveButton, gameOverPanel.name, manager.RequestRevive);
        BindButton(doubleButton, gameOverPanel.name, manager.RequestDoubleGems);
        BindButton(retryButton, gameOverPanel.name, manager.RestartRun);
        BindButton(gameOverHomeButton, gameOverPanel.name, manager.ReturnHome);
        BindButton(gameOverLeaderboardButton, gameOverPanel.name, ShowLeaderboardChoices);
        BindButton(leaderboardDailyButton, leaderboardTimeframePanel.name, () => SetLeaderboardTab(0));
        BindButton(leaderboardWeeklyButton, leaderboardTimeframePanel.name, () => SetLeaderboardTab(1));
        BindButton(leaderboardAllTimeButton, leaderboardTimeframePanel.name, () => SetLeaderboardTab(2));
        BindButton(leaderboardPreviousPageButton, leaderboardTimeframePanel.name, ShowPreviousLeaderboardPage);
        BindButton(leaderboardNextPageButton, leaderboardTimeframePanel.name, ShowNextLeaderboardPage);
        BindButton(leaderboardCloseButton, leaderboardTimeframePanel.name, CloseLeaderboardChoices);
        BindButton(resumeButton, pausePanel.name, manager.TogglePause);
        BindButton(pauseHomeButton, pausePanel.name, manager.ReturnHome);

        BindButton(previousSkinButton, shopPanel.name, () => BrowseSkin(-1));
        BindButton(nextSkinButton, shopPanel.name, () => BrowseSkin(1));
        BindButton(chooseSkinButton, shopPanel.name, ChooseSkin);
        BindButton(previousThemeButton, shopPanel.name, () => BrowseTheme(-1));
        BindButton(nextThemeButton, shopPanel.name, () => BrowseTheme(1));
        BindButton(chooseThemeButton, shopPanel.name, ChooseTheme);
        BindButton(shopBackButton, shopPanel.name, CloseGarage);

        BindButton(dailyGiftButton, gemStorePanel.name, ClaimDailyGift);
        for (int i = 0; i < gemPackButtons.Length; i++)
        {
            int productIndex = i;
            BindButton(gemPackButtons[i], gemStorePanel.name, () => BuyGemPack(productIndex));
        }
        BindButton(storeBackButton, gemStorePanel.name, ShowMenu);

        BindButton(privacyPolicyButton, privacyPanel.name, OpenPrivacyPolicy);
        BindButton(privacyChoicesButton, privacyPanel.name, OpenAdPrivacyOptions);
        BindButton(privacyBackButton, privacyPanel.name, ShowMenu);
        BindButton(tutorialStartButton, tutorialPanel.name, CompleteTutorial);
        BindButton(challengeTabButtons[0], challengePanel.name, () => SetChallengeTab(0));
        BindButton(challengeTabButtons[1], challengePanel.name, () => SetChallengeTab(1));
        BindButton(challengeTabButtons[2], challengePanel.name, () => SetChallengeTab(2));
        BindButton(challengeStartButton, challengePanel.name, () => RequestStart(true));
        BindButton(challengeBackButton, challengePanel.name, ShowMenu);
        BindButton(confirmDeleteButton, deleteAccountPanel.name, ConfirmDeleteAccount);
        BindButton(cancelDeleteButton, deleteAccountPanel.name, ShowMenu);
    }

    private void BindButton(Button button, string screenName, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(() =>
        {
            AnimateButton((RectTransform)button.transform);
            GameAnalytics.ButtonClicked(button.gameObject.name, screenName);
            action?.Invoke();
        });
    }

    private void OnDestroy()
    {
        CloudSaveManager.ProgressSynchronized -= HandleCloudProgressSynchronized;
        NetworkConnectivityMonitor.ConnectivityChanged -= HandleConnectivityChanged;
        DOTween.Kill(gameObject);
        RestoreTimeAfterNetworkPopup();
        RestoreTimeAfterQuitConfirmation();
        RestoreTimeAfterSpecialTileTooltip();
        if (gemStore == null) return;
        gemStore.StoreChanged -= RefreshGemStore;
        gemStore.StoreMessage -= OnStoreMessage;
    }

    private void HandleCloudProgressSynchronized()
    {
        RefreshMenu();
        if (gemStorePanel != null && gemStorePanel.activeInHierarchy) RefreshGemStore();
        if (challengePanel != null && challengePanel.activeInHierarchy) RefreshChallengeCards();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea) ApplySafeArea();
        if (Input.GetKeyDown(KeyCode.Escape)) HandleAndroidBack();
        if (challengeList != null && challengePanel.activeInHierarchy &&
            Time.unscaledTime >= nextChallengeRefresh)
        {
            RefreshChallengeCards();
            nextChallengeRefresh = Time.unscaledTime + 1f;
        }
    }

    private void HandleAndroidBack()
    {
        if (networkPanel != null && networkPanel.activeSelf) return;

        if (leaderboardTimeframePanel != null && leaderboardTimeframePanel.activeSelf)
        {
            CloseLeaderboardChoices();
            return;
        }

        if (quitPanel != null && quitPanel.activeSelf)
        {
            CancelQuitConfirmation();
            return;
        }

        ShowQuitConfirmation();
    }

    private void ShowQuitConfirmation()
    {
        BuildQuitControls();
        if (quitPanel == null || quitPanel.activeSelf) return;
        timeScaleBeforeQuit = Time.timeScale;
        quitConfirmationPausedGame = true;
        Time.timeScale = 0f;
        quitPanel.SetActive(true);
        quitPanel.transform.SetAsLastSibling();
        ShowPanel(quitPanel);
        GameAnalytics.ScreenViewed("quit_confirmation");
    }

    private void CancelQuitConfirmation()
    {
        if (quitPanel != null)
        {
            DOTween.Kill(quitPanel, false);
            quitPanel.SetActive(false);
        }
        RestoreTimeAfterQuitConfirmation();
        RefreshBannerForVisibleScreen();
    }

    private void QuitGame()
    {
        GameProgress.Save();
        RestoreTimeAfterQuitConfirmation();
        Application.Quit();
    }

    private void RestoreTimeAfterQuitConfirmation()
    {
        if (!quitConfirmationPausedGame) return;
        quitConfirmationPausedGame = false;
        Time.timeScale = timeScaleBeforeQuit;
    }

    public void ShowMenu()
    {
        CloudSaveManager.MigrateLegacyLoginChoice();
        if (CloudSaveManager.ShouldShowLoginScreen)
        {
            ShowLoginPage();
            return;
        }
        if (!CloudSaveManager.HasPlayerName)
        {
            ShowNamePage();
            return;
        }
        HideAll();
        ShowPanel(menuPanel);
        RefreshMenu();
        GameAnalytics.ScreenViewed("main_menu");
    }

    public void ShowHud()
    {
        HideAll();
        ResetHudFeedbackAnimations();
        ShowPanel(hudPanel);
        GameAnalytics.ScreenViewed("gameplay");
    }

    public void UpdateHud(int score, int gems, int combo, bool shield)
    {
        if (scoreText == null) return;
        scoreText.text = score.ToString();
        gemsText.text = $"GEMS :{gems}";
        hudBestText.text = $"BEST :{Mathf.Max(GameProgress.BestScore, score)}";
        comboText.text = combo > 1 ? $"COMBO {combo}" : shield ? "SHIELD READY" : string.Empty;
        comboText.color = shield && combo <= 1 ? new Color(0.25f, 1f, 0.45f) : gold;
        if (newBestText != null && newBestText.gameObject.activeSelf)
            newBestText.text = $"NEW BEST  {score}";
    }

    public void ShowGameOver(int score, int best, int gems, int bestCombo, bool canRevive, bool canDouble,
        string objective, bool leaderboardConfigured)
    {
        ClearGameplayFeedbackForGameOver();
        HideAll();
        gameOverView.SetResults(score, best, gems, bestCombo, objective);
        reviveButton.gameObject.SetActive(canRevive);
        doubleButton.gameObject.SetActive(canDouble && gems > 0);
        if (gameOverLeaderboardButton != null) gameOverLeaderboardButton.gameObject.SetActive(leaderboardConfigured);
        ShowPanel(gameOverPanel);
        GameAnalytics.ScreenViewed("game_over");
    }

    public void ShowPause(bool show)
    {
        if (show) ShowPanel(pausePanel);
        else
        {
            pausePanel.SetActive(false);
            RefreshBannerForVisibleScreen();
        }
        if (show) hudPanel.SetActive(true);
        if (show) GameAnalytics.ScreenViewed("pause");
    }

    public void ShowToast(string message)
    {
        ShowPopup(message);
    }

    public void ShowSpecialTileToast(string message)
    {
        SpecialTileVisualConfig visuals = ConfigManager.Current.gameplay.specialTileVisuals;
        ShowPopup(message, visuals != null ? visuals.ResolveActivationTextColor() : Color.black);
    }

    public void ShowNewHighScore(int score)
    {
        AnimateHudBestScore();
        if (newBestText == null) return;
        if (newBestRoutine != null) StopCoroutine(newBestRoutine);
        newBestRoutine = StartCoroutine(NewBestRoutine(score));
    }

    public void AnimateHudGems(int amount)
    {
        if (amount <= 0 || gemsText == null || !gemsText.gameObject.activeInHierarchy) return;
        EnsureHudFeedbackBaseline();

        RectTransform rect = gemsText.rectTransform;
        rect.DOKill(false);
        gemsText.DOKill(false);
        rect.localScale = gemsBaseScale;
        gemsText.color = gemsBaseColor;

        float scaleBoost = Mathf.Clamp(1.2f + amount * 0.025f, 1.2f, 1.38f);
        Color gemFlash = new Color(.05f, .92f, 1f, 1f);
        Sequence sequence = DOTween.Sequence()
            .SetTarget(gemsText.gameObject)
            .SetLink(gemsText.gameObject);
        sequence.Append(rect.DOScale(gemsBaseScale * scaleBoost, .14f).SetEase(Ease.OutBack));
        sequence.Join(gemsText.DOColor(gemFlash, .08f));
        sequence.Append(rect.DOScale(gemsBaseScale, .2f).SetEase(Ease.OutCubic));
        sequence.Join(gemsText.DOColor(gemsBaseColor, .2f));
    }

    public bool TryGetGemHudScreenPosition(out Vector2 screenPosition)
    {
        screenPosition = default;
        if (gemsText == null || !gemsText.gameObject.activeInHierarchy) return false;

        Canvas canvas = gemsText.canvas;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        screenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, gemsText.rectTransform.position);
        return true;
    }

    private void AnimateHudBestScore()
    {
        if (hudBestText == null || !hudBestText.gameObject.activeInHierarchy) return;
        EnsureHudFeedbackBaseline();

        RectTransform rect = hudBestText.rectTransform;
        rect.DOKill(false);
        hudBestText.DOKill(false);
        rect.localScale = hudBestBaseScale;
        hudBestText.color = hudBestBaseColor;

        Sequence sequence = DOTween.Sequence()
            .SetTarget(hudBestText.gameObject)
            .SetLink(hudBestText.gameObject);
        sequence.Append(rect.DOScale(hudBestBaseScale * 1.34f, .18f).SetEase(Ease.OutBack));
        sequence.Join(hudBestText.DOColor(pink, .1f));
        sequence.Append(rect.DOScale(hudBestBaseScale * 1.08f, .14f).SetEase(Ease.InOutSine));
        sequence.Append(rect.DOScale(hudBestBaseScale * 1.24f, .13f).SetEase(Ease.OutBack));
        sequence.Append(rect.DOScale(hudBestBaseScale, .22f).SetEase(Ease.OutCubic));
        sequence.Join(hudBestText.DOColor(hudBestBaseColor, .22f));
    }

    private void CaptureHudFeedbackBaseline()
    {
        hudBestBaseColor = hudBestText.color;
        gemsBaseColor = gemsText.color;
        hudBestBaseScale = hudBestText.rectTransform.localScale;
        gemsBaseScale = gemsText.rectTransform.localScale;
        hudFeedbackBaselineReady = true;
    }

    private void EnsureHudFeedbackBaseline()
    {
        if (!hudFeedbackBaselineReady) CaptureHudFeedbackBaseline();
    }

    private void ResetHudFeedbackAnimations()
    {
        EnsureHudFeedbackBaseline();
        hudBestText.rectTransform.DOKill(false);
        gemsText.rectTransform.DOKill(false);
        hudBestText.DOKill(false);
        gemsText.DOKill(false);
        hudBestText.rectTransform.localScale = hudBestBaseScale;
        gemsText.rectTransform.localScale = gemsBaseScale;
        hudBestText.color = hudBestBaseColor;
        gemsText.color = gemsBaseColor;
    }

    public void PlayReviveCountdown(Action ready)
    {
        if (reviveCountdownRoutine != null) StopCoroutine(reviveCountdownRoutine);
        ClearPopups();
        reviveCountdownRoutine = StartCoroutine(ReviveCountdownRoutine(ready));
    }

    public bool TryShowSpecialTileTooltip(SpecialTileType type)
    {
        if (type == SpecialTileType.Normal || GameProgress.HasSeenSpecialTileFtue(type))
            return false;
        BuildSpecialTileFtuePanel();
        if (specialTileFtuePanel.activeSelf) return false;

        ConfigureSpecialTileFtue(type);
        GameProgress.MarkSpecialTileFtueSeen(type);
        timeScaleBeforeFtue = Time.timeScale;
        specialTileFtuePausedGame = true;
        Time.timeScale = 0f;

        specialTileFtuePanel.SetActive(true);
        specialTileFtuePanel.transform.SetAsLastSibling();
        DOTween.Kill(specialTileFtuePanel, false);
        specialTileFtueCardRect.DOKill(false);
        specialTileFtueGroup.DOKill(false);
        specialTileFtueCardRect.anchoredPosition = specialTileFtueBasePosition + Vector2.right * 70f;
        specialTileFtueCardRect.localScale = Vector3.one * 0.92f;
        specialTileFtueGroup.alpha = 0f;

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(specialTileFtuePanel)
            .SetRecyclable(true)
            .SetLink(specialTileFtuePanel);
        sequence.Append(specialTileFtueGroup.DOFade(1f, 0.18f));
        sequence.Join(specialTileFtueCardRect.DOAnchorPos(specialTileFtueBasePosition, 0.32f).SetEase(Ease.OutCubic));
        sequence.Join(specialTileFtueCardRect.DOScale(1f, 0.32f).SetEase(Ease.OutBack));

        GameAnalytics.ScreenViewed($"ftue_{type.ToString().ToLowerInvariant()}");
        return true;
    }

    private void BuildPopupPool()
    {
        // These slots are shared feedback toasts, not six different popup panels.
        // Edit their source in ZigZagRuntimeUI.prefab / Feedback Toast Template.
        toastText.gameObject.SetActive(false);
        for (int i = 0; i < PopupPoolSize; i++)
        {
            TMP_Text popupText = Instantiate(toastText, toastText.transform.parent);
            popupText.gameObject.name = $"Feedback Toast - Unused [Pool {i + 1}]";
            popupText.gameObject.SetActive(false);
            CanvasGroup group = popupText.GetComponent<CanvasGroup>();
            if (group == null) group = popupText.gameObject.AddComponent<CanvasGroup>();
            RectTransform rect = (RectTransform)popupText.transform;
            popupPool.Add(new PopupSlot
            {
                PoolNumber = i + 1,
                Text = popupText,
                Rect = rect,
                Group = group,
                BasePosition = rect.anchoredPosition,
                BaseFontSize = popupText.fontSize
            });
        }
    }

    private void ShowPopup(string message, Color? colourOverride = null)
    {
        if (string.IsNullOrWhiteSpace(message) || popupPool.Count == 0) return;
        PopupSlot slot = null;
        for (int i = 0; i < popupPool.Count; i++)
        {
            int index = (nextPopupIndex + i) % popupPool.Count;
            if (popupPool[index].Text.gameObject.activeSelf) continue;
            slot = popupPool[index];
            nextPopupIndex = (index + 1) % popupPool.Count;
            break;
        }
        if (slot == null)
        {
            slot = popupPool[nextPopupIndex];
            nextPopupIndex = (nextPopupIndex + 1) % popupPool.Count;
        }

        string upper = message.ToUpperInvariant();
        bool perfect = upper.Contains("PERFECT");
        bool warning = upper.Contains("NOT ") || upper.Contains("FAILED") || upper.Contains("COULD NOT") || upper.Contains("ERROR");
        bool reward = !warning && (upper.Contains("GEM") || upper.Contains("+") || upper.Contains("COMPLETE") ||
                                   upper.Contains("ACHIEVEMENT") || upper.Contains("SELECTED") || upper.Contains("READY") ||
                                   upper.Contains("SAVED"));

        DOTween.Kill(slot.Text.gameObject, false);
        // Keep the last purpose visible in the Hierarchy, even while pooled/inactive.
        string purpose = perfect ? "Perfect Feedback" : warning ? "Warning Toast" : reward ? "Reward Toast" : "Status Toast";
        string hierarchyMessage = message.Replace('\n', ' ').Replace('\r', ' ');
        if (hierarchyMessage.Length > 80) hierarchyMessage = hierarchyMessage.Substring(0, 80) + "...";
        slot.Text.gameObject.name = $"{purpose} - {hierarchyMessage} [Pool {slot.PoolNumber}]";
        slot.Text.gameObject.SetActive(true);
        slot.Text.text = message;
        slot.Text.fontSize = perfect ? slot.BaseFontSize * 1.15f : slot.BaseFontSize;
        slot.Text.color = colourOverride ?? (warning ? pink : perfect || reward ? gold : Color.white);
        slot.Group.alpha = 0f;
        float popupAnchorY = gemStorePanel != null && gemStorePanel.activeInHierarchy
            ? GemStorePopupAnchorY
            : DefaultPopupAnchorY;
        slot.Rect.anchorMin = new Vector2(0.5f, popupAnchorY);
        slot.Rect.anchorMax = new Vector2(0.5f, popupAnchorY);
        slot.Rect.anchoredPosition = slot.BasePosition + new Vector2(UnityEngine.Random.Range(-24f, 24f), 0f);
        slot.Rect.localRotation = Quaternion.identity;
        slot.Rect.localScale = Vector3.one * (perfect ? 0.2f : 0.62f);

        float hold = perfect ? 0.45f : reward ? 0.9f : 0.7f;
        float targetScale = perfect ? 1.25f : reward ? 1.08f : 1f;
        Sequence popup = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(slot.Text.gameObject)
            .SetRecyclable(true)
            .SetLink(slot.Text.gameObject);
        popup.Append(slot.Group.DOFade(1f, 0.1f));
        popup.Join(slot.Rect.DOScale(targetScale, perfect ? 0.26f : 0.2f).SetEase(Ease.OutBack));
        if (perfect)
            popup.Join(slot.Rect.DOPunchRotation(new Vector3(0f, 0f, 9f), 0.3f, 7, 0.5f));
        else if (reward)
            popup.Append(slot.Rect.DOPunchScale(Vector3.one * 0.08f, 0.18f, 5, 0.6f));
        popup.AppendInterval(hold);
        popup.Append(slot.Rect.DOAnchorPosY(slot.Rect.anchoredPosition.y + (perfect ? 105f : 75f), 0.24f).SetEase(Ease.InQuad));
        popup.Join(slot.Group.DOFade(0f, 0.22f));
        popup.OnComplete(() => slot.Text.gameObject.SetActive(false));
    }

    private void ClearPopups()
    {
        foreach (PopupSlot slot in popupPool)
        {
            DOTween.Kill(slot.Text.gameObject, false);
            slot.Text.gameObject.SetActive(false);
        }
    }

    private void ClearGameplayFeedbackForGameOver()
    {
        ClearPopups();

        if (toastText != null)
        {
            DOTween.Kill(toastText.gameObject, false);
            toastText.DOKill(false);
            toastText.gameObject.SetActive(false);
        }

        if (newBestRoutine != null)
        {
            StopCoroutine(newBestRoutine);
            newBestRoutine = null;
        }

        if (newBestText != null)
        {
            DOTween.Kill(newBestText.gameObject, false);
            newBestText.DOKill(false);
            newBestText.gameObject.SetActive(false);
        }

        if (comboText != null) comboText.text = string.Empty;
    }

    public void RefreshSettings()
    {
        if (musicButtonText != null) musicButtonText.text = GameProgress.MusicEnabled ? "MUSIC: ON" : "MUSIC: OFF";
        if (hapticsButtonText != null) hapticsButtonText.text = GameProgress.HapticsEnabled ? "haptics: on" : "haptics: off";
    }

    private void BuildLoginPanel()
    {
        if (loginPanel != null) return;
        LoginPanelView view = InstantiateRuntimePart<LoginPanelView>("LoginPanel", safeRoot);
        loginPanel = view.gameObject;
        loginPlayGamesButton = view.playGamesButton;
        loginGuestButton = view.guestButton;
        loginStatusText = view.statusText;
        BindButton(loginPlayGamesButton, loginPanel.name, StartPlayGamesLogin);
        BindButton(loginGuestButton, loginPanel.name, ContinueAsGuest);
        loginPanel.SetActive(false);
    }

    private void BuildNamePanel()
    {
        if (namePanel != null) return;
        NamePanelView view = InstantiateRuntimePart<NamePanelView>("NamePanel", safeRoot);
        namePanel = view.gameObject;
        playerNameInput = view.playerNameInput;
        playerNameStatusText = view.statusText;
        confirmPlayerNameButton = view.confirmButton;
        BindButton(confirmPlayerNameButton, namePanel.name, SubmitPlayerName);
        EnsureMenuPlayerName();
        namePanel.SetActive(false);
    }

    private void EnsureMenuPlayerName()
    {
        if (menuPlayerNameText != null) return;
        menuPlayerNameText = InstantiateRuntimePart<TMP_Text>("LobbyPlayerName", menuPanel.transform);
        menuPlayerNameText.enableAutoSizing = false;
        menuPlayerNameText.fontSize = 27f;
        menuPlayerNameText.overflowMode = TextOverflowModes.Ellipsis;
        menuPlayerNameText.enableWordWrapping = false;
        menuPlayerNameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 260f);
    }

    private void BuildQuitControls()
    {
        if (quitPanel != null) return;
        quitButton = InstantiateRuntimePart<Button>("QuitButton", menuPanel.transform);
        QuitPanelView view = InstantiateRuntimePart<QuitPanelView>("QuitPanel", safeRoot);
        quitPanel = view.gameObject;
        confirmQuitButton = view.confirmButton;
        cancelQuitButton = view.cancelButton;
        BindButton(quitButton, menuPanel.name, ShowQuitConfirmation);
        BindButton(confirmQuitButton, quitPanel.name, QuitGame);
        BindButton(cancelQuitButton, quitPanel.name, CancelQuitConfirmation);
        quitPanel.SetActive(false);
    }

    private void BuildNetworkControls()
    {
        if (networkPanel != null) return;
        QuitPanelView view = InstantiateRuntimePart<QuitPanelView>("QuitPanel", safeRoot);
        networkPanel = view.gameObject;
        networkPanel.name = "Network Connectivity Panel";
        networkExitButton = view.confirmButton;
        networkRetryButton = view.cancelButton;

        TMP_Text title = FindText(networkPanel, "Quit Title");
        networkMessageText = FindText(networkPanel, "Quit Message");
        TMP_Text exitLabel = networkExitButton.GetComponentInChildren<TMP_Text>(true);
        TMP_Text retryLabel = networkRetryButton.GetComponentInChildren<TMP_Text>(true);
        if (title != null) title.text = "NO INTERNET";
        if (exitLabel != null) exitLabel.text = "EXIT";
        if (retryLabel != null) retryLabel.text = "RETRY";

        BindButton(networkRetryButton, networkPanel.name, RetryNetworkConnection);
        BindButton(networkExitButton, networkPanel.name, ExitForNetworkFailure);
        networkPanel.SetActive(false);
    }

    private static TMP_Text FindText(GameObject panel, string objectName)
    {
        if (panel == null) return null;
        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            if (text.name == objectName) return text;
        return null;
    }

    private void HandleConnectivityChanged(bool connected)
    {
        if (this == null) return;
        if (connected) HideNetworkPopup();
        else ShowNetworkPopup();
    }

    private void ShowNetworkPopup()
    {
        BuildNetworkControls();
        if (networkPanel == null) return;

        if (!networkPanel.activeSelf)
        {
            timeScaleBeforeNetworkPopup = Time.timeScale;
            networkPopupPausedGame = true;
            Time.timeScale = 0f;
            GameAnalytics.ScreenViewed("network_connection_required");
        }

        if (networkMessageText != null)
            networkMessageText.text = "CHECK YOUR INTERNET CONNECTION,\nTHEN TAP RETRY.";
        if (networkRetryButton != null) networkRetryButton.interactable = !NetworkConnectivityMonitor.IsChecking;
        if (!networkPanel.activeSelf)
        {
            networkPanel.SetActive(true);
            networkPanel.transform.SetAsLastSibling();
            ShowPanel(networkPanel);
        }
        else
        {
            networkPanel.transform.SetAsLastSibling();
            manager.SetBannerVisible(false);
        }
    }

    private void RetryNetworkConnection()
    {
        if (networkRetryButton != null) networkRetryButton.interactable = false;
        if (networkMessageText != null) networkMessageText.text = "CHECKING CONNECTION...";
        NetworkConnectivityMonitor.CheckNow(connected =>
        {
            if (this == null) return;
            if (connected)
            {
                HideNetworkPopup();
                return;
            }

            if (networkMessageText != null)
                networkMessageText.text = "STILL OFFLINE. CHECK WI-FI OR MOBILE DATA,\nTHEN RETRY.";
            if (networkRetryButton != null) networkRetryButton.interactable = true;
        });
    }

    private void HideNetworkPopup()
    {
        if (networkPanel == null || !networkPanel.activeSelf) return;
        DOTween.Kill(networkPanel, false);
        networkPanel.SetActive(false);
        RestoreTimeAfterNetworkPopup();
        RefreshBannerForVisibleScreen();
    }

    private void ExitForNetworkFailure()
    {
        GameProgress.Save();
        RestoreTimeAfterNetworkPopup();
        Application.Quit();
    }

    private void RestoreTimeAfterNetworkPopup()
    {
        if (!networkPopupPausedGame) return;
        networkPopupPausedGame = false;
        Time.timeScale = timeScaleBeforeNetworkPopup;
    }

    private void InitializeLeaderboardReferences()
    {
        if (menuLeaderboardButton == null)
            menuLeaderboardButton = FindButton(menuPanel, "Leaderboards");
        if (gameOverLeaderboardButton == null)
            gameOverLeaderboardButton = FindButton(gameOverPanel, "View Leaderboard");
        if (leaderboardView == null && menuPanel != null)
            leaderboardView = menuPanel.GetComponentInChildren<LeaderboardPanelView>(true);
        if (leaderboardView == null) return;
        leaderboardTimeframePanel = leaderboardView.gameObject;
        leaderboardDailyButton = leaderboardView.dailyButton;
        leaderboardWeeklyButton = leaderboardView.weeklyButton;
        leaderboardAllTimeButton = leaderboardView.allTimeButton;
        leaderboardCloseButton = leaderboardView.backButton;
        leaderboardPreviousPageButton = leaderboardView.previousPageButton;
        leaderboardNextPageButton = leaderboardView.nextPageButton;
        leaderboardPageText = leaderboardView.pageText;
        leaderboardStatusText = leaderboardView.statusText;
        leaderboardScrollRect = leaderboardView.scrollRect;
        leaderboardContent = leaderboardView.content;
        leaderboardRowPrefab = leaderboardView.rowPrefab;
    }

    private static Button FindButton(GameObject panel, string objectName)
    {
        if (panel == null) return null;
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            if (button.name == objectName) return button;
        return null;
    }

    private void BuildPrivacyControls()
    {
        if (privacyChoicesButton != null) return;
        privacyChoicesButton = Instantiate(privacyPolicyButton, privacyPolicyButton.transform.parent);
        privacyChoicesButton.name = "Ad Privacy Options";
        privacyChoicesButton.onClick.RemoveAllListeners();
        TMP_Text label = privacyChoicesButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = "AD PRIVACY OPTIONS";
        SetRuntimeRect((RectTransform)privacyChoicesButton.transform, new Vector2(0.5f, 0.49f), new Vector2(719f, 85f));
    }

    private void ShowLeaderboardChoices()
    {
        if (leaderboardTimeframePanel == null) return;
        leaderboardOpenedFromGameOver = gameOverPanel.activeSelf;
        HideAll();
        menuPanel.SetActive(true);
        leaderboardTimeframePanel.transform.SetAsLastSibling();
        ShowPanel(leaderboardTimeframePanel);
        SetLeaderboardTab(0);
        GameAnalytics.ScreenViewed("leaderboards");
    }

    private void CloseLeaderboardChoices()
    {
        leaderboardLoadVersion++;
        if (leaderboardTimeframePanel != null)
        {
            DOTween.Kill(leaderboardTimeframePanel, false);
            leaderboardTimeframePanel.SetActive(false);
        }
        if (leaderboardOpenedFromGameOver)
        {
            menuPanel.SetActive(false);
            ShowPanel(gameOverPanel);
            return;
        }
        ShowMenu();
    }

    private void SetLeaderboardTab(int tab)
    {
        selectedLeaderboardTab = Mathf.Clamp(tab, 0, 2);
        Button[] tabs = { leaderboardDailyButton, leaderboardWeeklyButton, leaderboardAllTimeButton };
        for (int i = 0; i < tabs.Length; i++) SetButtonColour(tabs[i], i == selectedLeaderboardTab ? pink : card);
        leaderboardPageNumber = 1;
        leaderboardPreviousPageToken = null;
        leaderboardNextPageToken = null;
        LoadLeaderboardPage(null, leaderboardPageNumber);
    }

    private void ShowPreviousLeaderboardPage()
    {
        if (leaderboardPreviousPageToken == null) return;
        LoadLeaderboardPage(leaderboardPreviousPageToken, Mathf.Max(1, leaderboardPageNumber - 1));
    }

    private void ShowNextLeaderboardPage()
    {
        if (leaderboardNextPageToken == null) return;
        LoadLeaderboardPage(leaderboardNextPageToken, leaderboardPageNumber + 1);
    }

    private void LoadLeaderboardPage(object pageToken, int requestedPageNumber)
    {
        SetLeaderboardPagingInteractable(false, false);
        SetLeaderboardRowsVisible(0);
        leaderboardStatusText.text = "LOADING SCORES...";
        leaderboardPageText.text = $"PAGE {requestedPageNumber}";

        GooglePlayGames.BasicApi.LeaderboardTimeSpan span = selectedLeaderboardTab == 0
            ? GooglePlayGames.BasicApi.LeaderboardTimeSpan.Daily
            : selectedLeaderboardTab == 1
                ? GooglePlayGames.BasicApi.LeaderboardTimeSpan.Weekly
                : GooglePlayGames.BasicApi.LeaderboardTimeSpan.AllTime;
        int loadVersion = ++leaderboardLoadVersion;
        PlayGamesLeaderboardManager.LoadMainScores(span, pageToken, (success, page, message) =>
        {
            if (this == null || leaderboardTimeframePanel == null ||
                !leaderboardTimeframePanel.activeSelf || loadVersion != leaderboardLoadVersion) return;
            if (success)
            {
                leaderboardPageNumber = requestedPageNumber;
                leaderboardPreviousPageToken = page.PreviousPageToken;
                leaderboardNextPageToken = page.NextPageToken;
                PopulateLeaderboardRows(page.Entries);
            }
            SetLeaderboardPagingInteractable(success && leaderboardPreviousPageToken != null,
                success && leaderboardNextPageToken != null);
            leaderboardPageText.text = $"PAGE {leaderboardPageNumber}";
            leaderboardStatusText.text = string.IsNullOrWhiteSpace(message)
                ? (success ? string.Empty : "COULD NOT LOAD SCORES")
                : message;
        });
    }

    private void SetLeaderboardPagingInteractable(bool previous, bool next)
    {
        leaderboardPreviousPageButton.interactable = previous;
        leaderboardNextPageButton.interactable = next;
    }

    private void PopulateLeaderboardRows(List<PlayGamesLeaderboardManager.LeaderboardEntry> entries)
    {
        int count = entries == null ? 0 : entries.Count;
        while (leaderboardRows.Count < count)
            leaderboardRows.Add(Instantiate(leaderboardRowPrefab, leaderboardContent, false));
        SetLeaderboardRowsVisible(count);
        for (int i = 0; i < count; i++)
            leaderboardRows[i].Bind(entries[i], i);
        Canvas.ForceUpdateCanvases();
        leaderboardScrollRect.StopMovement();
        leaderboardScrollRect.verticalNormalizedPosition = 1f;
    }

    private void SetLeaderboardRowsVisible(int count)
    {
        for (int i = 0; i < leaderboardRows.Count; i++)
            leaderboardRows[i].gameObject.SetActive(i < count);
    }

    private void BuildSpecialTileFtuePanel()
    {
        if (specialTileFtuePanel != null) return;
        SpecialTileFtueView view = InstantiateRuntimePart<SpecialTileFtueView>("SpecialTileFtuePanel", safeRoot);
        specialTileFtuePanel = view.gameObject;
        specialTileFtueDismissButton = view.dismissButton;
        specialTileFtueGroup = view.canvasGroup;
        specialTileFtueCardRect = view.cardRect;
        specialTileFtueBasePosition = specialTileFtueCardRect.anchoredPosition;
        specialTileFtueTitle = view.titleText;
        specialTileFtueBody = view.bodyText;
        specialTileFtueDismissButton.onClick.AddListener(() =>
        {
            GameAnalytics.ButtonClicked("Dismiss Special Block Tooltip", specialTileFtuePanel.name);
            DismissSpecialTileTooltip();
        });
        specialTileFtuePanel.SetActive(false);
    }

    private void BuildGarageEnhancements()
    {
        if (garagePreview != null) return;
        foreach (Transform child in shopPanel.GetComponentsInChildren<Transform>(true))
            if (child.name == "Garage Title")
            {
                SetRuntimeRect((RectTransform)child, new Vector2(0.5f, 0.91f), new Vector2(760f, 110f));
                break;
            }

        SetRuntimeRect((RectTransform)previousSkinButton.transform, new Vector2(0.12f, 0.635f), new Vector2(105f, 82f));
        SetRuntimeRect((RectTransform)nextSkinButton.transform, new Vector2(0.88f, 0.635f), new Vector2(105f, 82f));
        SetRuntimeRect((RectTransform)skinText.transform, new Vector2(0.5f, 0.625f), new Vector2(720f, 92f));
        skinText.fontSize = 32f;
        skinText.enableAutoSizing = true;
        skinText.fontSizeMin = 20f;
        skinText.fontSizeMax = 34f;
        SetRuntimeRect((RectTransform)chooseSkinButton.transform, new Vector2(0.5f, 0.405f), new Vector2(620f, 72f));
        SetRuntimeRect((RectTransform)themeText.transform, new Vector2(0.5f, 0.245f), new Vector2(650f, 82f));
        SetRuntimeRect((RectTransform)previousThemeButton.transform, new Vector2(0.12f, 0.245f), new Vector2(105f, 76f));
        SetRuntimeRect((RectTransform)nextThemeButton.transform, new Vector2(0.88f, 0.245f), new Vector2(105f, 76f));
        SetRuntimeRect((RectTransform)chooseThemeButton.transform, new Vector2(0.5f, 0.175f), new Vector2(620f, 72f));

        GarageEnhancementsView view = InstantiateRuntimePart<GarageEnhancementsView>("GarageEnhancements", shopPanel.transform);
        view.transform.SetSiblingIndex(1);
        garagePreviewImage = view.previewImage;
        garagePreview = gameObject.AddComponent<CarGaragePreview>();
        garagePreview.Initialize(garagePreviewImage);
        watchAdUnlockCarButton = view.watchAdButton;
        watchAdUnlockCarButtonText = view.watchAdButtonText;
        carSpeedSlider = view.speedSlider;
        carHandlingSlider = view.handlingSlider;
        carSpeedValueText = view.speedValueText;
        carHandlingValueText = view.handlingValueText;
        carOwnershipText = view.ownershipText;
        BindButton(watchAdUnlockCarButton, shopPanel.name, WatchAdToUnlockCar);
    }

    private void DismissSpecialTileTooltip()
    {
        if (specialTileFtuePanel == null || !specialTileFtuePanel.activeSelf) return;
        DOTween.Kill(specialTileFtuePanel, false);
        specialTileFtueCardRect.DOKill(false);
        specialTileFtueGroup.DOKill(false);
        RestoreTimeAfterSpecialTileTooltip();

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(specialTileFtuePanel)
            .SetRecyclable(true)
            .SetLink(specialTileFtuePanel);
        sequence.Append(specialTileFtueGroup.DOFade(0f, 0.14f));
        sequence.Join(specialTileFtueCardRect.DOScale(0.94f, 0.14f).SetEase(Ease.InCubic));
        sequence.OnComplete(() => specialTileFtuePanel.SetActive(false));
    }

    private void RestoreTimeAfterSpecialTileTooltip()
    {
        if (!specialTileFtuePausedGame) return;
        specialTileFtuePausedGame = false;
        Time.timeScale = timeScaleBeforeFtue;
    }

    private void ConfigureSpecialTileFtue(SpecialTileType type)
    {
        specialTileFtueTitle.color = Platform.GetConfiguredColour(type);
        switch (type)
        {
            case SpecialTileType.SpeedBoost:
                specialTileFtueTitle.text = "SPEED BOOST BLOCK";
                specialTileFtueBody.text = "Speeds up your car for 3 seconds. Prepare to turn sooner.";
                break;
            case SpecialTileType.SlowMotion:
                specialTileFtueTitle.text = "SLOW-MOTION BLOCK";
                specialTileFtueBody.text = "Slows your car for 3 seconds. Use the extra time to line up turns.";
                break;
            case SpecialTileType.Shield:
                specialTileFtueTitle.text = "SHIELD BLOCK";
                specialTileFtueBody.text = "Protects you from one fall, then the shield is consumed.";
                break;
            case SpecialTileType.GemMagnet:
                specialTileFtueTitle.text = "GEM MAGNET BLOCK";
                specialTileFtueBody.text = "Instantly collects nearby gems when you drive across it.";
                break;
            case SpecialTileType.Crumbling:
                specialTileFtueTitle.text = "CRUMBLING BLOCK";
                specialTileFtueBody.text = "Collapses shortly after contact. Keep moving!";
                break;
            case SpecialTileType.Moving:
                specialTileFtueTitle.text = "MOVING BLOCK";
                specialTileFtueBody.text = "Moves up and down. Watch its motion and stay near the centre.";
                break;
            case SpecialTileType.Mystery:
                specialTileFtueTitle.text = "MYSTERY BLOCK";
                specialTileFtueBody.text = "Contains a surprise gem reward. Drive over it to reveal the prize.";
                break;
        }
    }

    private static T InstantiateRuntimePart<T>(string prefabName, Transform parent) where T : Component
    {
        string resourcePath = $"UI/RuntimeParts/{prefabName}";
        GameObject template = Resources.Load<GameObject>(resourcePath);
        if (template == null)
            throw new MissingReferenceException($"Missing runtime UI prefab at Resources/{resourcePath}.prefab");

        GameObject instance = Instantiate(template, parent, false);
        T component = instance.GetComponent<T>();
        if (component == null)
            throw new MissingComponentException($"{template.name} must contain {typeof(T).Name} on its root.");
        return component;
    }

    private static void SetRuntimeRect(RectTransform rect, Vector2 anchor, Vector2 dimensions)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = Vector2.zero;
    }

    private void ShowLoginPage()
    {
        BuildLoginPanel();
        HideAll();
        loginStatusText.text = string.Empty;
        loginPlayGamesButton.interactable = true;
        ShowPanel(loginPanel);
        GameAnalytics.ScreenViewed("account_sign_in");
        GameAnalytics.SignInStep("sign_in_screen", "shown", 0f, false);
    }

    private void ShowNamePage()
    {
        BuildNamePanel();
        HideAll();
        string existingName = CloudSaveManager.PlayerName;
        playerNameInput.text = string.IsNullOrEmpty(existingName) ? CloudSaveManager.SuggestedPlayerName : existingName;
        playerNameStatusText.text = string.Empty;
        ShowPanel(namePanel);
        playerNameInput.ActivateInputField();
        GameAnalytics.ScreenViewed("choose_player_name");
    }

    private void SubmitPlayerName()
    {
        string playerName = playerNameInput.text.Trim();
        if (playerName.Length < 3 || playerName.Length > 16)
        {
            playerNameStatusText.text = "NAME MUST BE 3-16 CHARACTERS";
            return;
        }

        for (int i = 0; i < playerName.Length; i++)
        {
            char character = playerName[i];
            if (char.IsLetterOrDigit(character) || character == ' ' || character == '_' || character == '-') continue;
            playerNameStatusText.text = "USE LETTERS, NUMBERS, SPACES, - OR _";
            return;
        }

        CloudSaveManager.SetPlayerName(playerName);
        ShowMenu();
    }

    private void StartPlayGamesLogin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (loginWaitRoutine != null) return;
        loginStatusText.text = "OPENING GOOGLE PLAY GAMES...";
        loginPlayGamesButton.interactable = false;
        CloudSaveManager.EnableCloudSave();
        PlayGamesSignInManager.RetrySignIn();
        GameAnalytics.SignInStep("sign_in_button", "play_games", 0f, false);
        loginWaitRoutine = StartCoroutine(WaitForPlayGamesLogin());
#else
        loginStatusText.text = "GOOGLE PLAY GAMES SIGN-IN IS AVAILABLE IN THE ANDROID BUILD";
#endif
    }

    private IEnumerator WaitForPlayGamesLogin()
    {
        float remaining = Mathf.Max(1f, ConfigManager.Current.network.loginTimeoutSeconds);
        while (remaining > 0f && !CloudSaveManager.IsSignedIn)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!CloudSaveManager.IsSignedIn)
        {
            loginWaitRoutine = null;
            loginPlayGamesButton.interactable = true;
            loginStatusText.text = "SIGN-IN WAS NOT COMPLETED. PLEASE TRY AGAIN.";
            GameAnalytics.SignInStep("sign_in_screen", "timeout", ConfigManager.Current.network.loginTimeoutSeconds, false);
            yield break;
        }

        CloudSaveManager.SelectPlayGamesMode();
        if (!CloudSaveManager.IsInitialSyncComplete)
        {
            loginStatusText.text = "RESTORING YOUR CLOUD PROGRESS...";
            while (remaining > 0f && !CloudSaveManager.IsInitialSyncComplete)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (!CloudSaveManager.HasPlayerName)
        {
            float profileWaitRemaining = Mathf.Max(0f, ConfigManager.Current.network.playerProfileWaitSeconds);
            do
            {
                string playGamesName = PlayGamesSignInManager.AuthenticatedPlayerName;
                if (string.IsNullOrWhiteSpace(playGamesName)) playGamesName = CloudSaveManager.SuggestedPlayerName;
                if (!string.IsNullOrWhiteSpace(playGamesName))
                {
                    CloudSaveManager.SetPlayerName(playGamesName);
                    break;
                }

                profileWaitRemaining -= Time.unscaledDeltaTime;
                if (profileWaitRemaining > 0f) yield return null;
            }
            while (profileWaitRemaining > 0f);
        }
        loginWaitRoutine = null;
        loginPlayGamesButton.interactable = true;
        loginStatusText.text = CloudSaveManager.IsInitialSyncComplete
            ? "SIGNED IN. CLOUD SAVE IS READY!"
            : "SIGNED IN. CLOUD SYNC WILL CONTINUE IN THE MENU.";
        GameAnalytics.SignInStep("sign_in_screen", "complete", 0f, false);
        yield return new WaitForSecondsRealtime(0.55f);
        if (CloudSaveManager.HasPlayerName) ShowMenu();
        else ShowNamePage();
    }

    private void ContinueAsGuest()
    {
        GameAnalytics.SignInStep("sign_in_button", "guest", 0f, false);
        CloudSaveManager.SelectGuestMode();
        if (CloudSaveManager.HasPlayerName) ShowMenu();
        else ShowNamePage();
    }

    private void OpenChallenges()
    {
        InitializeChallengeCards();
        HideAll();
        ShowPanel(challengePanel);
        SetChallengeTab(0);
        GameAnalytics.ScreenViewed("challenges");
    }

    private void SetChallengeTab(int tab)
    {
        selectedChallengeTab = Mathf.Clamp(tab, 0, 2);
        for (int i = 0; i < challengeTabButtons.Length; i++)
            SetButtonColour(challengeTabButtons[i], i == selectedChallengeTab ? pink : card);
        challengeStartButton.gameObject.SetActive(selectedChallengeTab == 0);
        RefreshChallengeCards();
        challengeList.scrollRect.StopMovement();
        Canvas.ForceUpdateCanvases();
        challengeList.scrollRect.verticalNormalizedPosition = 1f;
    }

    private void InitializeChallengeCards()
    {
        if (challengeList != null) return;
        challengeText.gameObject.SetActive(false);
        challengeList = InstantiateRuntimePart<ChallengeListView>("ChallengeList", challengePanel.transform);
        ChallengeCardView template = Resources.Load<ChallengeCardView>("UI/RuntimeParts/ChallengeCard");
        if (template == null) throw new MissingReferenceException("Missing ChallengeCard prefab.");
        for (int i = 0; i < challengeCards.Length; i++)
        {
            challengeCards[i] = Instantiate(template, challengeList.content, false);
            challengeCards[i].Initialize(i, ClaimChallenge);
        }
    }

    private void RefreshChallengeCards()
    {
        for (int i = 0; i < challengeCards.Length; i++)
            challengeCards[i].Bind(ChallengeProgress.Get(selectedChallengeTab, i));
    }

    private void ClaimChallenge(int index, string period)
    {
        if (!ChallengeProgress.TryClaim(selectedChallengeTab, index, period, out int reward))
        {
            RefreshChallengeCards();
            return;
        }
        AnimateButton((RectTransform)challengeCards[index].claimButton.transform);
        ShowToast($"+{reward} GEMS CLAIMED!");
        GameAnalytics.GameplayReward("challenge_claim", reward);
        RefreshChallengeCards();
        RefreshMenu();
    }

    private void HandleAccountButton()
    {
        if (CloudSaveManager.IsGuest || CloudSaveManager.IsCloudSaveDisabled)
        {
            ShowLoginPage();
            return;
        }

        HideAll();
        ShowPanel(deleteAccountPanel);
        GameAnalytics.ScreenViewed("delete_account_confirmation");
    }

    private void ConfirmDeleteAccount()
    {
        ShowToast("DELETING ACCOUNT...");
        CloudSaveManager.DeleteAccount((success, message) =>
        {
            ShowToast(message);
            if (success) manager.ReturnHome();
        });
    }

    private void RequestStart(bool dailyRun)
    {
        if (PlayerPrefs.GetInt("TutorialSeen", 0) == 1)
        {
            manager.StartRun(dailyRun);
            return;
        }

        pendingDailyRun = dailyRun;
        HideAll();
        ShowPanel(tutorialPanel);
        GameAnalytics.ScreenViewed("tutorial");
    }

    private void CompleteTutorial()
    {
        PlayerPrefs.SetInt("TutorialSeen", 1);
        PlayerPrefs.Save();
        GameAnalytics.TutorialCompleted();
        manager.StartRun(pendingDailyRun);
    }

    private void ConfigureRequiredServices()
    {
        if (PlayerPrefs.GetInt("AnalyticsConsent", 0) != 1)
            GameAnalytics.SetConsent(true);
        if (PlayerPrefs.GetInt("AdPersonalizationConsent", -1) != 0)
            MediatedAdsManager.Create().SubmitPrivacyChoice(false);
    }

    private void ShowPrivacySettings()
    {
        HideAll();
        ShowPanel(privacyPanel);
        GameAnalytics.ScreenViewed("privacy");
    }

    private void OpenPrivacyPolicy()
    {
        if (!AdConfiguration.PrivacyPolicyUrl.StartsWith("REPLACE_")) Application.OpenURL(AdConfiguration.PrivacyPolicyUrl);
        else ShowToast("PRIVACY URL NOT CONFIGURED");
    }

    private void OpenAdPrivacyOptions()
    {
        if (privacyChoicesButton == null) return;
        privacyChoicesButton.interactable = false;
        GoogleUmpConsent.ShowPrivacyOptions(shown =>
        {
            if (privacyChoicesButton != null) privacyChoicesButton.interactable = true;
            ShowToast(shown ? "AD PRIVACY CHOICES UPDATED" : "NO AD PRIVACY FORM IS REQUIRED FOR THIS REGION");
        });
    }

    private void OpenShop()
    {
        BuildGarageEnhancements();
        previewSkin = GameProgress.SelectedCarIndex;
        previewTheme = GameProgress.SelectedTheme;
        HideAll();
        ShowPanel(shopPanel);
        RefreshShop();
        GameAnalytics.ScreenViewed("garage");
    }

    private void CloseGarage()
    {
        manager.ApplySelectedCosmetics();
        ShowMenu();
    }

    private void OpenGemStore()
    {
        EnsureGemStore();
        HideAll();
        ShowPanel(gemStorePanel);
        RefreshGemStore();
        GameAnalytics.ScreenViewed("gem_store");
    }

    private void EnsureGemStore()
    {
        if (gemStore != null) return;
        gemStore = GemStoreManager.Create();
        gemStore.StoreChanged += RefreshGemStore;
        gemStore.StoreMessage += OnStoreMessage;
    }

    private void ClaimDailyGift()
    {
        EconomyConfig economy = ConfigManager.Current.economy;
        if (GameProgress.DailyStoreGiftAvailable)
        {
            if (GameProgress.ClaimDailyStoreGift())
            {
                GameAnalytics.GameplayReward("daily_gift", economy.dailyGiftGems);
                ShowToast($"DAILY GIFT +{economy.dailyGiftGems} GEMS");
            }
            RefreshGemStore();
            return;
        }

        if (storeRewardAdInProgress) return;
        storeRewardAdInProgress = true;
        RefreshGemStore();
        string placement = ConfigManager.Current.ads.doubleGemsPlacement;
        GameAnalytics.RewardedAdRequested(placement);
        MediatedAdsManager.Create().ShowRewarded(placement, success =>
        {
            GameAnalytics.RewardedAdResult(placement, success);
            storeRewardAdInProgress = false;
            if (success)
            {
                GameProgress.AddGems(economy.rewardedStoreGems);
                GameAnalytics.GameplayReward("rewarded_store_gems", economy.rewardedStoreGems);
                ShowToast($"REWARDED AD +{economy.rewardedStoreGems} GEMS");
            }
            else ShowToast("AD NOT READY - TRY AGAIN");
            RefreshGemStore();
        });
    }

    private void BuyGemPack(int index)
    {
        if (index < 0 || index >= GemStoreManager.ProductIds.Length) return;
        gemStore.Buy(GemStoreManager.ProductIds[index]);
    }

    private void OnStoreMessage(string message)
    {
        ShowToast(message);
        RefreshGemStore();
    }

    private void BrowseSkin(int amount)
    {
        CarCatalog catalog = CarCatalog.Load();
        int count = catalog?.Count ?? GameProgress.SkinNames.Length;
        if (count <= 0) return;
        previewSkin = (previewSkin + amount + count) % count;
        manager.PreviewCar(previewSkin);
        RefreshShop();
    }

    private void BrowseTheme(int amount)
    {
        previewTheme = (previewTheme + amount + GameProgress.ThemeNames.Length) % GameProgress.ThemeNames.Length;
        RefreshShop();
    }

    private void ChooseSkin()
    {
        CarDefinition car = CarCatalog.Load()?.Get(previewSkin);
        bool wasUnlocked = GameProgress.IsCarUnlocked(previewSkin);
        bool success = GameProgress.UnlockOrSelectCar(previewSkin);
        string carName = car?.displayName ?? GameProgress.SkinNames[Mathf.Clamp(previewSkin, 0, GameProgress.SkinNames.Length - 1)];
        int cost = car?.gemCost ?? GameProgress.SkinCosts[Mathf.Clamp(previewSkin, 0, GameProgress.SkinCosts.Length - 1)];
        GameAnalytics.CosmeticResult("car", carName,
            success ? (wasUnlocked ? "selected" : "unlocked") : "insufficient_gems",
            wasUnlocked ? 0 : cost);
        if (!success) ShowToast("NOT ENOUGH GEMS");
        else
        {
            manager.ApplySelectedCosmetics();
            garagePreview?.Show(car);
            ShowToast(wasUnlocked ? "CAR SELECTED" : "CAR UNLOCKED");
        }
        RefreshShop();
    }

    private void WatchAdToUnlockCar()
    {
        if (carUnlockAdInProgress) return;
        CarDefinition car = CarCatalog.Load()?.Get(previewSkin);
        if (car == null || !car.rewardedAdUnlock) return;
        if (GameProgress.IsCarUnlocked(previewSkin))
        {
            ChooseSkin();
            return;
        }

        int requestedCar = previewSkin;
        string requestedCarId = car.id;
        carUnlockAdInProgress = true;
        RefreshShop();
        string placement = ConfigManager.Current.ads.carUnlockPlacement;
        GameAnalytics.RewardedAdRequested(placement);
        MediatedAdsManager.Create().ShowRewarded(placement, success =>
        {
            carUnlockAdInProgress = false;
            GameAnalytics.RewardedAdResult(placement, success);
            CarDefinition requested = CarCatalog.Load()?.Get(requestedCar);
            if (success && requested != null && string.Equals(requested.id, requestedCarId, StringComparison.Ordinal) &&
                GameProgress.UnlockCarFromRewardedAd(requestedCar))
            {
                manager.ApplySelectedCosmetics();
                garagePreview?.Show(requested);
                ShowToast("CAR UNLOCKED BY REWARDED AD");
            }
            else if (!success) ShowToast("AD NOT READY - TRY AGAIN");
            RefreshShop();
        });
    }

    private void ChooseTheme()
    {
        bool wasUnlocked = GameProgress.IsThemeUnlocked(previewTheme);
        bool success = GameProgress.UnlockOrSelectTheme(previewTheme);
        GameAnalytics.CosmeticResult("theme", GameProgress.ThemeNames[previewTheme],
            success ? (wasUnlocked ? "selected" : "unlocked") : "insufficient_gems",
            wasUnlocked ? 0 : GameProgress.ThemeCosts[previewTheme]);
        if (!success) ShowToast("NOT ENOUGH GEMS");
        else
        {
            manager.ApplySelectedCosmetics();
            ShowToast("THEME SELECTED");
        }
        RefreshShop();
    }

    private void RefreshMenu()
    {
        if (menuBestText != null) menuBestText.text = $"BEST:{GameProgress.BestScore}";
        if (menuGemsText != null) menuGemsText.text = $"gems:{GameProgress.TotalGems}";
        if (menuPlayerNameText != null) menuPlayerNameText.text = FormatMenuPlayerName(CloudSaveManager.PlayerName);
        if (accountButtonText != null)
            accountButtonText.text = CloudSaveManager.IsCloudSaveDisabled ? "enable cloud save" : "delete account";
        RefreshSettings();
    }

    private static string FormatMenuPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return string.Empty;
        string upperName = playerName.Trim().ToUpperInvariant();
        return upperName.Length <= MenuPlayerNameVisibleCharacters
            ? upperName
            : upperName.Substring(0, MenuPlayerNameVisibleCharacters) + "...";
    }

    private void RefreshShop()
    {
        CarDefinition car = CarCatalog.Load()?.Get(previewSkin);
        bool skinUnlocked = GameProgress.IsCarUnlocked(previewSkin);
        bool themeUnlocked = GameProgress.IsThemeUnlocked(previewTheme);
        if (car != null)
        {
            string ownership = skinUnlocked
                ? previewSkin == GameProgress.SelectedCarIndex ? "SELECTED" : "UNLOCKED"
                : car.gemCost + " GEMS OR REWARDED AD";
            int speedPercent = Mathf.RoundToInt(car.speedMultiplier * 100f);
            int handlingPercent = Mathf.RoundToInt(car.perfectWindowMultiplier * 100f);
            string carClass = car.drivingStyle;
            int classSeparator = carClass.IndexOf(':');
            if (classSeparator >= 0) carClass = carClass.Substring(0, classSeparator);
            skinText.text = $"{car.displayName.ToUpperInvariant()}\n{carClass.ToUpperInvariant()} CLASS";
            carSpeedSlider.value = speedPercent;
            carHandlingSlider.value = handlingPercent;
            carSpeedValueText.text = speedPercent.ToString();
            carHandlingValueText.text = handlingPercent.ToString();
            carOwnershipText.text = $"{ownership}\n{GameProgress.GetCarMasteryProgressText(previewSkin)}";
            TMP_Text chooseLabel = chooseSkinButton.GetComponentInChildren<TMP_Text>();
            if (chooseLabel != null) chooseLabel.text = skinUnlocked
                ? previewSkin == GameProgress.SelectedCarIndex ? "SELECTED" : "SELECT CAR"
                : $"UNLOCK  {car.gemCost} GEMS";
            chooseSkinButton.interactable = previewSkin != GameProgress.SelectedCarIndex;
            watchAdUnlockCarButton.gameObject.SetActive(AdConfiguration.AdsEnabled && !skinUnlocked && car.rewardedAdUnlock);
            watchAdUnlockCarButton.interactable = !carUnlockAdInProgress;
            if (watchAdUnlockCarButtonText != null)
                watchAdUnlockCarButtonText.text = carUnlockAdInProgress ? "LOADING REWARDED AD..." : "WATCH AD TO UNLOCK";
            garagePreview?.Show(car);
        }
        else
        {
            int legacy = Mathf.Clamp(previewSkin, 0, GameProgress.SkinNames.Length - 1);
            skinText.text = $"CAR: {GameProgress.SkinNames[legacy]}\n{(skinUnlocked ? "UNLOCKED" : GameProgress.SkinCosts[legacy] + " GEMS")}";
            if (watchAdUnlockCarButton != null) watchAdUnlockCarButton.gameObject.SetActive(false);
            if (carOwnershipText != null) carOwnershipText.text = string.Empty;
        }
        themeText.text = $"WORLD: {GameProgress.ThemeNames[previewTheme]}\n{(themeUnlocked ? "UNLOCKED" : GameProgress.ThemeCosts[previewTheme] + " GEMS")}";
    }

    private void RefreshGemStore()
    {
        if (storeBalanceText == null || gemStore == null) return;
        storeBalanceText.text = $"YOUR GEMS: {GameProgress.TotalGems}";
        EconomyConfig economy = ConfigManager.Current.economy;
        bool giftAvailable = GameProgress.DailyStoreGiftAvailable;
        dailyGiftButtonText.text = giftAvailable
            ? $"DAILY GIFT  +{economy.dailyGiftGems} GEMS  -  FREE"
            : !AdConfiguration.AdsEnabled ? "DAILY GIFT ALREADY CLAIMED"
            : storeRewardAdInProgress ? "LOADING REWARDED AD..." : $"WATCH AD  +{economy.rewardedStoreGems} GEMS";
        dailyGiftButton.interactable = !storeRewardAdInProgress && (giftAvailable || AdConfiguration.AdsEnabled);

        for (int i = 0; i < gemPackButtons.Length; i++)
        {
            string price = gemStore.GetLocalizedPrice(GemStoreManager.ProductIds[i], GemStoreManager.FallbackPrices[i]);
            gemPackButtonTexts[i].text = $"{GemStoreManager.GemAmounts[i]}\nGEMS\n{price}";
            gemPackButtons[i].interactable = gemStore.CanPurchase(GemStoreManager.ProductIds[i]);
        }

        storeStatusText.text = gemStore.StatusText;
        RefreshMenu();
    }

    private IEnumerator NewBestRoutine(int score)
    {
        RectTransform rect = (RectTransform)newBestText.transform;
        newBestText.text = $"NEW HIGH SCORE!  {score}";
        newBestText.gameObject.SetActive(true);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.62f);
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.25f;
        newBestText.alpha = 0f;
        rect.DOKill();
        newBestText.DOKill();
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(newBestText.gameObject);
        sequence.Append(newBestText.DOFade(1f, 0.12f));
        sequence.Join(rect.DOScale(1.28f, 0.4f).SetEase(Ease.OutBack));
        sequence.Join(rect.DOPunchRotation(new Vector3(0f, 0f, 12f), 0.45f, 8, 0.45f));
        sequence.Append(rect.DOPunchScale(Vector3.one * 0.14f, 0.22f, 6, 0.6f));
        sequence.AppendInterval(1.38f);
        sequence.Append(rect.DOAnchorPos(new Vector2(0f, 650f), 0.55f).SetEase(Ease.InOutCubic));
        sequence.Join(rect.DOScale(0.62f, 0.55f));
        yield return sequence.WaitForCompletion();
        newBestRoutine = null;
    }

    private IEnumerator ReviveCountdownRoutine(Action ready)
    {
        RectTransform rect = (RectTransform)toastText.transform;
        string[] steps = { "3", "2", "1", "GO" };
        Color originalColour = toastText.color;
        toastText.gameObject.SetActive(true);
        toastText.color = gold;

        for (int i = 0; i < steps.Length; i++)
        {
            toastText.text = steps[i];
            toastText.alpha = 0f;
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * 0.25f;
            rect.DOKill();
            toastText.DOKill();

            GameplayConfig config = ConfigManager.Current.gameplay;
            float hold = i == steps.Length - 1
                ? config.reviveCountdownGoSeconds
                : config.reviveCountdownStepSeconds;
            Sequence tick = DOTween.Sequence().SetUpdate(true).SetLink(toastText.gameObject);
            tick.Append(toastText.DOFade(1f, 0.08f));
            tick.Join(rect.DOScale(i == steps.Length - 1 ? 1.35f : 1.1f, 0.24f).SetEase(Ease.OutBack));
            tick.AppendInterval(hold);
            tick.Append(toastText.DOFade(0f, 0.12f));
            yield return tick.WaitForCompletion();
        }

        toastText.color = originalColour;
        toastText.gameObject.SetActive(false);
        reviveCountdownRoutine = null;
        ready?.Invoke();
    }

    private void ShowPanel(GameObject panel)
    {
        panel.SetActive(true);
        bool networkIsBlocking = networkPanel != null &&
                                 networkPanel.activeSelf &&
                                 panel != networkPanel;
        SetFullScreenBackdropVisible(networkIsBlocking ||
                                     (panel != hudPanel && panel != specialTileFtuePanel));
        manager.SetBannerVisible(!networkIsBlocking && ShouldShowBanner(panel));
        if (networkIsBlocking) networkPanel.transform.SetAsLastSibling();
        DOTween.Kill(panel, true);

        if (!initialScreenPresented)
        {
            initialScreenPresented = true;
            foreach (Transform child in panel.transform)
            {
                if (!child.gameObject.activeSelf || !(child is RectTransform rect)) continue;
                rect.DOKill(false);
                rect.localScale = Vector3.one;
                CanvasGroup initialGroup = child.GetComponent<CanvasGroup>();
                if (initialGroup != null)
                {
                    initialGroup.DOKill(false);
                    initialGroup.alpha = 1f;
                }
            }
            return;
        }

        Sequence entrance = DOTween.Sequence().SetUpdate(true).SetTarget(panel).SetLink(panel);
        int animatedIndex = 0;
        bool isMenu = panel == menuPanel;
        if (isMenu)
        {
            InsertPanelEntrance(entrance, menuBestText != null ? menuBestText.rectTransform : null, ref animatedIndex);
            InsertPanelEntrance(entrance, quitButton != null ? (RectTransform)quitButton.transform : null, ref animatedIndex);
            InsertPanelEntrance(entrance, menuGemsText != null ? menuGemsText.rectTransform : null, ref animatedIndex);
        }

        foreach (Transform child in panel.transform)
        {
            if (!child.gameObject.activeSelf || !(child is RectTransform rect)) continue;
            if (isMenu && ((menuBestText != null && rect == menuBestText.rectTransform) ||
                           (quitButton != null && rect == quitButton.transform) ||
                           (menuGemsText != null && rect == menuGemsText.rectTransform))) continue;
            InsertPanelEntrance(entrance, rect, ref animatedIndex);
        }
    }

    private static void InsertPanelEntrance(Sequence entrance, RectTransform rect, ref int animatedIndex)
    {
        if (rect == null || !rect.gameObject.activeSelf) return;
        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        rect.DOKill();
        group.DOKill();

        Vector2 destination = rect.anchoredPosition;
        rect.anchoredPosition = destination + Vector2.down * 42f;
        rect.localScale = Vector3.one * 0.94f;
        group.alpha = 0f;
        float delay = animatedIndex++ * 0.035f;
        entrance.Insert(delay, rect.DOAnchorPos(destination, 0.36f).SetEase(Ease.OutCubic));
        entrance.Insert(delay, rect.DOScale(1f, 0.36f).SetEase(Ease.OutBack));
        entrance.Insert(delay, group.DOFade(1f, 0.22f));
    }

    private void HideAll()
    {
        RestoreTimeAfterSpecialTileTooltip();
        RestoreTimeAfterQuitConfirmation();
        DOTween.Kill(menuPanel, true);
        DOTween.Kill(hudPanel, true);
        DOTween.Kill(gameOverPanel, true);
        DOTween.Kill(pausePanel, true);
        DOTween.Kill(shopPanel, true);
        DOTween.Kill(gemStorePanel, true);
        DOTween.Kill(privacyPanel, true);
        DOTween.Kill(tutorialPanel, true);
        DOTween.Kill(challengePanel, true);
        DOTween.Kill(deleteAccountPanel, true);
        if (loginPanel != null) DOTween.Kill(loginPanel, true);
        if (namePanel != null) DOTween.Kill(namePanel, true);
        if (quitPanel != null) DOTween.Kill(quitPanel, true);
        if (specialTileFtuePanel != null) DOTween.Kill(specialTileFtuePanel, true);
        if (leaderboardTimeframePanel != null) DOTween.Kill(leaderboardTimeframePanel, true);
        menuPanel.SetActive(false);
        hudPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        pausePanel.SetActive(false);
        shopPanel.SetActive(false);
        gemStorePanel.SetActive(false);
        privacyPanel.SetActive(false);
        tutorialPanel.SetActive(false);
        challengePanel.SetActive(false);
        deleteAccountPanel.SetActive(false);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (namePanel != null) namePanel.SetActive(false);
        if (quitPanel != null) quitPanel.SetActive(false);
        if (specialTileFtuePanel != null) specialTileFtuePanel.SetActive(false);
        if (leaderboardTimeframePanel != null) leaderboardTimeframePanel.SetActive(false);
        if (newBestText != null) newBestText.gameObject.SetActive(false);
        bool networkIsBlocking = networkPanel != null && networkPanel.activeSelf;
        SetFullScreenBackdropVisible(networkIsBlocking);
        if (networkIsBlocking)
        {
            Time.timeScale = 0f;
            networkPanel.transform.SetAsLastSibling();
            manager.SetBannerVisible(false);
        }
    }

    private bool ShouldShowBanner(GameObject panel)
    {
        // Never cover active gameplay or pause with a banner. Banners are
        // limited to screens where they cannot obstruct driving controls.
        return panel == menuPanel ||
               panel == gameOverPanel ||
               panel == shopPanel ||
               panel == gemStorePanel ||
               panel == challengePanel ||
               panel == leaderboardTimeframePanel;
    }

    private void RefreshBannerForVisibleScreen()
    {
        bool gameplayOrBlockingPanelVisible = hudPanel.activeSelf ||
                                              pausePanel.activeSelf ||
                                              (loginPanel != null && loginPanel.activeSelf) ||
                                              (namePanel != null && namePanel.activeSelf) ||
                                              tutorialPanel.activeSelf ||
                                              privacyPanel.activeSelf ||
                                              (quitPanel != null && quitPanel.activeSelf) ||
                                              (networkPanel != null && networkPanel.activeSelf) ||
                                              deleteAccountPanel.activeSelf;
        bool shouldShow = !gameplayOrBlockingPanelVisible &&
                          (menuPanel.activeSelf ||
                           gameOverPanel.activeSelf ||
                           shopPanel.activeSelf ||
                           gemStorePanel.activeSelf ||
                           challengePanel.activeSelf ||
                           (leaderboardTimeframePanel != null && leaderboardTimeframePanel.activeSelf));
        manager.SetBannerVisible(shouldShow);

        bool fullScreenPanelVisible = !hudPanel.activeSelf || pausePanel.activeSelf ||
                                      (quitPanel != null && quitPanel.activeSelf) ||
                                      (networkPanel != null && networkPanel.activeSelf);
        SetFullScreenBackdropVisible(fullScreenPanelVisible);
    }

    private void EnsureFullScreenBackdrop()
    {
        if (fullScreenBackdrop != null) return;

        fullScreenBackdrop = new GameObject("Full Screen UI Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = fullScreenBackdrop.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();

        Image image = fullScreenBackdrop.GetComponent<Image>();
        image.color = new Color(0.025f, 0.035f, 0.075f, 1f);
        image.raycastTarget = false;
        fullScreenBackdrop.SetActive(false);
    }

    private void SetFullScreenBackdropVisible(bool visible)
    {
        if (fullScreenBackdrop != null) fullScreenBackdrop.SetActive(visible);
    }

    private static void SetButtonColour(Button button, Color colour)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = colour;
        ColorBlock colors = button.colors;
        colors.normalColor = colour;
        colors.highlightedColor = Color.Lerp(colour, Color.white, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(colour, Color.black, 0.2f);
        colors.disabledColor = new Color(colour.r, colour.g, colour.b, colour.a * 0.45f);
        button.colors = colors;
    }

    private void AnimateButton(RectTransform rect)
    {
        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOPunchScale(Vector3.one * -0.12f, 0.22f, 6, 0.65f)
            .SetUpdate(true)
            .SetLink(rect.gameObject);
    }

    private void ApplySafeArea()
    {
        Rect area = Screen.safeArea;
        lastSafeArea = area;
        Vector2 min = area.position;
        Vector2 max = area.position + area.size;
        min.y = Mathf.Min(max.y, min.y + bannerInsetPixels);
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;
        safeRoot.anchorMin = min;
        safeRoot.anchorMax = max;
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;
    }

    public void SetBannerInset(float heightPixels)
    {
        float clampedHeight = Mathf.Clamp(heightPixels, 0f, Screen.height * 0.25f);
        if (Mathf.Approximately(bannerInsetPixels, clampedHeight)) return;
        bannerInsetPixels = clampedHeight;
        ApplySafeArea();
    }
}
