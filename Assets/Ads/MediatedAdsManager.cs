using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if LEVELPLAY_SDK
using Unity.Services.LevelPlay;
#endif

public class MediatedAdsManager : MonoBehaviour
{
    public event Action<float> BannerInsetChanged;

    private static MediatedAdsManager instance;
    private Action<bool> pendingReward;
    private int completedRunsThisSession;
    private bool initializationRequested;
    private bool consentRequestPending;
    private bool requestedPersonalizedAds;
    private bool bannerRequestedVisible;
    private float bannerHeightPixels;

#if LEVELPLAY_SDK
    private LevelPlayRewardedAd rewardedAd;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayBannerAd bannerAd;
    private bool bannerLoaded;
#endif

    public static MediatedAdsManager Create()
    {
        if (instance != null) return instance;
        GameObject host = new GameObject("Mediated Ads");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<MediatedAdsManager>();
        return instance;
    }

    private void Start()
    {
        if (!AdConfiguration.AdsEnabled)
        {
            Debug.Log("Ads are disabled by AdsConfig.");
            return;
        }

        if (!AdConfiguration.IsConfigured)
        {
            Debug.Log("LevelPlay is installed but production ad IDs are not configured. Editor reward previews remain available.");
            return;
        }

        // Every device build initializes ads. Until a valid privacy choice says
        // otherwise, use the safer non-personalized setting.
        bool personalizedAds = PlayerPrefs.GetInt("AdPersonalizationConsent", 0) == 1;
        InitializeAfterConsent(personalizedAds);
    }

    public void SubmitPrivacyChoice(bool personalizedAds)
    {
        PlayerPrefs.SetInt("AdPersonalizationConsent", personalizedAds ? 1 : 0);
        PlayerPrefs.Save();
        if (!AdConfiguration.AdsEnabled) return;
        ApplyPrivacySettings(personalizedAds);
        InitializeAfterConsent(personalizedAds);
    }

    public bool NotifyRunEnded()
    {
        if (!AdConfiguration.AdsEnabled) return false;
        int cadence = ConfigManager.Current.ads.interstitialEveryRuns;
        if (cadence <= 0) return false;
        completedRunsThisSession++;
        if (completedRunsThisSession < cadence) return false;
        completedRunsThisSession = 0;
        return ShowInterstitial(ConfigManager.Current.ads.gameOverPlacement);
    }

    private void InitializeSdk(bool personalizedAds)
    {
        if (initializationRequested || !AdConfiguration.AdsEnabled || !AdConfiguration.IsConfigured) return;
        initializationRequested = true;
#if LEVELPLAY_SDK
        ApplyPrivacySettings(personalizedAds);
        LevelPlay.OnInitSuccess += HandleInitializationSucceeded;
        LevelPlay.OnInitFailed += HandleInitializationFailed;
        LevelPlay.Init(AdConfiguration.AndroidAppKey);
#else
        Debug.LogWarning("The Ads Mediation package is not available; ads are disabled.");
#endif
    }

    private void InitializeAfterConsent(bool personalizedAds)
    {
        requestedPersonalizedAds = personalizedAds;
        if (initializationRequested || consentRequestPending || !AdConfiguration.AdsEnabled ||
            !AdConfiguration.IsConfigured) return;
        consentRequestPending = true;
        GoogleUmpConsent.EnsureReady(allowed =>
        {
            consentRequestPending = false;
            if (!allowed)
            {
                Debug.LogWarning("Ads remain disabled until Google UMP confirms that ads may be requested.");
                return;
            }
            InitializeSdk(requestedPersonalizedAds);
        });
    }

    private void ApplyPrivacySettings(bool personalizedAds)
    {
#if LEVELPLAY_SDK
        Dictionary<string, bool> consent = new Dictionary<string, bool>
        {
            { "UnityAds", personalizedAds },
            { "IronSource", personalizedAds },
            { "AdMob", personalizedAds }
        };
        LevelPlayPrivacySettings.SetGDPRConsents(consent);
        LevelPlayPrivacySettings.SetCCPA(!personalizedAds);
        LevelPlayPrivacySettings.SetCOPPA(AdConfiguration.ChildDirected);
#endif
    }

    public void ShowRewarded(string placement, Action<bool> callback)
    {
        if (!AdConfiguration.AdsEnabled)
        {
            callback?.Invoke(false);
            return;
        }
#if UNITY_EDITOR
        StartCoroutine(SimulateReward(callback));
 #else
#if LEVELPLAY_SDK
        if (rewardedAd != null && rewardedAd.IsAdReady() && !LevelPlayRewardedAd.IsPlacementCapped(placement))
        {
            pendingReward = callback;
            rewardedAd.ShowAd(placement);
            return;
        }
#endif
        callback?.Invoke(false);
#endif
    }

    public bool ShowInterstitial(string placement)
    {
        if (!AdConfiguration.AdsEnabled) return false;
#if LEVELPLAY_SDK
        if (interstitialAd != null && interstitialAd.IsAdReady() && !LevelPlayInterstitialAd.IsPlacementCapped(placement))
        {
            interstitialAd.ShowAd(placement);
            return true;
        }
#endif
        return false;
    }

    public bool ShowBanner()
    {
        bannerRequestedVisible = true;
        if (!AdConfiguration.BannerAdsEnabled || !AdConfiguration.IsBannerConfigured) return false;
#if LEVELPLAY_SDK
        if (bannerAd == null || !bannerLoaded) return false;
        bannerAd.ShowAd();
        bannerAd.ResumeAutoRefresh();
        BannerInsetChanged?.Invoke(bannerHeightPixels);
        return true;
#else
        return false;
#endif
    }

    public void HideBanner()
    {
        bannerRequestedVisible = false;
        BannerInsetChanged?.Invoke(0f);
#if LEVELPLAY_SDK
        if (bannerAd == null) return;
        bannerAd.PauseAutoRefresh();
        bannerAd.HideAd();
#endif
    }

    private IEnumerator SimulateReward(Action<bool> callback)
    {
        yield return new WaitForSecondsRealtime(0.35f);
        callback?.Invoke(true);
    }

#if LEVELPLAY_SDK
    private void HandleInitializationSucceeded(LevelPlayConfiguration configuration)
    {
        Debug.Log("LevelPlay initialized successfully; loading rewarded, interstitial, and banner ads.");
        CreateAndLoadAds();
    }

    private void HandleInitializationFailed(LevelPlayInitError error)
    {
        Debug.LogWarning($"LevelPlay initialization failed: {error}");
    }

    private void CreateAndLoadAds()
    {
        rewardedAd = new LevelPlayRewardedAd(AdConfiguration.RewardedAdUnitId);
        rewardedAd.OnAdRewarded += (adInfo, reward) =>
        {
            Action<bool> callback = pendingReward;
            pendingReward = null;
            callback?.Invoke(true);
        };
        rewardedAd.OnAdDisplayFailed += (adInfo, error) =>
        {
            FailPendingReward();
            rewardedAd.LoadAd();
        };
        rewardedAd.OnAdLoadFailed += error =>
        {
            Debug.LogWarning($"LevelPlay rewarded ad failed to load: {error}");
            StartCoroutine(RetryRewardedLoad());
        };
        rewardedAd.OnAdClosed += adInfo =>
        {
            rewardedAd.LoadAd();
            if (pendingReward != null) StartCoroutine(FailPendingRewardAfterCallbackWindow());
        };
        rewardedAd.LoadAd();

        interstitialAd = new LevelPlayInterstitialAd(AdConfiguration.InterstitialAdUnitId);
        interstitialAd.OnAdClosed += adInfo => interstitialAd.LoadAd();
        interstitialAd.OnAdDisplayFailed += (adInfo, error) =>
        {
            Debug.LogWarning($"LevelPlay interstitial ad failed to display: {error}");
            interstitialAd.LoadAd();
        };
        interstitialAd.OnAdLoadFailed += error =>
        {
            Debug.LogWarning($"LevelPlay interstitial ad failed to load: {error}");
            StartCoroutine(RetryInterstitialLoad());
        };
        interstitialAd.LoadAd();

        CreateAndLoadBanner();
    }

    private void CreateAndLoadBanner()
    {
        if (!AdConfiguration.BannerAdsEnabled || !AdConfiguration.IsBannerConfigured)
        {
            if (AdConfiguration.BannerAdsEnabled)
                Debug.Log("Banner ads are enabled, but the LevelPlay banner ad-unit ID has not been configured yet.");
            return;
        }

        LevelPlayAdSize size = AdConfiguration.BannerUseAdaptiveSize
            ? LevelPlayAdSize.CreateAdaptiveAdSize()
            : LevelPlayAdSize.BANNER;
        LevelPlayBannerAd.Config bannerConfig = new LevelPlayBannerAd.Config.Builder()
            .SetSize(size)
            .SetPosition(ResolveBannerPosition(AdConfiguration.BannerPosition))
            .SetPlacementName(AdConfiguration.BannerPlacement)
            .SetDisplayOnLoad(false)
            .SetRespectSafeArea(AdConfiguration.BannerRespectSafeArea)
            .Build();

        bannerAd = new LevelPlayBannerAd(AdConfiguration.BannerAdUnitId, bannerConfig);
        bannerAd.OnAdLoaded += HandleBannerLoaded;
        bannerAd.OnAdLoadFailed += HandleBannerLoadFailed;
        bannerAd.OnAdDisplayFailed += HandleBannerDisplayFailed;
        bannerAd.LoadAd();
    }

    private void HandleBannerLoaded(LevelPlayAdInfo adInfo)
    {
        bannerLoaded = true;
        bannerHeightPixels = CalculateBannerHeightPixels();
        if (bannerRequestedVisible) ShowBanner();
        else bannerAd?.PauseAutoRefresh();
    }

    private void HandleBannerLoadFailed(LevelPlayAdError error)
    {
        bannerLoaded = false;
        Debug.LogWarning($"LevelPlay banner failed to load: {error}");
        StartCoroutine(RetryBannerLoad());
    }

    private void HandleBannerDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        bannerLoaded = false;
        Debug.LogWarning($"LevelPlay banner failed to display: {error}");
        StartCoroutine(RetryBannerLoad());
    }

    private static LevelPlayBannerPosition ResolveBannerPosition(string position)
    {
        switch (position)
        {
            case "TopLeft": return LevelPlayBannerPosition.TopLeft;
            case "TopCenter": return LevelPlayBannerPosition.TopCenter;
            case "TopRight": return LevelPlayBannerPosition.TopRight;
            case "CenterLeft": return LevelPlayBannerPosition.CenterLeft;
            case "Center": return LevelPlayBannerPosition.Center;
            case "CenterRight": return LevelPlayBannerPosition.CenterRight;
            case "BottomLeft": return LevelPlayBannerPosition.BottomLeft;
            case "BottomRight": return LevelPlayBannerPosition.BottomRight;
            default: return LevelPlayBannerPosition.BottomCenter;
        }
    }

    private float CalculateBannerHeightPixels()
    {
        LevelPlayAdSize size = bannerAd?.GetAdSize();
        if (size == null || size.Height <= 0) return 0f;

        // LevelPlay reports Android dimensions in dp and iOS dimensions in points.
        // Using the reported width derives the device scale without platform-specific APIs.
        float unitToPixelScale = size.Width > 0 ? Screen.width / (float)size.Width : 1f;
        return Mathf.Clamp(size.Height * unitToPixelScale, 0f, Screen.height * 0.25f);
    }

    private IEnumerator RetryRewardedLoad()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(1f, AdConfiguration.AdLoadRetrySeconds));
        if (rewardedAd != null && !rewardedAd.IsAdReady()) rewardedAd.LoadAd();
    }

    private IEnumerator RetryInterstitialLoad()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(1f, AdConfiguration.AdLoadRetrySeconds));
        if (interstitialAd != null && !interstitialAd.IsAdReady()) interstitialAd.LoadAd();
    }

    private IEnumerator RetryBannerLoad()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(1f, AdConfiguration.AdLoadRetrySeconds));
        if (bannerAd != null && !bannerLoaded) bannerAd.LoadAd();
    }

    private IEnumerator FailPendingRewardAfterCallbackWindow()
    {
        yield return new WaitForSecondsRealtime(0.75f);
        FailPendingReward();
    }

    private void FailPendingReward()
    {
        Action<bool> callback = pendingReward;
        pendingReward = null;
        callback?.Invoke(false);
    }

#endif

    private void OnApplicationPause(bool paused)
    {
#if LEVELPLAY_SDK
        if (bannerAd == null || !bannerLoaded) return;
        if (paused) bannerAd.PauseAutoRefresh();
        else if (bannerRequestedVisible) bannerAd.ResumeAutoRefresh();
#endif
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
#if LEVELPLAY_SDK
        LevelPlay.OnInitSuccess -= HandleInitializationSucceeded;
        LevelPlay.OnInitFailed -= HandleInitializationFailed;
        if (bannerAd != null)
        {
            bannerAd.OnAdLoaded -= HandleBannerLoaded;
            bannerAd.OnAdLoadFailed -= HandleBannerLoadFailed;
            bannerAd.OnAdDisplayFailed -= HandleBannerDisplayFailed;
            bannerAd.DestroyAd();
            bannerAd = null;
        }
#endif
    }
}
