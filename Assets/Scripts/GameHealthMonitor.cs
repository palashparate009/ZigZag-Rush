using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1100)]
public sealed class GameHealthMonitor : MonoBehaviour
{
    private readonly struct PendingError
    {
        public readonly string Message;
        public readonly string StackTrace;

        public PendingError(string message, string stackTrace)
        {
            Message = message;
            StackTrace = stackTrace;
        }
    }

    private static readonly ConcurrentQueue<PendingError> PendingErrors = new ConcurrentQueue<PendingError>();
    private static readonly Dictionary<string, float> TraceStartTimes = new Dictionary<string, float>();
    private static GameHealthMonitor instance;
    private float nextStallReportTime;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject crashlytics;
    private static AndroidJavaObject performance;
    private static readonly Dictionary<string, AndroidJavaObject> NativeTraces = new Dictionary<string, AndroidJavaObject>();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Game Health Monitor");
        instance = host.AddComponent<GameHealthMonitor>();
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
        Application.logMessageReceivedThreaded += HandleLog;
        InitializeNativeServices();
        StartTrace("app_start_to_game_ready");
    }

    private void Update()
    {
        while (PendingErrors.TryDequeue(out PendingError error))
            RecordNonFatal(error.Message, error.StackTrace);

        if (Time.unscaledDeltaTime < 0.15f || Time.unscaledTime < nextStallReportTime) return;
        nextStallReportTime = Time.unscaledTime + 30f;
        SetKey("last_frame_stall_ms", Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f).ToString());
        GameAnalytics.PerformanceTrace("frame_stall", Time.unscaledDeltaTime, false);
    }

    public static void StartTrace(string traceName)
    {
        if (string.IsNullOrWhiteSpace(traceName)) return;
        Bootstrap();
        TraceStartTimes[traceName] = Time.realtimeSinceStartup;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (performance == null) return;
            if (NativeTraces.TryGetValue(traceName, out AndroidJavaObject previous))
            {
                previous?.Dispose();
                NativeTraces.Remove(traceName);
            }

            AndroidJavaObject trace = performance.Call<AndroidJavaObject>("newTrace", SanitizeName(traceName));
            trace?.Call("start");
            if (trace != null) NativeTraces[traceName] = trace;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Firebase Performance trace could not start: {exception.Message}");
        }
#endif
    }

    public static void StopTrace(string traceName, bool success = true)
    {
        float elapsed = 0f;
        if (TraceStartTimes.TryGetValue(traceName, out float startedAt))
        {
            elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - startedAt);
            TraceStartTimes.Remove(traceName);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (NativeTraces.TryGetValue(traceName, out AndroidJavaObject trace))
            {
                trace.Call("putAttribute", "success", success ? "true" : "false");
                trace.Call("stop");
                trace.Dispose();
                NativeTraces.Remove(traceName);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Firebase Performance trace could not stop: {exception.Message}");
        }
#endif

        GameAnalytics.PerformanceTrace(traceName, elapsed, success);
    }

    public static void SetKey(string key, string value)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            crashlytics?.Call("setCustomKey", SanitizeName(key), value ?? string.Empty);
        }
        catch { }
#endif
    }

    public static void RecordNonFatal(string message, string stackTrace = "")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (crashlytics == null) return;
            string detail = string.IsNullOrWhiteSpace(stackTrace) ? message : $"{message}\n{stackTrace}";
            using (AndroidJavaObject exception = new AndroidJavaObject("java.lang.Exception", detail))
                crashlytics.Call("recordException", exception);
        }
        catch { }
#else
        if (!string.IsNullOrWhiteSpace(message)) Debug.Log($"[Crash preview] {message}");
#endif
    }

    private static void InitializeNativeServices()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass crashClass = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
                crashlytics = crashClass.CallStatic<AndroidJavaObject>("getInstance");
            crashlytics?.Call("setCrashlyticsCollectionEnabled", true);
            SetKey("app_version", Application.version);
            SetKey("unity_version", Application.unityVersion);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Firebase Crashlytics is unavailable: {exception.Message}");
        }

        try
        {
            using (AndroidJavaClass performanceClass = new AndroidJavaClass("com.google.firebase.perf.FirebasePerformance"))
                performance = performanceClass.CallStatic<AndroidJavaObject>("getInstance");
            performance?.Call("setPerformanceCollectionEnabled", true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Firebase Performance is unavailable: {exception.Message}");
        }
#endif
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (condition != null && condition.StartsWith("Firebase Crashlytics is unavailable", StringComparison.Ordinal)) return;
        PendingErrors.Enqueue(new PendingError(condition ?? "Unknown Unity error", stackTrace ?? string.Empty));
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        string result = value.Trim().Replace(' ', '_').Replace('-', '_');
        return result.Length <= 80 ? result : result.Substring(0, 80);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        Application.logMessageReceivedThreaded -= HandleLog;
#if UNITY_ANDROID && !UNITY_EDITOR
        foreach (AndroidJavaObject trace in NativeTraces.Values) trace?.Dispose();
        NativeTraces.Clear();
        crashlytics?.Dispose();
        performance?.Dispose();
        crashlytics = null;
        performance = null;
#endif
        instance = null;
    }
}
