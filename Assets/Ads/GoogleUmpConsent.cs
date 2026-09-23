using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1050)]
public sealed class GoogleUmpConsent : MonoBehaviour
{
    private static GoogleUmpConsent instance;
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private readonly List<Action<bool>> readyCallbacks = new List<Action<bool>>();
    private bool completed;
    private bool canRequestAds;
    private bool privacyOptionsRequired;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject activity;
    private AndroidJavaObject consentInformation;
    private ConsentInfoSuccessProxy consentInfoSuccessProxy;
    private ConsentInfoFailureProxy consentInfoFailureProxy;
    private ConsentFormDismissedProxy formDismissedProxy;
    private PrivacyFormDismissedProxy privacyFormDismissedProxy;
#endif

    public static bool PrivacyOptionsRequired => instance != null && instance.privacyOptionsRequired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Google UMP Consent");
        instance = host.AddComponent<GoogleUmpConsent>();
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
        if (!AdConfiguration.AdsEnabled)
        {
            Complete(true);
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        BeginAndroidConsentFlow();
#else
        Complete(true);
#endif
    }

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action)) action?.Invoke();
    }

    public static void EnsureReady(Action<bool> callback)
    {
        Bootstrap();
        if (instance.completed)
        {
            callback?.Invoke(instance.canRequestAds);
            return;
        }

        if (callback != null) instance.readyCallbacks.Add(callback);
    }

    public static void ShowPrivacyOptions(Action<bool> completedCallback)
    {
        Bootstrap();
#if UNITY_ANDROID && !UNITY_EDITOR
        instance.ShowAndroidPrivacyOptions(completedCallback);
#else
        completedCallback?.Invoke(false);
#endif
    }

    private void Complete(bool adsAllowed)
    {
        completed = true;
        canRequestAds = adsAllowed;
        Action<bool>[] callbacks = readyCallbacks.ToArray();
        readyCallbacks.Clear();
        foreach (Action<bool> callback in callbacks) callback?.Invoke(canRequestAds);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void BeginAndroidConsentFlow()
    {
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using (AndroidJavaClass ump = new AndroidJavaClass("com.google.android.ump.UserMessagingPlatform"))
                consentInformation = ump.CallStatic<AndroidJavaObject>("getConsentInformation", activity);

            AndroidJavaObject builder = new AndroidJavaObject(
                "com.google.android.ump.ConsentRequestParameters$Builder");
            AndroidJavaObject parameters = builder.Call<AndroidJavaObject>("build");
            builder.Dispose();

            consentInfoSuccessProxy = new ConsentInfoSuccessProxy(this);
            consentInfoFailureProxy = new ConsentInfoFailureProxy(this);
            RunOnAndroidUiThread(() =>
            {
                consentInformation.Call("requestConsentInfoUpdate", activity, parameters,
                    consentInfoSuccessProxy, consentInfoFailureProxy);
                parameters.Dispose();
            });
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Google UMP consent could not start; ads remain disabled: {exception.Message}");
            Complete(false);
        }
    }

    private void LoadAndShowRequiredForm()
    {
        try
        {
            formDismissedProxy = new ConsentFormDismissedProxy(this);
            RunOnAndroidUiThread(() =>
            {
                using (AndroidJavaClass ump = new AndroidJavaClass("com.google.android.ump.UserMessagingPlatform"))
                    ump.CallStatic("loadAndShowConsentFormIfRequired", activity, formDismissedProxy);
            });
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Google UMP form could not be shown: {exception.Message}");
            RefreshConsentState();
        }
    }

    private void RefreshConsentState(string errorMessage = "")
    {
        try
        {
            canRequestAds = consentInformation != null && consentInformation.Call<bool>("canRequestAds");
        }
        catch (Exception exception)
        {
            errorMessage = string.IsNullOrWhiteSpace(errorMessage) ? exception.Message : errorMessage;
            canRequestAds = false;
        }

        // UMP 4 returns PrivacyOptionsRequirementStatus as a Java enum, not an int.
        // Read it independently so a privacy-button lookup can never override a
        // successful canRequestAds() result and disable all ad formats.
        privacyOptionsRequired = ReadPrivacyOptionsRequired();

        if (!string.IsNullOrWhiteSpace(errorMessage))
            Debug.LogWarning($"Google UMP consent status: {errorMessage}");
        Debug.Log($"Google UMP ready. Ads allowed: {canRequestAds}; privacy options required: {privacyOptionsRequired}.");
        Complete(canRequestAds);
    }

    private bool ReadPrivacyOptionsRequired()
    {
        if (consentInformation == null) return false;

        try
        {
            using (AndroidJavaObject status = consentInformation.Call<AndroidJavaObject>(
                       "getPrivacyOptionsRequirementStatus"))
            {
                if (status == null) return false;

                string statusName;
                try { statusName = status.Call<string>("name"); }
                catch { statusName = status.Call<string>("toString"); }

                return string.Equals(statusName, "REQUIRED", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Google UMP privacy-options status could not be read: {exception.Message}");
            return false;
        }
    }

    private void ShowAndroidPrivacyOptions(Action<bool> completedCallback)
    {
        if (!completed || !privacyOptionsRequired || activity == null)
        {
            completedCallback?.Invoke(false);
            return;
        }

        privacyFormDismissedProxy = new PrivacyFormDismissedProxy(this, completedCallback);
        RunOnAndroidUiThread(() =>
        {
            using (AndroidJavaClass ump = new AndroidJavaClass("com.google.android.ump.UserMessagingPlatform"))
                ump.CallStatic("showPrivacyOptionsForm", activity, privacyFormDismissedProxy);
        });
    }

    private void RunOnAndroidUiThread(Action action)
    {
        activity.Call("runOnUiThread", new AndroidJavaRunnable(action));
    }

    private void Post(Action action)
    {
        if (action != null) mainThreadActions.Enqueue(action);
    }

    private static string ReadFormError(AndroidJavaObject error)
    {
        if (error == null) return string.Empty;
        try { return error.Call<string>("getMessage") ?? "Unknown UMP error"; }
        catch { return "Unknown UMP error"; }
    }

    private sealed class ConsentInfoSuccessProxy : AndroidJavaProxy
    {
        private readonly GoogleUmpConsent owner;
        public ConsentInfoSuccessProxy(GoogleUmpConsent owner)
            : base("com.google.android.ump.ConsentInformation$OnConsentInfoUpdateSuccessListener") => this.owner = owner;
        public void onConsentInfoUpdateSuccess() => owner.Post(owner.LoadAndShowRequiredForm);
    }

    private sealed class ConsentInfoFailureProxy : AndroidJavaProxy
    {
        private readonly GoogleUmpConsent owner;
        public ConsentInfoFailureProxy(GoogleUmpConsent owner)
            : base("com.google.android.ump.ConsentInformation$OnConsentInfoUpdateFailureListener") => this.owner = owner;
        public void onConsentInfoUpdateFailure(AndroidJavaObject error)
        {
            string message = ReadFormError(error);
            owner.Post(() => owner.RefreshConsentState(message));
        }
    }

    private sealed class ConsentFormDismissedProxy : AndroidJavaProxy
    {
        private readonly GoogleUmpConsent owner;
        public ConsentFormDismissedProxy(GoogleUmpConsent owner)
            : base("com.google.android.ump.ConsentForm$OnConsentFormDismissedListener") => this.owner = owner;
        public void onConsentFormDismissed(AndroidJavaObject error)
        {
            string message = ReadFormError(error);
            owner.Post(() => owner.RefreshConsentState(message));
        }
    }

    private sealed class PrivacyFormDismissedProxy : AndroidJavaProxy
    {
        private readonly GoogleUmpConsent owner;
        private readonly Action<bool> callback;
        public PrivacyFormDismissedProxy(GoogleUmpConsent owner, Action<bool> callback)
            : base("com.google.android.ump.ConsentForm$OnConsentFormDismissedListener")
        {
            this.owner = owner;
            this.callback = callback;
        }

        public void onConsentFormDismissed(AndroidJavaObject error)
        {
            string message = ReadFormError(error);
            owner.Post(() =>
            {
                owner.RefreshConsentState(message);
                callback?.Invoke(string.IsNullOrEmpty(message));
            });
        }
    }
#endif

    private void OnDestroy()
    {
        if (instance != this) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        consentInformation?.Dispose();
        activity?.Dispose();
#endif
        instance = null;
    }
}
