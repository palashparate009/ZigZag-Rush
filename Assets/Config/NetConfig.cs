using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public static class NetConfig
{
    private const string JsonCacheKey = "NetConfig.Json";
    private const string CacheTimeKey = "NetConfig.UtcTicks";

    public static bool TryGetCached(int lifetimeMinutes, out string json)
    {
        json = PlayerPrefs.GetString(JsonCacheKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return false;
        if (lifetimeMinutes <= 0) return true;

        string ticksText = PlayerPrefs.GetString(CacheTimeKey, string.Empty);
        if (!long.TryParse(ticksText, out long ticks)) return false;
        DateTime savedAt = new DateTime(ticks, DateTimeKind.Utc);
        return DateTime.UtcNow - savedAt <= TimeSpan.FromMinutes(lifetimeMinutes);
    }

    public static IEnumerator Fetch(NetworkConfig settings, Action<bool, string> completed)
    {
        if (settings == null || string.IsNullOrWhiteSpace(settings.remoteConfigUrl))
        {
            completed?.Invoke(false, string.Empty);
            yield break;
        }

        if (!Uri.TryCreate(settings.remoteConfigUrl.Trim(), UriKind.Absolute, out Uri endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("Network config URL must be an absolute HTTPS URL.");
            completed?.Invoke(false, string.Empty);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint.AbsoluteUri))
        {
            request.timeout = Mathf.Max(1, settings.requestTimeoutSeconds);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success || string.IsNullOrWhiteSpace(request.downloadHandler.text))
            {
                Debug.LogWarning($"Network config download failed: {request.error}");
                completed?.Invoke(false, string.Empty);
                yield break;
            }

            string json = request.downloadHandler.text;
            PlayerPrefs.SetString(JsonCacheKey, json);
            PlayerPrefs.SetString(CacheTimeKey, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
            completed?.Invoke(true, json);
        }
    }
}
