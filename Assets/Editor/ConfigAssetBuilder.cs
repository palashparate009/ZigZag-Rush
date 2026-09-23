using System.IO;
using UnityEditor;
using UnityEngine;

public static class ConfigAssetBuilder
{
    private const string JsonPath = "Assets/Resources/Config/game_config.json";
    private const string RootFolder = "Assets/Resources/Config";
    private const string SectionsFolder = RootFolder + "/Sections";
    private const string RootAssetPath = RootFolder + "/GameConfig.asset";

    [InitializeOnLoadMethod]
    private static void QueueFirstTimeBuild()
    {
        EditorApplication.delayCall += EnsureAssetsExist;
    }

    [MenuItem("ZigZag/Config/Select Game Config")]
    public static void SelectGameConfig()
    {
        EnsureAssetsExist();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(RootAssetPath);
    }

    [MenuItem("ZigZag/Config/Rebuild ScriptableObjects From JSON")]
    public static void RebuildFromJson()
    {
        TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (jsonAsset == null)
        {
            Debug.LogError($"Cannot build configuration assets because {JsonPath} is missing.");
            return;
        }

        GameConfig data = JsonUtility.FromJson<GameConfig>(jsonAsset.text);
        if (data == null)
        {
            Debug.LogError($"Cannot build configuration assets because {JsonPath} is invalid.");
            return;
        }

        EnsureFolder(SectionsFolder);
        NetworkConfigAsset network = LoadOrCreate<NetworkConfigAsset>(SectionsFolder + "/NetworkConfig.asset");
        ServiceConfigAsset services = LoadOrCreate<ServiceConfigAsset>(SectionsFolder + "/ServiceConfig.asset");
        AdsConfigAsset ads = LoadOrCreate<AdsConfigAsset>(SectionsFolder + "/AdsConfig.asset");
        CloudConfigAsset cloud = LoadOrCreate<CloudConfigAsset>(SectionsFolder + "/CloudConfig.asset");
        EconomyConfigAsset economy = LoadOrCreate<EconomyConfigAsset>(SectionsFolder + "/EconomyConfig.asset");
        GameplayConfigAsset gameplay = LoadOrCreate<GameplayConfigAsset>(SectionsFolder + "/GameplayConfig.asset");
        ChallengeConfigAsset challenges = LoadOrCreate<ChallengeConfigAsset>(SectionsFolder + "/ChallengeConfig.asset");
        GameConfigAsset root = LoadOrCreate<GameConfigAsset>(RootAssetPath);

        network.settings = data.network ?? new NetworkConfig();
        services.settings = data.services ?? new ServiceConfig();
        ads.settings = data.ads ?? new AdsConfig();
        cloud.settings = data.cloud ?? new CloudConfig();
        economy.settings = data.economy ?? new EconomyConfig();
        gameplay.settings = data.gameplay ?? new GameplayConfig();
        challenges.settings = data.challenges ?? new ChallengeConfig();

        root.version = data.version;
        root.network = network;
        root.services = services;
        root.ads = ads;
        root.cloud = cloud;
        root.economy = economy;
        root.gameplay = gameplay;
        root.challenges = challenges;

        EditorUtility.SetDirty(network);
        EditorUtility.SetDirty(services);
        EditorUtility.SetDirty(ads);
        EditorUtility.SetDirty(cloud);
        EditorUtility.SetDirty(economy);
        EditorUtility.SetDirty(gameplay);
        EditorUtility.SetDirty(challenges);
        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Game configuration ScriptableObjects rebuilt from the single JSON config.");
    }

    private static void EnsureAssetsExist()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameConfigAsset>(RootAssetPath) == null) RebuildFromJson();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
