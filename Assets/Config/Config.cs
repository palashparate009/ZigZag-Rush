using System;
using UnityEngine;

[Serializable]
public sealed class GameConfig
{
    public int version = 5;
    public NetworkConfig network = new NetworkConfig();
    public ServiceConfig services = new ServiceConfig();
    public AdsConfig ads = new AdsConfig();
    public CloudConfig cloud = new CloudConfig();
    public EconomyConfig economy = new EconomyConfig();
    public GameplayConfig gameplay = new GameplayConfig();
    public GhostConfig ghost = new GhostConfig();
    public BackgroundConfig background = new BackgroundConfig();
    public ChallengeConfig challenges = new ChallengeConfig();
}

[Serializable]
public sealed class NetworkConfig
{
    public string remoteConfigUrl = string.Empty;
    public int requestTimeoutSeconds = 8;
    public int cacheLifetimeMinutes = 60;
    public float loginTimeoutSeconds = 45f;
    public float playerProfileWaitSeconds = 3f;
    public bool firebaseRemoteConfigEnabled = true;
    public string firebaseRemoteConfigJsonKey = "game_config_json";
}

[Serializable]
public sealed class ServiceConfig
{
    public string packageName = "com.zigzagstudio.zigzagrush";
    public string playGamesApplicationId = "636986009425";
    public string playGamesWebClientId = "636986009425-q7600ertmrmo47aihohfajj57ojephut.apps.googleusercontent.com";
    public string mainLeaderboardId = "CgkI0c7M-sQSEAIQBQ";
    public string dailyLeaderboardId = "CgkI0c7M-sQSEAIQBg";
    public string privacyPolicyUrl = "https://palashparate009.github.io/zigzag-rush-privacy/";
}

[Serializable]
public sealed class AdsConfig
{
    public bool adsEnabled = true;
    public string androidAppKey = "27e8d6dbd";
    public string rewardedAdUnitId = "z2ymdwmbjvj6xc68";
    public string interstitialAdUnitId = "5idp7gb71pk5lwv6";
    public bool bannerAdsEnabled = true;
    public string bannerAdUnitId = "5h6pm2tvhfy7jvib";
    public string bannerPlacement = "Main_Menu_Banner";
    public string bannerPosition = "BottomCenter";
    public bool bannerUseAdaptiveSize = true;
    public bool bannerRespectSafeArea = true;
    public bool adMobEnabled = true;
    public string adMobAppId = "ca-app-pub-8757024569931584~3937763004";
    public string adMobBannerAdUnitId = "ca-app-pub-8757024569931584/4629484419";
    public float adLoadRetrySeconds = 15f;
    public int interstitialEveryRuns = 4;
    public bool certifiedCmpConfigured = true;
    public bool targetAudienceReviewed = true;
    public bool childDirected;
    public string revivePlacement = "Revive";
    public string doubleGemsPlacement = "Double_Gems";
    public string carUnlockPlacement = "Unlock_Car";
    public string gameOverPlacement = "Game_Over";
}

[Serializable]
public sealed class CloudConfig
{
    public string playersCollection = "players";
    public int schemaVersion = 6;
    public float saveDelaySeconds = 2f;
}

[Serializable]
public sealed class IapProductConfig
{
    public string productId;
    public int gems;
    public string fallbackPrice;
}

[Serializable]
public sealed class EconomyConfig
{
    public int dailyGiftGems = 10;
    public int rewardedStoreGems = 10;
    public string[] legacySkinNames = { "Crimson", "Ocean", "Lime", "Neon Pink", "Gold", "Violet" };
    public int[] legacySkinCosts = { 0, 50, 120, 250, 500, 900 };
    public string[] themeNames = { "Sunset", "Neon", "Space" };
    public int[] themeCosts = { 0, 200, 500 };
    public IapProductConfig[] iapProducts =
    {
        new IapProductConfig { productId = "gems_50", gems = 50, fallbackPrice = "₹30" },
        new IapProductConfig { productId = "gems_100", gems = 100, fallbackPrice = "₹70" },
        new IapProductConfig { productId = "gems_150", gems = 150, fallbackPrice = "₹130" },
        new IapProductConfig { productId = "gems_200", gems = 200, fallbackPrice = "₹180" }
    };
}

[Serializable]
public sealed class GameplayConfig
{
    public int targetFrameRate = 60;
    public float maximumDeltaTime = 0.1f;
    public float baseMoveSpeed = 8f;
    public float newPlayerMoveSpeed = 5.5f;
    public int newPlayerSpeedRunCount = 3;
    public float maximumDifficultySpeedMultiplier = 1.75f;
    public float perfectTurnWindow = 0.34f;
    public float newPlayerPerfectTurnWindow = 0.42f;
    public int difficultyScoreCap = 250;
    public int comboScoreStep = 5;
    public int maxComboMultiplier = 5;
    public int gemScoreMultiplier = 2;
    public float gemSoundVolume = 0.25f;
    public float speedBoostMultiplier = 1.35f;
    public float slowMotionMultiplier = 0.72f;
    public float specialEffectDuration = 3f;
    public float gemMagnetRadius = 9f;
    public int mysteryRewardMinInclusive = 3;
    public int mysteryRewardMaxExclusive = 9;
    public float specialTooltipDistance = 16f;
    public float reviveSafetyInset = 0.8f;
    public int reviveRunwayTiles = 5;
    public float perfectShakeDuration = 0.09f;
    public float perfectShakeIntensity = 0.07f;
    public int perfectHapticDurationMs = 24;
    public int perfectHapticAmplitude = 100;
    public int newBestHapticDurationMs = 42;
    public int newBestHapticAmplitude = 100;
    public float newBestSoundVolume = 0.8f;
    public float deathShakeDuration = 0.3f;
    public float deathShakeIntensity = 0.18f;
    public float menuMusicVolume = 0.55f;
    public float gameplayMusicVolume = 0.65f;
    public int specialTilesStartScore = 12;
    public int fullSpecialTilesScore = 35;
    public float specialTileChanceMin = 0.06f;
    public float specialTileChanceMax = 0.16f;
    public int specialTileMinGap = 4;
    public float diamondChanceMin = 0.12f;
    public float diamondChanceMax = 0.2f;
    public int earlyMaxStraightTiles = 5;
    public int midMaxStraightTiles = 4;
    public int lateMaxStraightTiles = 3;
    public float midRoadDifficultyThreshold = 0.35f;
    public float lateRoadDifficultyThreshold = 0.7f;
    public float movingTileSpeed = 2f;
    public float movingTileAmplitude = 0.22f;
    public float crumblingTileDelay = 0.42f;
    public float crumblingTileExitDelay = 0.03f;
    public float normalTileFallDelay = 0.2f;
    public float platformRecycleDelay = 1.2f;
    public float reviveCountdownStepSeconds = 0.42f;
    public float reviveCountdownGoSeconds = 0.25f;
    public bool routeBranchesEnabled = true;
    public int routeBranchesStartScore = 25;
    public float routeBranchChanceMin = 0.015f;
    public float routeBranchChanceMax = 0.045f;
    public int routeBranchCooldownTiles = 18;
    public int routeBranchLength = 3;
    public float riskRouteGemChance = 0.85f;
    public int riskRouteHazardSpacing = 3;
    public bool smoothVisualTurns = true;
    public float visualTurnDuration = 0.16f;
    public string visualTurnEase = "OutCubic";
    public int[] scoreMilestones = { 10, 25, 50, 100, 200, 500 };
    public SpecialTileVisualConfig specialTileVisuals = new SpecialTileVisualConfig();
}

[Serializable]
public sealed class GhostConfig
{
    public bool enabled = true;
    public float sampleInterval = 0.08f;
    public int maximumRecordedFrames = 7500;
    public float verticalOffset = 0.08f;
    public float scaleMultiplier = 1f;
    public float opacity = 0.28f;
    public bool preserveOriginalColors = true;
    public Color tintColor = Color.white;
    public Color rimColor = Color.white;
    public float rimStrength = 0.2f;
    public float rimPower = 2.2f;
    public float faceAlphaFactor = 0.42f;
    public float rimAlphaFactor = 0.65f;
}

[Serializable]
public sealed class SpecialTileVisualConfig
{
    public Color[] normalThemeColors =
    {
        new Color(0.98f, 0.95f, 0.88f, 1f),
        new Color(0.08f, 0.13f, 0.23f, 1f),
        new Color(0.22f, 0.18f, 0.32f, 1f)
    };
    public Color speedBoostColor = new Color(1f, 0.45f, 0.05f, 1f);
    public Color slowMotionColor = new Color(0.15f, 0.8f, 1f, 1f);
    public Color shieldColor = new Color(0.2f, 1f, 0.4f, 1f);
    public Color gemMagnetColor = new Color(0.85f, 0.2f, 1f, 1f);
    public Color crumblingColor = new Color(0.75f, 0.18f, 0.12f, 1f);
    public Color movingColor = new Color(1f, 0.8f, 0.1f, 1f);
    public Color mysteryColor = new Color(1f, 0.15f, 0.65f, 1f);
    public Color activationTextColor = Color.black;

    public Color ResolveTileColor(int tileType, int theme)
    {
        switch (tileType)
        {
            case 1: return speedBoostColor;
            case 2: return slowMotionColor;
            case 3: return shieldColor;
            case 4: return gemMagnetColor;
            case 5: return crumblingColor;
            case 6: return movingColor;
            case 7: return mysteryColor;
            default:
                if (normalThemeColors == null || normalThemeColors.Length == 0) return Color.white;
                return normalThemeColors[Mathf.Clamp(theme, 0, normalThemeColors.Length - 1)];
        }
    }

    public Color ResolveActivationTextColor()
    {
        return activationTextColor;
    }
}

[Serializable]
public sealed class BackgroundThemeConfig
{
    public string[] topColors = Array.Empty<string>();
    public string[] middleColors = Array.Empty<string>();
    public string[] bottomColors = Array.Empty<string>();
    public string[] accentColors = Array.Empty<string>();
}

[Serializable]
public sealed class BackgroundTransitionOption
{
    public string name;
    public int style;
    public bool enabled = true;
}

[Serializable]
public sealed class BackgroundConfig
{
    public float transitionDuration = 3f;
    public float transitionSoftness = 0.13f;
    public float transitionCrossFade = 0.24f;
    public float gameplayColorInterval = 10f;
    public float motionSpeed = 0.28f;
    public float effectIntensity = 0.42f;
    public float menuMoodInterval = 9f;
    public float pulseFadeSpeed = 0.65f;
    public int comboForMaximumPulse = 12;
    public BackgroundTransitionOption[] transitionAnimations =
    {
        Transition("Smooth Fade", 0),
        Transition("Vertical Wipe Up", 1),
        Transition("Vertical Wipe Down", 2),
        Transition("Horizontal Wipe Left", 3),
        Transition("Horizontal Wipe Right", 4),
        Transition("Radial Bloom", 5),
        Transition("Radial Collapse", 6),
        Transition("Diagonal Rise", 7),
        Transition("Diagonal Fall", 8),
        Transition("Clock Sweep", 9),
        Transition("Liquid Wave", 10),
        Transition("Twin Wave", 11),
        Transition("Spiral Reveal", 12),
        Transition("Diamond Reveal", 13),
        Transition("Venetian Bands", 14),
        Transition("Checker Flow", 15),
        Transition("Soft Noise Dissolve", 16),
        Transition("Aurora Sweep", 17),
        Transition("Elliptical Iris", 18),
        Transition("Ripple Ring", 19)
    };
    public BackgroundThemeConfig[] themes =
    {
        new BackgroundThemeConfig
        {
            topColors = new[] { "7048e8", "e73c78", "e85d35", "d99a18", "008fb3", "159a8c", "55a85c", "3478d4", "d52b78", "8449c6" },
            middleColors = new[] { "a855f7", "ff6f91", "ff8c42", "f9c74f", "20c5e8", "39c6ad", "80cf72", "55a7ff", "ff5da2", "bd6cff" },
            bottomColors = new[] { "5b3bc4", "bd3b72", "c6533e", "b88724", "0093a8", "249c89", "4c9e59", "3575c4", "b72c68", "7142ba" },
            accentColors = new[] { "35f2ff", "ffe66d", "5cffb1", "ff4d6d", "ffd60a", "ff70a6", "b06cff", "ffb703", "4dffdb", "ffe169" }
        },
        new BackgroundThemeConfig
        {
            topColors = new[] { "1268e8", "009ac7", "00a884", "39a844", "9cab16", "d68b14", "e84b20", "d62976", "7a35c7", "455ed1" },
            middleColors = new[] { "3694ff", "20c9e8", "20c997", "72cf62", "c9d934", "ffbd3e", "ff7145", "f04e9b", "a963f2", "6f8cff" },
            bottomColors = new[] { "1556ac", "087b98", "087d67", "337c3b", "778617", "a46b19", "b63e24", "a92b68", "6031a0", "374fa6" },
            accentColors = new[] { "72f6ff", "b4ff39", "ffe84a", "42f5c5", "ff5bd7", "53e0ff", "8cff66", "ffd23f", "3fffe2", "ff7a59" }
        },
        new BackgroundThemeConfig
        {
            topColors = new[] { "5637c8", "315fc9", "8a3bb5", "c63686", "b94750", "1689b5", "168f78", "a17817", "7352a6", "43556f" },
            middleColors = new[] { "8c59f2", "4f8df7", "c45ce0", "f05bac", "ee6a63", "35bce0", "39c39b", "d6ac35", "a87bd6", "6e87a8" },
            bottomColors = new[] { "422ca1", "294d9f", "713092", "9d2e6d", "983b43", "14718f", "14745f", "806119", "5e4389", "35465e" },
            accentColors = new[] { "44f0ff", "ff68cf", "ffe45c", "58ffb1", "7ee8ff", "ff9f43", "c77dff", "5ef2d6", "ff6b6b", "a8ff60" }
        }
    };
    public string speedBoostAccent = "ff521f";
    public string slowMotionAccent = "33bfff";
    public string shieldAccent = "33ff8c";
    public string gemMagnetAccent = "e640ff";
    public string mysteryAccent = "ffd12e";
    public string gameOverAccent = "ff2e40";

    private static BackgroundTransitionOption Transition(string name, int style)
    {
        return new BackgroundTransitionOption { name = name, style = style, enabled = true };
    }
}

[Serializable]
public sealed class ChallengeConfig
{
    public int dailyTargetMin = 45;
    public int dailyTargetRange = 56;
    public int dailyTargetReward = 10;
    public int dailyRunsTarget = 3;
    public int dailyRunsReward = 5;
    public int dailyGemsTarget = 15;
    public int dailyGemsReward = 5;
    public int dailyComboTarget = 6;
    public int dailyComboReward = 8;
    public int dailyScoreTarget = 200;
    public int dailyScoreReward = 12;
    public int weeklyRunsTarget = 15;
    public int weeklyRunsReward = 20;
    public int weeklyScoreTarget = 1000;
    public int weeklyScoreReward = 25;
    public int weeklyGemsTarget = 75;
    public int weeklyGemsReward = 25;
    public int weeklyComboTarget = 12;
    public int weeklyComboReward = 30;
    public int weeklyBestTarget = 150;
    public int weeklyBestReward = 35;
    public int missionScoreTarget = 60;
    public int missionScoreReward = 5;
    public int missionComboTarget = 8;
    public int missionComboReward = 7;
    public int missionGemsTarget = 15;
    public int missionGemsReward = 5;
    public int missionRunsTarget = 5;
    public int missionRunsReward = 8;
    public int missionTotalScoreTarget = 300;
    public int missionTotalScoreReward = 10;
    public int achievementScore50Target = 50;
    public int achievementScore50Reward = 25;
    public int achievementCombo10Target = 10;
    public int achievementCombo10Reward = 35;
    public int achievementScore200Target = 200;
    public int achievementScore200Reward = 75;
}
