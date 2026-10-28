using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class ConfigManager : MonoBehaviour
{
    private const string ScriptableObjectResourcePath = "Config/GameConfig";
    private const string JsonResourcePath = "Config/game_config";
    private static ConfigManager instance;
    private static GameConfig current;

    public static event Action ConfigChanged;
    public static GameConfig Current
    {
        get
        {
            if (current == null) current = LoadLocalConfig();
            return current;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Current;
        if (instance != null) return;
        GameObject host = new GameObject("Config Manager");
        instance = host.AddComponent<ConfigManager>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyCachedConfig();
    }

    private void Start()
    {
        FetchUrlFallback();
    }

    private void FetchUrlFallback()
    {
        if (string.IsNullOrWhiteSpace(Current.network.remoteConfigUrl)) return;
        StartCoroutine(NetConfig.Fetch(Current.network, (success, json) =>
        {
            if (success) TryApplyRemoteOverride(json, "network");
        }));
    }

    private static GameConfig LoadLocalConfig()
    {
        GameConfigAsset scriptableConfig = Resources.Load<GameConfigAsset>(ScriptableObjectResourcePath);
        if (scriptableConfig != null)
            return Sanitize(scriptableConfig.CreateRuntimeConfig());

        TextAsset jsonAsset = Resources.Load<TextAsset>(JsonResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError($"Missing Resources/{ScriptableObjectResourcePath}.asset and Resources/{JsonResourcePath}.json. Built-in configuration defaults will be used.");
            return new GameConfig();
        }

        try
        {
            Debug.LogWarning("GameConfig ScriptableObject is missing. Using the JSON configuration fallback.");
            GameConfig loaded = JsonUtility.FromJson<GameConfig>(jsonAsset.text);
            return Sanitize(loaded ?? new GameConfig());
        }
        catch (Exception exception)
        {
            Debug.LogError($"Local game config is invalid: {exception.Message}");
            return new GameConfig();
        }
    }

    private static void ApplyCachedConfig()
    {
        if (NetConfig.TryGetCached(Current.network.cacheLifetimeMinutes, out string json))
            TryApplyRemoteOverride(json, "cache");
    }

    public static bool TryApplyRemoteOverride(string json, string source)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            JsonUtility.FromJsonOverwrite(json, Current);
            Sanitize(Current);
            ConfigChanged?.Invoke();
            Debug.Log($"Applied {source} game config version {Current.version}.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Ignored invalid {source} game config: {exception.Message}");
            return false;
        }
    }

    private static GameConfig Sanitize(GameConfig config)
    {
        GameConfig defaults = new GameConfig();
        config.network ??= defaults.network;
        config.services ??= defaults.services;
        config.ads ??= defaults.ads;
        config.cloud ??= defaults.cloud;
        config.economy ??= defaults.economy;
        config.gameplay ??= defaults.gameplay;
        config.ghost ??= defaults.ghost;
        config.background ??= defaults.background;
        config.challenges ??= defaults.challenges;

        if (string.IsNullOrWhiteSpace(config.cloud.playersCollection)) config.cloud.playersCollection = defaults.cloud.playersCollection;
        if (string.IsNullOrWhiteSpace(config.network.firebaseRemoteConfigJsonKey)) config.network.firebaseRemoteConfigJsonKey = defaults.network.firebaseRemoteConfigJsonKey;
        if (string.IsNullOrWhiteSpace(config.network.connectivityProbeUrl)) config.network.connectivityProbeUrl = defaults.network.connectivityProbeUrl;
        config.network.connectivityCheckIntervalSeconds = Mathf.Clamp(config.network.connectivityCheckIntervalSeconds, 2f, 60f);
        config.network.connectivityProbeTimeoutSeconds = Mathf.Clamp(config.network.connectivityProbeTimeoutSeconds, 2f, 15f);
        if (string.IsNullOrWhiteSpace(config.ads.bannerPlacement)) config.ads.bannerPlacement = defaults.ads.bannerPlacement;
        if (string.IsNullOrWhiteSpace(config.ads.bannerPosition)) config.ads.bannerPosition = defaults.ads.bannerPosition;
        config.ads.adLoadRetrySeconds = Mathf.Max(1f, config.ads.adLoadRetrySeconds);
        config.ads.interstitialEveryRuns = Mathf.Max(0, config.ads.interstitialEveryRuns);
        if (config.economy.legacySkinNames == null || config.economy.legacySkinNames.Length == 0) config.economy.legacySkinNames = defaults.economy.legacySkinNames;
        if (config.economy.legacySkinCosts == null || config.economy.legacySkinCosts.Length != config.economy.legacySkinNames.Length) config.economy.legacySkinCosts = defaults.economy.legacySkinCosts;
        if (config.economy.themeNames == null || config.economy.themeNames.Length == 0) config.economy.themeNames = defaults.economy.themeNames;
        if (config.economy.themeCosts == null || config.economy.themeCosts.Length != config.economy.themeNames.Length) config.economy.themeCosts = defaults.economy.themeCosts;
        if (config.economy.iapProducts == null || config.economy.iapProducts.Length == 0) config.economy.iapProducts = defaults.economy.iapProducts;
        if (config.gameplay.scoreMilestones == null) config.gameplay.scoreMilestones = defaults.gameplay.scoreMilestones;
        config.gameplay.baseMoveSpeed = Mathf.Clamp(config.gameplay.baseMoveSpeed, 4f, 12f);
        config.gameplay.newPlayerMoveSpeed = Mathf.Clamp(
            config.gameplay.newPlayerMoveSpeed, 2.5f, config.gameplay.baseMoveSpeed);
        config.gameplay.newPlayerSpeedRunCount = Mathf.Clamp(
            config.gameplay.newPlayerSpeedRunCount, 0, 100);
        config.gameplay.maximumDifficultySpeedMultiplier = Mathf.Clamp(
            config.gameplay.maximumDifficultySpeedMultiplier, 1f, 3f);
        config.gameplay.perfectTurnWindow = Mathf.Clamp(config.gameplay.perfectTurnWindow, 0.1f, 0.8f);
        config.gameplay.newPlayerPerfectTurnWindow = Mathf.Clamp(
            config.gameplay.newPlayerPerfectTurnWindow, config.gameplay.perfectTurnWindow, 1f);
        config.gameplay.difficultyScoreCap = Mathf.Clamp(config.gameplay.difficultyScoreCap, 25, 2000);
        config.gameplay.diamondChanceMin = Mathf.Clamp01(config.gameplay.diamondChanceMin);
        config.gameplay.diamondChanceMax = Mathf.Clamp(
            config.gameplay.diamondChanceMax, config.gameplay.diamondChanceMin, 1f);
        if (config.gameplay.diamondMinGap <= 0)
            config.gameplay.diamondMinGap = defaults.gameplay.diamondMinGap;
        config.gameplay.diamondMinGap = Mathf.Clamp(config.gameplay.diamondMinGap, 2, 20);
        config.gameplay.earlyMaxStraightTiles = Mathf.Clamp(config.gameplay.earlyMaxStraightTiles, 3, 10);
        config.gameplay.midMaxStraightTiles = Mathf.Clamp(
            config.gameplay.midMaxStraightTiles, 2, config.gameplay.earlyMaxStraightTiles);
        config.gameplay.lateMaxStraightTiles = Mathf.Clamp(
            config.gameplay.lateMaxStraightTiles, 2, config.gameplay.midMaxStraightTiles);
        config.gameplay.midRoadDifficultyThreshold = Mathf.Clamp01(config.gameplay.midRoadDifficultyThreshold);
        config.gameplay.lateRoadDifficultyThreshold = Mathf.Clamp(
            config.gameplay.lateRoadDifficultyThreshold, config.gameplay.midRoadDifficultyThreshold, 1f);
        config.gameplay.movingTileSpeed = Mathf.Clamp(config.gameplay.movingTileSpeed, 0.1f, 6f);
        config.gameplay.movingTileAmplitude = Mathf.Clamp(config.gameplay.movingTileAmplitude, 0f, 0.45f);
        config.gameplay.crumblingTileDelay = Mathf.Clamp(config.gameplay.crumblingTileDelay, 0.05f, 2f);
        config.gameplay.crumblingTileExitDelay = Mathf.Clamp(config.gameplay.crumblingTileExitDelay, 0.01f, 0.5f);
        config.gameplay.normalTileFallDelay = Mathf.Clamp(config.gameplay.normalTileFallDelay, 0.05f, 1f);
        config.gameplay.platformRecycleDelay = Mathf.Clamp(config.gameplay.platformRecycleDelay, 0.5f, 4f);
        config.gameplay.reviveCountdownStepSeconds = Mathf.Clamp(
            config.gameplay.reviveCountdownStepSeconds, 0.15f, 1.5f);
        config.gameplay.reviveCountdownGoSeconds = Mathf.Clamp(
            config.gameplay.reviveCountdownGoSeconds, 0.1f, 1f);
        if (config.gameplay.specialTileMinGap <= 0)
            config.gameplay.specialTileMinGap = defaults.gameplay.specialTileMinGap;
        config.gameplay.specialTileMinGap = Mathf.Clamp(config.gameplay.specialTileMinGap, 2, 12);
        config.gameplay.routeBranchesStartScore = Mathf.Max(0, config.gameplay.routeBranchesStartScore);
        config.gameplay.routeBranchChanceMin = Mathf.Clamp01(config.gameplay.routeBranchChanceMin);
        config.gameplay.routeBranchChanceMax = Mathf.Clamp(config.gameplay.routeBranchChanceMax, config.gameplay.routeBranchChanceMin, 1f);
        config.gameplay.routeBranchCooldownTiles = Mathf.Max(4, config.gameplay.routeBranchCooldownTiles);
        config.gameplay.routeBranchLength = Mathf.Clamp(config.gameplay.routeBranchLength, 2, 5);
        config.gameplay.riskRouteGemChance = Mathf.Clamp01(config.gameplay.riskRouteGemChance);
        config.gameplay.riskRouteHazardSpacing = Mathf.Clamp(config.gameplay.riskRouteHazardSpacing, 2, 8);
        // Firebase can tune this range without a new build. Individual reward
        // values below are always kept inside the configured economy limits.
        config.challenges.missionGemRewardMin = Mathf.Clamp(config.challenges.missionGemRewardMin, 1, 50);
        config.challenges.missionGemRewardMax = Mathf.Clamp(
            config.challenges.missionGemRewardMax, config.challenges.missionGemRewardMin, 50);
        int rewardMin = config.challenges.missionGemRewardMin;
        int rewardMax = config.challenges.missionGemRewardMax;
        config.challenges.dailyTargetReward = Mathf.Clamp(config.challenges.dailyTargetReward, rewardMin, rewardMax);
        config.challenges.dailyRunsReward = Mathf.Clamp(config.challenges.dailyRunsReward, rewardMin, rewardMax);
        config.challenges.dailyGemsReward = Mathf.Clamp(config.challenges.dailyGemsReward, rewardMin, rewardMax);
        config.challenges.dailyComboReward = Mathf.Clamp(config.challenges.dailyComboReward, rewardMin, rewardMax);
        config.challenges.dailyScoreReward = Mathf.Clamp(config.challenges.dailyScoreReward, rewardMin, rewardMax);
        int weeklyReward = Mathf.Clamp(config.challenges.weeklyMissionGemReward, 5, 50);
        config.challenges.weeklyMissionGemReward = weeklyReward;
        config.challenges.weeklyRunsReward = weeklyReward;
        config.challenges.weeklyScoreReward = weeklyReward;
        config.challenges.weeklyGemsReward = weeklyReward;
        config.challenges.weeklyComboReward = weeklyReward;
        config.challenges.weeklyBestReward = weeklyReward;
        config.challenges.missionScoreReward = Mathf.Clamp(config.challenges.missionScoreReward, rewardMin, rewardMax);
        config.challenges.missionComboReward = Mathf.Clamp(config.challenges.missionComboReward, rewardMin, rewardMax);
        config.challenges.missionGemsReward = Mathf.Clamp(config.challenges.missionGemsReward, rewardMin, rewardMax);
        config.challenges.missionRunsReward = Mathf.Clamp(config.challenges.missionRunsReward, rewardMin, rewardMax);
        config.challenges.missionTotalScoreReward = Mathf.Clamp(config.challenges.missionTotalScoreReward, rewardMin, rewardMax);
        config.challenges.achievementScore50Reward = Mathf.Clamp(config.challenges.achievementScore50Reward, rewardMin, rewardMax);
        config.challenges.achievementCombo10Reward = Mathf.Clamp(config.challenges.achievementCombo10Reward, rewardMin, rewardMax);
        config.challenges.achievementScore200Reward = Mathf.Clamp(config.challenges.achievementScore200Reward, rewardMin, rewardMax);
        if (config.gameplay.perfectHapticDurationMs < 20)
            config.gameplay.perfectHapticDurationMs = defaults.gameplay.perfectHapticDurationMs;
        if (config.gameplay.perfectHapticAmplitude < 70)
            config.gameplay.perfectHapticAmplitude = defaults.gameplay.perfectHapticAmplitude;
        config.gameplay.perfectHapticDurationMs = Mathf.Clamp(config.gameplay.perfectHapticDurationMs, 20, 80);
        config.gameplay.perfectHapticAmplitude = Mathf.Clamp(config.gameplay.perfectHapticAmplitude, 70, 255);
        config.ghost.sampleInterval = Mathf.Clamp(config.ghost.sampleInterval, 0.04f, 0.2f);
        config.ghost.maximumRecordedFrames = Mathf.Clamp(config.ghost.maximumRecordedFrames, 300, 20000);
        config.ghost.verticalOffset = Mathf.Clamp(config.ghost.verticalOffset, -0.5f, 0.5f);
        config.ghost.scaleMultiplier = Mathf.Clamp(config.ghost.scaleMultiplier, 0.5f, 1.5f);
        config.ghost.opacity = Mathf.Clamp(config.ghost.opacity, 0.02f, 0.8f);
        config.ghost.rimStrength = Mathf.Clamp01(config.ghost.rimStrength);
        config.ghost.rimPower = Mathf.Clamp(config.ghost.rimPower, 0.5f, 8f);
        config.ghost.faceAlphaFactor = Mathf.Clamp01(config.ghost.faceAlphaFactor);
        config.ghost.rimAlphaFactor = Mathf.Clamp(config.ghost.rimAlphaFactor, 0f, 2f);
        config.gameplay.specialTileVisuals ??= defaults.gameplay.specialTileVisuals;
        if (config.gameplay.specialTileVisuals.normalThemeColors == null || config.gameplay.specialTileVisuals.normalThemeColors.Length == 0)
            config.gameplay.specialTileVisuals.normalThemeColors = defaults.gameplay.specialTileVisuals.normalThemeColors;
        if (config.background.themes == null || config.background.themes.Length == 0)
            config.background.themes = defaults.background.themes;
        if (config.background.transitionAnimations == null || config.background.transitionAnimations.Length == 0)
            config.background.transitionAnimations = defaults.background.transitionAnimations;
        return config;
    }
}
