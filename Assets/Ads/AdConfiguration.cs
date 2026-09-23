public static class AdConfiguration
{
    public static bool AdsEnabled => ConfigManager.Current.ads.adsEnabled;
    public static string AndroidAppKey => ConfigManager.Current.ads.androidAppKey;
    public static string RewardedAdUnitId => ConfigManager.Current.ads.rewardedAdUnitId;
    public static string InterstitialAdUnitId => ConfigManager.Current.ads.interstitialAdUnitId;
    public static bool BannerAdsEnabled => AdsEnabled && ConfigManager.Current.ads.bannerAdsEnabled;
    public static string BannerAdUnitId => ConfigManager.Current.ads.bannerAdUnitId;
    public static string BannerPlacement => ConfigManager.Current.ads.bannerPlacement;
    public static string BannerPosition => ConfigManager.Current.ads.bannerPosition;
    public static bool BannerUseAdaptiveSize => ConfigManager.Current.ads.bannerUseAdaptiveSize;
    public static bool BannerRespectSafeArea => ConfigManager.Current.ads.bannerRespectSafeArea;
    public static bool AdMobEnabled => ConfigManager.Current.ads.adMobEnabled;
    public static string AdMobAppId => ConfigManager.Current.ads.adMobAppId;
    public static string AdMobBannerAdUnitId => ConfigManager.Current.ads.adMobBannerAdUnitId;
    public static float AdLoadRetrySeconds => ConfigManager.Current.ads.adLoadRetrySeconds;
    public static string PrivacyPolicyUrl => ConfigManager.Current.services.privacyPolicyUrl;
    public static bool CertifiedCmpConfigured => ConfigManager.Current.ads.certifiedCmpConfigured;
    public static bool TargetAudienceReviewed => ConfigManager.Current.ads.targetAudienceReviewed;
    public static bool ChildDirected => ConfigManager.Current.ads.childDirected;

    public static bool IsConfigured =>
        HasProductionValue(AndroidAppKey) &&
        HasProductionValue(RewardedAdUnitId) &&
        HasProductionValue(InterstitialAdUnitId);

    public static bool IsBannerConfigured =>
        !BannerAdsEnabled || HasProductionValue(BannerAdUnitId);

    public static bool IsAdMobConfigured =>
        !AdMobEnabled ||
        (HasAdMobId(AdMobAppId, "~") && HasAdMobId(AdMobBannerAdUnitId, "/"));

    public static bool HasPrivacyPolicy => HasProductionValue(PrivacyPolicyUrl);
    public static bool IsReleaseReady =>
        !AdsEnabled || (IsConfigured && IsBannerConfigured && IsAdMobConfigured && HasPrivacyPolicy && CertifiedCmpConfigured && TargetAudienceReviewed);

    private static bool HasProductionValue(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.StartsWith("REPLACE_");

    private static bool HasAdMobId(string value, string separator) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("ca-app-pub-") && value.Contains(separator);
}
