using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PlayStoreBuild
{
    private static string PackageName => ConfigManager.Current.services.packageName;

    [MenuItem("ZigZag/Prepare Android Release Settings")]
    public static void PrepareSettings()
    {
        PlayerSettings.companyName = "ZigZag Studio";
        PlayerSettings.productName = "ZigZag Rush";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PackageName);
        // Keep the release version configured in Player Settings. A build helper
        // must never silently roll a newer store version back to 1.0.0.
        PlayerSettings.Android.bundleVersionCode = Mathf.Max(1, PlayerSettings.Android.bundleVersionCode);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Branding/AppIcon.png");
        if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Android, new[] { icon }, IconKind.Application);
        EditorUserBuildSettings.buildAppBundle = true;
        AssetDatabase.SaveAssets();
        Debug.Log("Android release settings prepared: API 36, ARM64 + ARMv7, IL2CPP and AAB.");
    }

    [MenuItem("ZigZag/Validate Play Store Readiness")]
    public static void ValidateReadiness()
    {
        bool valid = true;
        valid &= Check(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android) == PackageName, "Final Android package ID is configured.");
        valid &= Check(PlayerSettings.Android.targetSdkVersion == (AndroidSdkVersions)36, "Target SDK is API 36.");
        valid &= Check((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0, "ARM64 is enabled.");
        valid &= Check(PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP, "IL2CPP is enabled.");
        valid &= Check(EditorUserBuildSettings.buildAppBundle, "Android App Bundle output is enabled.");
        valid &= Check(PlayerSettings.Android.useCustomKeystore, "A private release keystore is configured.");
        if (AdConfiguration.AdsEnabled)
        {
            valid &= Check(AdConfiguration.IsConfigured, "Production LevelPlay rewarded and interstitial IDs are configured.");
            if (AdConfiguration.BannerAdsEnabled)
                valid &= Check(AdConfiguration.IsBannerConfigured, "Production LevelPlay banner ad-unit ID is configured.");
            if (AdConfiguration.AdMobEnabled)
                valid &= Check(AdConfiguration.IsAdMobConfigured, "AdMob Android app and banner ad-unit IDs are configured.");
            valid &= Check(AdConfiguration.HasPrivacyPolicy, "Privacy policy URL is configured.");
            valid &= Check(AdConfiguration.CertifiedCmpConfigured, "A Google-certified consent management platform is configured.");
            valid &= Check(AdConfiguration.TargetAudienceReviewed, "Target audience and child-directed ad settings were reviewed.");
        }
        else Debug.Log("INFO: Advertising is disabled in AdsConfig.");
        valid &= Check(PlayerSettings.GetIcons(UnityEditor.Build.NamedBuildTarget.Android, IconKind.Application).Length > 0, "Android launcher icons are configured.");

        if (valid) Debug.Log("ZigZag Rush is configured for a Play Store release build.");
        else Debug.LogWarning("Play Store validation found unfinished account-owned configuration. Review the messages above.");
    }

    [MenuItem("ZigZag/Build Release AAB")]
    public static void BuildReleaseAab()
    {
        PrepareSettings();
        if (!PlayerSettings.Android.useCustomKeystore)
            throw new UnityEditor.Build.BuildFailedException("Configure and back up a private Android keystore before building the Play Store AAB.");
        if (!AdConfiguration.IsReleaseReady)
            throw new UnityEditor.Build.BuildFailedException("Configure LevelPlay IDs, a hosted privacy policy, and a certified CMP before building the ad-supported release.");

        string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Android"));
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, $"ZigZagRush-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.aab");
        string[] scenes = Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled).ConvertAll(scene => scene.path);
        BuildReport report = BuildPipeline.BuildPlayer(scenes, outputPath, BuildTarget.Android, BuildOptions.CleanBuildCache);
        if (report.summary.result != BuildResult.Succeeded)
            throw new UnityEditor.Build.BuildFailedException($"Android build failed: {report.summary.result}");
        Debug.Log($"Release AAB created at {outputPath}");
    }

    private static bool Check(bool condition, string message)
    {
        if (condition) Debug.Log($"PASS: {message}");
        else Debug.LogWarning($"TODO: {message}");
        return condition;
    }

    private static string[] ConvertAll(this EditorBuildSettingsScene[] scenes, Func<EditorBuildSettingsScene, string> selector)
    {
        string[] result = new string[scenes.Length];
        for (int i = 0; i < scenes.Length; i++) result[i] = selector(scenes[i]);
        return result;
    }
}
