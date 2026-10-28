using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class GameAnalytics : MonoBehaviour
{
    private readonly struct PendingEvent
    {
        public readonly string Name;
        public readonly Parameter[] Parameters;

        public PendingEvent(string name, Parameter[] parameters)
        {
            Name = name;
            Parameters = parameters;
        }
    }

    private const string ConsentKey = "AnalyticsConsent";
    private const int MaxPendingEvents = 64;
    private const string HasLaunchedKey = "AnalyticsHasLaunched";
    private static readonly Queue<PendingEvent> PendingEvents = new Queue<PendingEvent>();
    private static GameAnalytics instance;

    private FirebaseApp app;
    private bool ready;
    private bool collectionEnabled;
    private bool appStartLogged;
    private float appStartRealtime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Firebase Analytics");
        instance = host.AddComponent<GameAnalytics>();
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
        appStartRealtime = Time.realtimeSinceStartup;
        collectionEnabled = PlayerPrefs.GetInt(ConsentKey, 0) == 1;

#if UNITY_EDITOR
        ready = true;
        Debug.Log("Firebase Analytics is disabled in the Unity Editor to keep production reports clean.");
        LogApplicationStarted();
#else
        FirebaseBootstrap.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogWarning($"Firebase dependency check failed: {task.Exception?.GetBaseException().Message}");
                return;
            }

            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning($"Firebase dependencies are unavailable: {task.Result}");
                return;
            }

            app = FirebaseApp.DefaultInstance;
            ready = true;
            ApplyCollectionSetting();
            LogApplicationStarted();
        });
#endif
    }

    private void OnApplicationPause(bool paused)
    {
        Log(paused ? "app_background" : "app_foreground",
            new Parameter("session_seconds", Math.Max(0d, Time.realtimeSinceStartup - appStartRealtime)));
    }

    private void OnApplicationQuit()
    {
        Log("app_quit",
            new Parameter("session_seconds", Math.Max(0d, Time.realtimeSinceStartup - appStartRealtime)));
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        app = null;
    }

    public static void SetConsent(bool enabled)
    {
        PlayerPrefs.SetInt(ConsentKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        EnsureInstance();
        instance.collectionEnabled = enabled;

        if (!enabled) PendingEvents.Clear();
        if (instance.ready) instance.ApplyCollectionSetting();
        if (enabled)
        {
            instance.LogApplicationStarted();
            Log("privacy_choice", new Parameter("analytics", "enabled"));
        }
    }

    public static void ButtonClicked(string buttonName, string screenName)
    {
        Log("button_click",
            new Parameter("button_name", SafeValue(buttonName)),
            new Parameter("screen_name", SafeValue(screenName)));
    }

    public static void ScreenViewed(string screenName)
    {
        Log("screen_view", new Parameter("screen_name", SafeValue(screenName)));
    }

    public static void RunStarted(bool daily, int skinIndex, int themeIndex)
    {
        SetUserProperty("selected_skin", skinIndex.ToString());
        SetUserProperty("selected_theme", themeIndex.ToString());
        Log("run_start",
            new Parameter("run_type", daily ? "daily" : "standard"));
    }

    public static void RunFailed(int score, int runGems, int bestCombo, bool daily, int deathNumber,
        float durationSeconds, int turns, int perfectTurns, int diamonds, int specialTiles)
    {
        Log("run_failed",
            new Parameter("score", (long)Mathf.Max(0, score)),
            new Parameter("run_gems", (long)Mathf.Max(0, runGems)),
            new Parameter("best_combo", (long)Mathf.Max(0, bestCombo)),
            new Parameter("run_type", daily ? "daily" : "standard"),
            new Parameter("death_number", (long)Mathf.Max(1, deathNumber)),
            new Parameter("duration_seconds", Math.Max(0d, durationSeconds)),
            new Parameter("turns", (long)Mathf.Max(0, turns)),
            new Parameter("perfect_turns", (long)Mathf.Max(0, perfectTurns)),
            new Parameter("diamonds", (long)Mathf.Max(0, diamonds)),
            new Parameter("special_tiles", (long)Mathf.Max(0, specialTiles)));
    }

    public static void RunMilestone(int score, bool daily, float durationSeconds)
    {
        Log("run_milestone",
            new Parameter("score", (long)Mathf.Max(0, score)),
            new Parameter("run_type", daily ? "daily" : "standard"),
            new Parameter("duration_seconds", Math.Max(0d, durationSeconds)));
    }

    public static void RunAbandoned(int score, int runGems, bool daily, float durationSeconds, string reason)
    {
        Log("run_abandoned",
            new Parameter("score", (long)Mathf.Max(0, score)),
            new Parameter("run_gems", (long)Mathf.Max(0, runGems)),
            new Parameter("run_type", daily ? "daily" : "standard"),
            new Parameter("duration_seconds", Math.Max(0d, durationSeconds)),
            new Parameter("reason", SafeValue(reason)));
    }

    public static void GamePaused(bool paused, int score)
    {
        Log(paused ? "game_paused" : "game_resumed",
            new Parameter("score", (long)Mathf.Max(0, score)));
    }

    public static void SpecialTileUsed(string tileType)
    {
        Log("special_tile", new Parameter("tile_type", SafeValue(tileType)));
    }

    public static void SettingChanged(string settingName, bool enabled)
    {
        Log("setting_changed",
            new Parameter("setting_name", SafeValue(settingName)),
            new Parameter("enabled", enabled ? "true" : "false"));
    }

    public static void CosmeticResult(string cosmeticType, string itemName, string result, int cost)
    {
        Log("cosmetic_action",
            new Parameter("cosmetic_type", SafeValue(cosmeticType)),
            new Parameter("item_name", SafeValue(itemName)),
            new Parameter("result", SafeValue(result)),
            new Parameter("cost", (long)Mathf.Max(0, cost)));
    }

    public static void StoreStatus(string status, int productCount = 0)
    {
        Log("store_status",
            new Parameter("status", SafeValue(status)),
            new Parameter("product_count", (long)Mathf.Max(0, productCount)));
    }

    public static void CloudSaveStatus(string status)
    {
        Log("cloud_save_status", new Parameter("status", SafeValue(status)));
    }

    public static void SignInStep(string step, string result, float elapsedSeconds, bool automatic)
    {
        Log("sign_in_step",
            new Parameter("step", SafeValue(step)),
            new Parameter("result", SafeValue(result)),
            new Parameter("elapsed_ms", (long)Mathf.Max(0, Mathf.RoundToInt(elapsedSeconds * 1000f))),
            new Parameter("automatic", automatic ? "true" : "false"));
    }

    public static void LeaderboardAction(string action, string board, bool success)
    {
        Log("leaderboard_action",
            new Parameter("action", SafeValue(action)),
            new Parameter("board", SafeValue(board)),
            new Parameter("success", success ? "true" : "false"));
    }

    public static void RemoteConfigStatus(string source, bool success)
    {
        Log("remote_config_status",
            new Parameter("source", SafeValue(source)),
            new Parameter("success", success ? "true" : "false"));
    }

    public static void PerformanceTrace(string traceName, float elapsedSeconds, bool success)
    {
        Log("performance_trace",
            new Parameter("trace_name", SafeValue(traceName)),
            new Parameter("elapsed_ms", (long)Mathf.Max(0, Mathf.RoundToInt(elapsedSeconds * 1000f))),
            new Parameter("success", success ? "true" : "false"));
    }

    public static void PersonalizedObjectiveShown(string objectiveType)
    {
        Log("post_run_objective",
            new Parameter("objective_type", SafeValue(objectiveType)));
    }

    public static void RouteBranchChosen(bool risky)
    {
        Log("route_branch_chosen",
            new Parameter("route", risky ? "risk" : "safe"));
    }

    public static void CarMasteryAdvanced(string carId, int level, int xp)
    {
        Log("car_mastery_advanced",
            new Parameter("car_id", SafeValue(carId)),
            new Parameter("level", (long)Mathf.Max(1, level)),
            new Parameter("xp", (long)Mathf.Max(0, xp)));
    }

    public static void TutorialCompleted()
    {
        Log("tutorial_complete");
    }

    public static void ProgressRewardUnlocked(string rewardDescription)
    {
        Log("progress_reward",
            new Parameter("reward", SafeValue(rewardDescription)));
    }

    public static void RewardedAdRequested(string placement)
    {
        Log("rewarded_ad_request", new Parameter("placement", SafeValue(placement)));
    }

    public static void RewardedAdResult(string placement, bool rewarded)
    {
        Log("rewarded_ad_result",
            new Parameter("placement", SafeValue(placement)),
            new Parameter("result", rewarded ? "rewarded" : "unavailable_or_closed"));
    }

    public static void InterstitialRequested(string placement)
    {
        Log("interstitial_request", new Parameter("placement", SafeValue(placement)));
    }

    public static void GameplayReward(string rewardName, int amount)
    {
        Log("gameplay_reward",
            new Parameter("reward_name", SafeValue(rewardName)),
            new Parameter("amount", (long)Mathf.Max(0, amount)));
    }

    public static void PurchaseStarted(string productId)
    {
        Log("begin_checkout", new Parameter("item_id", SafeValue(productId)));
    }

    public static void PurchaseCompleted(string productId, int gems, double value, string currency)
    {
        List<Parameter> parameters = new List<Parameter>
        {
            new Parameter("item_id", SafeValue(productId)),
            new Parameter("gem_amount", (long)Mathf.Max(0, gems))
        };
        if (value > 0d) parameters.Add(new Parameter("value", value));
        if (!string.IsNullOrWhiteSpace(currency)) parameters.Add(new Parameter("currency", SafeValue(currency)));
        Log("purchase", parameters.ToArray());
    }

    public static void PurchaseFailed(string productId, string result)
    {
        Log("purchase_failed",
            new Parameter("item_id", SafeValue(productId)),
            new Parameter("result", SafeValue(result)));
    }

    private static void SetUserProperty(string name, string value)
    {
        if (PlayerPrefs.GetInt(ConsentKey, 0) != 1) return;
        EnsureInstance();
#if !UNITY_EDITOR
        if (instance.ready && instance.collectionEnabled)
            FirebaseAnalytics.SetUserProperty(name, SafeValue(value));
#endif
    }

    private static void Log(string eventName, params Parameter[] parameters)
    {
        if (PlayerPrefs.GetInt(ConsentKey, 0) != 1) return;
        EnsureInstance();

#if UNITY_EDITOR
        Debug.Log($"[Analytics preview] {eventName}");
#else
        if (!instance.ready)
        {
            if (PendingEvents.Count < MaxPendingEvents)
                PendingEvents.Enqueue(new PendingEvent(eventName, parameters));
            return;
        }

        if (!instance.collectionEnabled) return;
        FirebaseAnalytics.LogEvent(eventName, parameters);
#endif
    }

    private void ApplyCollectionSetting()
    {
#if !UNITY_EDITOR
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(collectionEnabled);
        if (!collectionEnabled) return;

        while (PendingEvents.Count > 0)
        {
            PendingEvent pending = PendingEvents.Dequeue();
            FirebaseAnalytics.LogEvent(pending.Name, pending.Parameters);
        }
#endif
    }

    private void LogApplicationStarted()
    {
        if (appStartLogged || !collectionEnabled) return;
        appStartLogged = true;
        bool firstLaunch = PlayerPrefs.GetInt(HasLaunchedKey, 0) == 0;
#if !UNITY_EDITOR
        PlayerPrefs.SetInt(HasLaunchedKey, 1);
        PlayerPrefs.Save();
#endif
        Log("app_start",
            new Parameter("app_version", SafeValue(Application.version)),
            new Parameter("platform", SafeValue(Application.platform.ToString())),
            new Parameter("first_launch", firstLaunch ? "true" : "false"));
    }

    private static string SafeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        value = value.Trim();
        return value.Length <= 80 ? value : value.Substring(0, 80);
    }
}
