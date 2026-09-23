using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public sealed class FirebaseRemoteConfigBridge : MonoBehaviour
{
    private static FirebaseRemoteConfigBridge instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null || !ConfigManager.Current.network.firebaseRemoteConfigEnabled) return;
        GameObject host = new GameObject("Firebase Remote Config");
        instance = host.AddComponent<FirebaseRemoteConfigBridge>();
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
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(FetchRoutine());
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static IEnumerator FetchRoutine()
    {
        AndroidJavaObject remoteConfig = null;
        AndroidJavaObject task = null;
        try
        {
            using (AndroidJavaClass remoteConfigClass = new AndroidJavaClass("com.google.firebase.remoteconfig.FirebaseRemoteConfig"))
                remoteConfig = remoteConfigClass.CallStatic<AndroidJavaObject>("getInstance");

            using (AndroidJavaObject builder = new AndroidJavaObject(
                       "com.google.firebase.remoteconfig.FirebaseRemoteConfigSettings$Builder"))
            {
                long intervalSeconds = Math.Max(0, ConfigManager.Current.network.cacheLifetimeMinutes) * 60L;
                builder.Call<AndroidJavaObject>("setMinimumFetchIntervalInSeconds", intervalSeconds);
                using (AndroidJavaObject settings = builder.Call<AndroidJavaObject>("build"))
                    remoteConfig.Call<AndroidJavaObject>("setConfigSettingsAsync", settings)?.Dispose();
            }

            task = remoteConfig.Call<AndroidJavaObject>("fetchAndActivate");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Firebase Remote Config could not start: {exception.Message}");
            remoteConfig?.Dispose();
            GameAnalytics.RemoteConfigStatus("firebase_remote_config", false);
            yield break;
        }

        float timeoutAt = Time.realtimeSinceStartup + Mathf.Max(2f, ConfigManager.Current.network.requestTimeoutSeconds);
        while (Time.realtimeSinceStartup < timeoutAt)
        {
            bool complete;
            try { complete = task.Call<bool>("isComplete"); }
            catch { complete = true; }
            if (complete) break;
            yield return null;
        }

        bool success = false;
        string json = string.Empty;
        try
        {
            success = task.Call<bool>("isComplete") && task.Call<bool>("isSuccessful");
            if (success)
            {
                string key = ConfigManager.Current.network.firebaseRemoteConfigJsonKey;
                json = remoteConfig.Call<string>("getString", key);
                success = !string.IsNullOrWhiteSpace(json);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Firebase Remote Config fetch failed: {exception.Message}");
            success = false;
        }

        task?.Dispose();
        remoteConfig?.Dispose();
        bool applied = success && ConfigManager.TryApplyRemoteOverride(json, "firebase_remote_config");
        GameAnalytics.RemoteConfigStatus("firebase_remote_config", applied);
    }
#endif
}
