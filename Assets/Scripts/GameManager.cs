using System.Collections;
using GooglePlayGames.BasicApi;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, Reviving, Paused, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Existing scene references")]
    public bool gameStarted;
    public GameObject platformSpawner;
    public GameObject gameplayUI;
    public GameObject MenuUI;
    public TMP_Text scoreText;
    public TMP_Text highScoreText;
    public AudioClip[] gameMusic;
    [Header("Feedback")]
    public AudioClip newBestSound;

    public GameState State { get; private set; } = GameState.Menu;
    public int Score { get; private set; }
    public int RunGems { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public bool IsDailyRun { get; private set; }
    public bool HasShield { get; private set; }
    public bool UsesNewPlayerSpeedThisRun { get; private set; }
    public float SpeedMultiplier { get; private set; } = 1f;
    public float Difficulty01 => Mathf.Clamp01(Score / (float)Mathf.Max(1, ConfigManager.Current.gameplay.difficultyScoreCap));

    private static bool autoStartAfterReload;
    private static bool dailyAfterReload;
    private AudioSource audioSource;
    private CarController car;
    private PlatformSpawner spawner;
    private RuntimeGameUI runtimeUI;
    private MediatedAdsManager ads;
    private MediatedAdsManager Ads => ads ??= MediatedAdsManager.Create();
    private bool bannerVisibleRequested;
    private Coroutine speedRoutine;
    private bool reviveAdInProgress;
    private int savedRunGems;
    private bool doubleGemsClaimed;
    private int deathCount;
    private float runStartRealtime;
    private int turnCount;
    private int perfectTurnCount;
    private int diamondCount;
    private int specialTileCount;
    private int nextScoreMilestoneIndex;
    private bool runAbandonmentLogged;
    private int bestScoreAtRunStart;
    private bool newHighScoreAnnounced;
    private bool reviveUsedThisRun;
    private int masteryScoreGranted;
    private int masteryGemsGranted;
    private int masteryComboGranted;
    private string currentPostRunObjective;
    private PersonalBestGhost personalBestGhost;
    private readonly Collider[] nearbyColliders = new Collider[64];
    private CameraFollow cameraFollow;
    private CameraColourChanger cameraColourChanger;
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject androidVibrator;
    private int androidSdkVersion;
    private bool hapticFailureLogged;
#endif

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        GameplayConfig config = ConfigManager.Current.gameplay;
        Application.targetFrameRate = Mathf.Max(30, config.targetFrameRate);
        QualitySettings.vSyncCount = 0;
        Time.fixedDeltaTime = 1f / Mathf.Max(30, config.targetFrameRate);
        Time.maximumDeltaTime = Mathf.Max(0.02f, config.maximumDeltaTime);
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        ApplySavedAudioSetting();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        car = FindFirstObjectByType<CarController>();
        personalBestGhost = gameObject.GetComponent<PersonalBestGhost>();
        if (personalBestGhost == null) personalBestGhost = gameObject.AddComponent<PersonalBestGhost>();
        personalBestGhost.Initialize(car);
        spawner = platformSpawner != null ? platformSpawner.GetComponent<PlatformSpawner>() : null;
        runtimeUI = RuntimeGameUI.Create(this);
        if (Camera.main != null)
        {
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
            cameraColourChanger = Camera.main.GetComponent<CameraColourChanger>();
        }

        if (MenuUI != null) MenuUI.SetActive(false);
        if (gameplayUI != null) gameplayUI.SetActive(false);

        ApplySelectedCosmetics();
        StartCoroutine(InitializeAdsAfterFirstFrame());
        GameHealthMonitor.StopTrace("app_start_to_game_ready", true);

        if (autoStartAfterReload)
        {
            bool daily = dailyAfterReload;
            autoStartAfterReload = false;
            dailyAfterReload = false;
            StartRun(daily);
            return;
        }

        if (cameraColourChanger != null) cameraColourChanger.SetMenuMode();
        PlayMenuMusic();
        runtimeUI.ShowMenu();
    }

    private IEnumerator InitializeAdsAfterFirstFrame()
    {
        // Keep SDK setup out of the first rendered menu frame.
        yield return null;
        ads = MediatedAdsManager.Create();
        ads.BannerInsetChanged -= HandleBannerInsetChanged;
        ads.BannerInsetChanged += HandleBannerInsetChanged;
        if (bannerVisibleRequested) ads.ShowBanner();
        else ads.HideBanner();
    }

    public void SetBannerVisible(bool visible)
    {
        bannerVisibleRequested = visible;
        if (ads == null) return;
        if (visible) ads.ShowBanner();
        else ads.HideBanner();
    }

    private void HandleBannerInsetChanged(float heightPixels)
    {
        if (runtimeUI != null)
            runtimeUI.SetBannerInset(bannerVisibleRequested ? heightPixels : 0f);
    }

    private void OnDestroy()
    {
        if (ads != null) ads.BannerInsetChanged -= HandleBannerInsetChanged;
#if UNITY_ANDROID && !UNITY_EDITOR
        androidVibrator?.Dispose();
        androidVibrator = null;
#endif
        if (instance == this) instance = null;
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
        Time.timeScale = 1f;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            GameProgress.Save();
            return;
        }

        ApplySavedAudioSetting();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ApplySavedAudioSetting();
    }

    public void StartRun(bool dailyChallenge)
    {
        if (State == GameState.Playing) return;

        bool resetWorld = State == GameState.GameOver;
        GameplayConfig gameplayConfig = ConfigManager.Current.gameplay;
        int runNumber = GameProgress.RegisterRunStarted(gameplayConfig.newPlayerSpeedRunCount);
        UsesNewPlayerSpeedThisRun = runNumber <= gameplayConfig.newPlayerSpeedRunCount;

        if (speedRoutine != null)
        {
            StopCoroutine(speedRoutine);
            speedRoutine = null;
        }

        Score = 0;
        RunGems = 0;
        Combo = 0;
        BestCombo = 0;
        SpeedMultiplier = 1f;
        HasShield = false;
        reviveAdInProgress = false;
        savedRunGems = 0;
        doubleGemsClaimed = false;
        deathCount = 0;
        turnCount = 0;
        perfectTurnCount = 0;
        diamondCount = 0;
        specialTileCount = 0;
        nextScoreMilestoneIndex = 0;
        runAbandonmentLogged = false;
        runStartRealtime = Time.realtimeSinceStartup;
        IsDailyRun = dailyChallenge;
        bestScoreAtRunStart = GameProgress.BestScore;
        newHighScoreAnnounced = false;
        reviveUsedThisRun = false;
        masteryScoreGranted = 0;
        masteryGemsGranted = 0;
        masteryComboGranted = 0;
        currentPostRunObjective = string.Empty;

        if (resetWorld)
        {
            if (car != null) car.ResetForNewRun();
            if (spawner != null) spawner.ResetForNewRun(dailyChallenge, GameProgress.SelectedTheme);
            if (cameraFollow != null) cameraFollow.ResetFollow();
        }
        else if (spawner != null)
        {
            spawner.Configure(dailyChallenge, GameProgress.SelectedTheme);
        }

        State = GameState.Playing;
        gameStarted = true;
        if (car != null) car.ApplyCar(GameProgress.SelectedCarIndex);
        personalBestGhost?.StartRun(dailyChallenge);
        if (cameraColourChanger != null) cameraColourChanger.SetGameplayState(0f, 0);

        if (platformSpawner != null) platformSpawner.SetActive(true);
        if (scoreText != null) scoreText.text = "0";

        runtimeUI.ShowHud();
        runtimeUI.UpdateHud(Score, RunGems, Combo, HasShield);
        PlayGameplayMusic();
        GameAnalytics.RunStarted(dailyChallenge, GameProgress.SelectedSkin, GameProgress.SelectedTheme);
    }

    public void RegisterTurn(bool perfect)
    {
        if (State != GameState.Playing) return;
        turnCount++;

        if (perfect)
        {
            perfectTurnCount++;
            Combo++;
            BestCombo = Mathf.Max(BestCombo, Combo);
            GameplayConfig config = ConfigManager.Current.gameplay;
            int multiplier = Mathf.Clamp(1 + Combo / Mathf.Max(1, config.comboScoreStep), 1, Mathf.Max(1, config.maxComboMultiplier));
            Score += multiplier;
            runtimeUI.ShowToast(multiplier > 1 ? $"PERFECT x{multiplier}" : "PERFECT");
            ShakeCamera(config.perfectShakeDuration, config.perfectShakeIntensity);
            TriggerHaptic(config.perfectHapticDurationMs, config.perfectHapticAmplitude);
        }
        else
        {
            Combo = 0;
            Score++;
        }

        UpdateScoreDisplay();
    }

    public IEnumerator UpdateScore()
    {
        RegisterTurn(false);
        yield break;
    }

    public void DiamondScore()
    {
        diamondCount++;
        CollectGems(1, true);
    }

    public void CollectGems(int amount, bool playSound)
    {
        if (State != GameState.Playing || amount <= 0) return;
        RunGems += amount;
        Score += amount * Mathf.Max(0, ConfigManager.Current.gameplay.gemScoreMultiplier);
        UpdateScoreDisplay();
        runtimeUI.AnimateHudGems(amount);

        if (playSound && audioSource != null && gameMusic != null && gameMusic.Length > 2 && gameMusic[2] != null)
            audioSource.PlayOneShot(gameMusic[2], ConfigManager.Current.gameplay.gemSoundVolume);
    }

    public void ApplySpecialTile(SpecialTileType type, Vector3 origin)
    {
        if (State != GameState.Playing) return;
        GameplayConfig config = ConfigManager.Current.gameplay;
        specialTileCount++;
        GameAnalytics.SpecialTileUsed(type.ToString());
        if (cameraColourChanger != null) cameraColourChanger.PulseSpecial(type);

        switch (type)
        {
            case SpecialTileType.SpeedBoost:
                SetTemporarySpeed(config.speedBoostMultiplier, config.specialEffectDuration);
                runtimeUI.ShowSpecialTileToast("SPEED BOOST!");
                break;
            case SpecialTileType.SlowMotion:
                SetTemporarySpeed(config.slowMotionMultiplier, config.specialEffectDuration);
                runtimeUI.ShowSpecialTileToast("SLOW MOTION");
                break;
            case SpecialTileType.Shield:
                HasShield = true;
                runtimeUI.ShowSpecialTileToast("SHIELD READY");
                break;
            case SpecialTileType.GemMagnet:
                CollectNearbyDiamonds(origin, config.gemMagnetRadius);
                runtimeUI.ShowSpecialTileToast("GEM MAGNET");
                break;
            case SpecialTileType.Mystery:
                int reward = Random.Range(config.mysteryRewardMinInclusive,
                    Mathf.Max(config.mysteryRewardMinInclusive + 1, config.mysteryRewardMaxExclusive));
                CollectGems(reward, true);
                runtimeUI.ShowSpecialTileToast($"MYSTERY +{reward} GEMS");
                break;
        }

        runtimeUI.UpdateHud(Score, RunGems, Combo, HasShield);
    }

    public bool TryShowSpecialTileTooltip(SpecialTileType type, Vector3 tilePosition)
    {
        if (State != GameState.Playing || runtimeUI == null || car == null) return false;
        Vector3 toTile = tilePosition - car.transform.position;
        toTile.y = 0f;
        float tooltipDistance = Mathf.Max(0f, ConfigManager.Current.gameplay.specialTooltipDistance);
        if (toTile.sqrMagnitude > tooltipDistance * tooltipDistance) return false;
        if (toTile.sqrMagnitude > 0.01f && Vector3.Dot(toTile.normalized, car.MovementDirection) < -0.15f) return false;
        return runtimeUI.TryShowSpecialTileTooltip(type);
    }

    public void GameOver()
    {
        if (State != GameState.Playing) return;

        if (HasShield && car != null)
        {
            HasShield = false;
            car.Revive();
            runtimeUI.ShowToast("SHIELD SAVED YOU!");
            runtimeUI.UpdateHud(Score, RunGems, Combo, HasShield);
            GameAnalytics.GameplayReward("shield_save", 1);
            return;
        }

        State = GameState.GameOver;
        if (cameraColourChanger != null) cameraColourChanger.SetGameOverMood();
        deathCount++;
        GameAnalytics.RunFailed(Score, RunGems, BestCombo, IsDailyRun, deathCount,
            Time.realtimeSinceStartup - runStartRealtime, turnCount, perfectTurnCount, diamondCount, specialTileCount);
        GameplayConfig config = ConfigManager.Current.gameplay;
        ShakeCamera(config.deathShakeDuration, config.deathShakeIntensity);
        gameStarted = false;
        if (spawner != null) spawner.StopSpawning();
        SaveRunRewards();
        personalBestGhost?.CompleteRun(Score);
        PlayGamesLeaderboardManager.SubmitRunScore(Score, IsDailyRun);
        currentPostRunObjective = GameProgress.GetPostRunObjective(Score, bestScoreAtRunStart, out string objectiveType);
        GameAnalytics.PersonalizedObjectiveShown(objectiveType);
        bool adsEnabled = AdConfiguration.AdsEnabled;
        runtimeUI.ShowGameOver(Score, GameProgress.BestScore, RunGems, BestCombo,
            adsEnabled && !reviveUsedThisRun, adsEnabled && !doubleGemsClaimed, currentPostRunObjective,
            PlayGamesLeaderboardManager.IsConfigured);
        if (Ads.NotifyRunEnded()) GameAnalytics.InterstitialRequested(ConfigManager.Current.ads.gameOverPlacement);
    }

    public void RequestRevive()
    {
        if (State != GameState.GameOver || reviveAdInProgress || reviveUsedThisRun || car == null) return;
        reviveAdInProgress = true;
        string placement = ConfigManager.Current.ads.revivePlacement;
        GameAnalytics.RewardedAdRequested(placement);
        Ads.ShowRewarded(placement, success =>
        {
            reviveAdInProgress = false;
            GameAnalytics.RewardedAdResult(placement, success);
            if (!success)
            {
                runtimeUI.ShowToast("AD NOT READY - TRY AGAIN");
                return;
            }
            if (State != GameState.GameOver) return;
            reviveUsedThisRun = true;
            State = GameState.Reviving;
            gameStarted = false;
            if (cameraColourChanger != null) cameraColourChanger.SetGameplayState(Difficulty01, Combo);
            car.Revive(ConfigManager.Current.gameplay.reviveSafetyInset);
            if (spawner != null) spawner.RebuildForRevive(car.LastSafePlatformPosition, car.MovementDirection);
            runtimeUI.ShowHud();
            runtimeUI.UpdateHud(Score, RunGems, Combo, HasShield);
            runtimeUI.PlayReviveCountdown(() =>
            {
                if (State != GameState.Reviving) return;
                State = GameState.Playing;
                gameStarted = true;
                if (spawner != null) spawner.ResumeSpawning();
                GameAnalytics.GameplayReward("revive", 1);
            });
        });
    }

    public void RequestDoubleGems()
    {
        if (State != GameState.GameOver || doubleGemsClaimed || RunGems <= 0) return;
        string placement = ConfigManager.Current.ads.doubleGemsPlacement;
        GameAnalytics.RewardedAdRequested(placement);
        Ads.ShowRewarded(placement, success =>
        {
            GameAnalytics.RewardedAdResult(placement, success);
            if (!success)
            {
                runtimeUI.ShowToast("AD NOT READY - TRY AGAIN");
                return;
            }
            if (doubleGemsClaimed) return;
            doubleGemsClaimed = true;
            GameProgress.AddGems(RunGems);
            GameAnalytics.GameplayReward("double_gems", RunGems);
            runtimeUI.ShowToast($"BONUS +{RunGems} GEMS");
            runtimeUI.ShowGameOver(Score, GameProgress.BestScore, RunGems * 2, BestCombo,
                AdConfiguration.AdsEnabled && !reviveUsedThisRun, false, currentPostRunObjective,
                PlayGamesLeaderboardManager.IsConfigured);
        });
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
        {
            State = GameState.Paused;
            gameStarted = false;
            Time.timeScale = 0f;
            runtimeUI.ShowPause(true);
            GameAnalytics.GamePaused(true, Score);
        }
        else if (State == GameState.Paused)
        {
            State = GameState.Playing;
            gameStarted = true;
            Time.timeScale = 1f;
            runtimeUI.ShowPause(false);
            GameAnalytics.GamePaused(false, Score);
        }
    }

    public void ToggleMusic()
    {
        GameProgress.SetMusicEnabled(!GameProgress.MusicEnabled);
        ApplySavedAudioSetting();
        if (!GameProgress.MusicEnabled) audioSource.Stop();
        else if (State == GameState.Playing || State == GameState.Paused) PlayGameplayMusic();
        else PlayMenuMusic();
        runtimeUI.RefreshSettings();
        GameAnalytics.SettingChanged("music", GameProgress.MusicEnabled);
    }

    public void ToggleHaptics()
    {
        GameProgress.SetHapticsEnabled(!GameProgress.HapticsEnabled);
        if (GameProgress.HapticsEnabled)
        {
            GameplayConfig config = ConfigManager.Current.gameplay;
            TriggerHaptic(config.perfectHapticDurationMs, config.perfectHapticAmplitude);
        }
        runtimeUI.RefreshSettings();
        GameAnalytics.SettingChanged("haptics", GameProgress.HapticsEnabled);
    }

    public void ShowLeaderboard()
    {
        bool opened = IsDailyRun
            ? PlayGamesLeaderboardManager.ShowDaily(LeaderboardTimeSpan.Daily)
            : PlayGamesLeaderboardManager.ShowMain(LeaderboardTimeSpan.AllTime);
        if (!opened && runtimeUI != null)
            runtimeUI.ShowToast(PlayGamesLeaderboardManager.IsConfigured
                ? "SIGN IN WITH PLAY GAMES TO VIEW LEADERBOARDS"
                : "LEADERBOARD IDS NOT CONFIGURED");
    }

    public void ShowMainLeaderboard()
    {
        ShowMainLeaderboard(LeaderboardTimeSpan.AllTime);
    }

    public void ShowMainLeaderboard(LeaderboardTimeSpan timeSpan)
    {
        bool opened = PlayGamesLeaderboardManager.ShowMain(timeSpan);
        if (!opened && runtimeUI != null)
            runtimeUI.ShowToast(PlayGamesLeaderboardManager.IsConfigured
                ? "SIGN IN WITH PLAY GAMES TO VIEW LEADERBOARDS"
                : "LEADERBOARD IDS NOT CONFIGURED");
    }

    public void RestartRun()
    {
        if (State != GameState.GameOver) return;
        StartRun(IsDailyRun);
    }

    public void ReturnHome()
    {
        LogRunAbandoned("home");
        autoStartAfterReload = false;
        dailyAfterReload = false;
        ReloadLevel();
    }

    public void ReloadLevel()
    {
        GameProgress.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ApplySelectedCosmetics()
    {
        if (car == null) car = FindFirstObjectByType<CarController>();
        if (car != null) car.ApplyCar(GameProgress.SelectedCarIndex);
        if (cameraColourChanger != null) cameraColourChanger.SetTheme(GameProgress.SelectedTheme);
        if (spawner != null) spawner.SetTheme(GameProgress.SelectedTheme);
    }

    public void PreviewCar(int carIndex)
    {
        if (car == null) car = FindFirstObjectByType<CarController>();
        if (car != null) car.ApplyCar(carIndex);
    }

    private void SaveRunRewards()
    {
        int newGems = Mathf.Max(0, RunGems - savedRunGems);
        savedRunGems = RunGems;
        GameProgress.AddGems(newGems);
        int masteryXp = Mathf.Max(0, Score - masteryScoreGranted) +
                        Mathf.Max(0, RunGems - masteryGemsGranted) * 3 +
                        Mathf.Max(0, BestCombo - masteryComboGranted) * 2;
        masteryScoreGranted = Mathf.Max(masteryScoreGranted, Score);
        masteryGemsGranted = Mathf.Max(masteryGemsGranted, RunGems);
        masteryComboGranted = Mathf.Max(masteryComboGranted, BestCombo);
        string masteryReward = GameProgress.AddCarMasteryProgress(GameProgress.SelectedCarIndex, masteryXp);
        if (!string.IsNullOrEmpty(masteryReward)) runtimeUI.ShowToast(masteryReward);
        string achievement = GameProgress.RecordRun(Score, BestCombo, newGems, IsDailyRun);
        if (!string.IsNullOrEmpty(achievement))
        {
            GameAnalytics.ProgressRewardUnlocked(achievement);
            runtimeUI.ShowToast(achievement);
        }
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null) scoreText.text = Score.ToString();
        runtimeUI.UpdateHud(Score, RunGems, Combo, HasShield);
        if (cameraColourChanger != null) cameraColourChanger.SetGameplayState(Difficulty01, Combo);
        if (!newHighScoreAnnounced && Score > bestScoreAtRunStart)
        {
            newHighScoreAnnounced = true;
            runtimeUI.ShowNewHighScore(Score);
            PlayNewBestFeedback();
        }
        TrackScoreMilestones();
    }

    private void TrackScoreMilestones()
    {
        int[] milestones = ConfigManager.Current.gameplay.scoreMilestones;
        if (milestones == null) return;
        while (nextScoreMilestoneIndex < milestones.Length &&
               Score >= milestones[nextScoreMilestoneIndex])
        {
            int milestone = milestones[nextScoreMilestoneIndex++];
            GameAnalytics.RunMilestone(milestone, IsDailyRun,
                Time.realtimeSinceStartup - runStartRealtime);
        }
    }

    private void SetTemporarySpeed(float multiplier, float duration)
    {
        if (speedRoutine != null) StopCoroutine(speedRoutine);
        speedRoutine = StartCoroutine(TemporarySpeedRoutine(multiplier, duration));
    }

    private IEnumerator TemporarySpeedRoutine(float multiplier, float duration)
    {
        SpeedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        SpeedMultiplier = 1f;
        speedRoutine = null;
    }

    private void CollectNearbyDiamonds(Vector3 origin, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(origin, radius, nearbyColliders);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = nearbyColliders[i];
            if (!hit.CompareTag("Diamond") || !hit.gameObject.activeSelf) continue;
            hit.gameObject.SetActive(false);
            CollectGems(1, false);
        }
    }

    private void PlayMenuMusic()
    {
        if (audioSource == null) return;
        ApplySavedAudioSetting();
        if (!GameProgress.MusicEnabled || gameMusic == null || gameMusic.Length == 0)
        {
            audioSource.Stop();
            return;
        }
        audioSource.clip = gameMusic[0];
        audioSource.loop = true;
        audioSource.volume = ConfigManager.Current.gameplay.menuMusicVolume;
        audioSource.Play();
    }

    private void PlayGameplayMusic()
    {
        if (audioSource == null) return;
        ApplySavedAudioSetting();
        if (!GameProgress.MusicEnabled)
        {
            audioSource.Stop();
            return;
        }
        if (gameMusic != null && gameMusic.Length > 1 && gameMusic[1] != null)
        {
            audioSource.clip = gameMusic[1];
            audioSource.loop = true;
            audioSource.volume = ConfigManager.Current.gameplay.gameplayMusicVolume;
            audioSource.Play();
        }
    }

    private void ApplySavedAudioSetting()
    {
        if (audioSource == null) return;
        audioSource.playOnAwake = false;
        audioSource.mute = !GameProgress.MusicEnabled;
        if (!GameProgress.MusicEnabled && audioSource.isPlaying) audioSource.Stop();
    }

    private void PlayNewBestFeedback()
    {
        GameplayConfig config = ConfigManager.Current.gameplay;
        TriggerHaptic(config.newBestHapticDurationMs, config.newBestHapticAmplitude);

        if (!GameProgress.MusicEnabled || audioSource == null) return;
        AudioClip clip = newBestSound;
        if (clip == null && gameMusic != null && gameMusic.Length > 2) clip = gameMusic[2];
        if (clip != null) audioSource.PlayOneShot(clip, Mathf.Clamp01(config.newBestSoundVolume));
    }

    private void TriggerHaptic(int durationMs, int amplitude)
    {
        if (!GameProgress.HapticsEnabled || !Application.isMobilePlatform) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (androidVibrator == null)
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    androidVibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                    androidSdkVersion = version.GetStatic<int>("SDK_INT");
            }

            if (androidVibrator == null || !androidVibrator.Call<bool>("hasVibrator")) return;
            long safeDuration = Mathf.Clamp(durationMs, 1, 200);
            int safeAmplitude = Mathf.Clamp(amplitude, 1, 255);
            if (androidSdkVersion >= 26)
            {
                using (AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                           "createOneShot", safeDuration, safeAmplitude))
                    androidVibrator.Call("vibrate", effect);
            }
            else
            {
                androidVibrator.Call("vibrate", safeDuration);
            }
        }
        catch (System.Exception exception)
        {
            if (hapticFailureLogged) return;
            hapticFailureLogged = true;
            Debug.LogWarning($"Android haptic feedback is unavailable: {exception.Message}");
        }
#endif
    }

    private void ShakeCamera(float duration, float intensity)
    {
        if (cameraFollow != null) cameraFollow.Shake(duration, intensity);
    }

    private void OnApplicationQuit()
    {
        LogRunAbandoned("app_quit");
    }

    private void LogRunAbandoned(string reason)
    {
        if (runAbandonmentLogged || (State != GameState.Playing && State != GameState.Paused)) return;
        runAbandonmentLogged = true;
        GameAnalytics.RunAbandoned(Score, RunGems, IsDailyRun,
            Time.realtimeSinceStartup - runStartRealtime, reason);
    }
}
