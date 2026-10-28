using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(-950)]
public sealed class NetworkConnectivityMonitor : MonoBehaviour
{
    private static NetworkConnectivityMonitor instance;
    private readonly List<Action<bool>> pendingCallbacks = new List<Action<bool>>();
    private bool probeInProgress;

    public static event Action<bool> ConnectivityChanged;
    public static bool IsConnected { get; private set; } = true;
    public static bool IsChecking => instance != null && instance.probeInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Network Connectivity Monitor");
        instance = host.AddComponent<NetworkConnectivityMonitor>();
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
        IsConnected = Application.internetReachability != NetworkReachability.NotReachable;
    }

    private void Start()
    {
        StartCoroutine(MonitorRoutine());
    }

    public static void CheckNow(Action<bool> completed = null)
    {
        EnsureInstance();
        instance.RequestCheck(completed);
    }

    private void RequestCheck(Action<bool> completed)
    {
        if (completed != null) pendingCallbacks.Add(completed);
        if (!probeInProgress) StartCoroutine(ProbeRoutine());
    }

    private IEnumerator MonitorRoutine()
    {
        while (true)
        {
            RequestCheck(null);
            while (probeInProgress) yield return null;
            float interval = Mathf.Clamp(
                ConfigManager.Current.network.connectivityCheckIntervalSeconds, 2f, 60f);
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private IEnumerator ProbeRoutine()
    {
        probeInProgress = true;
        bool connected = Application.internetReachability != NetworkReachability.NotReachable;

        if (connected)
        {
            NetworkConfig config = ConfigManager.Current.network;
            using (UnityWebRequest request = UnityWebRequest.Get(config.connectivityProbeUrl))
            {
                request.timeout = Mathf.CeilToInt(Mathf.Clamp(
                    config.connectivityProbeTimeoutSeconds, 2f, 15f));
                yield return request.SendWebRequest();
                connected = request.result == UnityWebRequest.Result.Success;
            }
        }

        bool changed = IsConnected != connected;
        IsConnected = connected;
        probeInProgress = false;

        if (changed) ConnectivityChanged?.Invoke(connected);

        if (pendingCallbacks.Count == 0) yield break;
        Action<bool>[] callbacks = pendingCallbacks.ToArray();
        pendingCallbacks.Clear();
        foreach (Action<bool> callback in callbacks)
        {
            try { callback?.Invoke(connected); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) RequestCheck(null);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
