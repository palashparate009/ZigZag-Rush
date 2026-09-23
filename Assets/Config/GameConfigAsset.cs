using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "ZigZag/Config/Game Config")]
public sealed class GameConfigAsset : ScriptableObject
{
    public int version = 5;
    public NetworkConfigAsset network;
    public ServiceConfigAsset services;
    public AdsConfigAsset ads;
    public CloudConfigAsset cloud;
    public EconomyConfigAsset economy;
    public GameplayConfigAsset gameplay;
    public GhostConfigAsset ghost;
    public BackgroundConfigAsset background;
    public ChallengeConfigAsset challenges;

    public GameConfig CreateRuntimeConfig()
    {
        return new GameConfig
        {
            version = version,
            network = network != null ? network.settings : new NetworkConfig(),
            services = services != null ? services.settings : new ServiceConfig(),
            ads = ads != null ? ads.settings : new AdsConfig(),
            cloud = cloud != null ? cloud.settings : new CloudConfig(),
            economy = economy != null ? economy.settings : new EconomyConfig(),
            gameplay = gameplay != null ? gameplay.settings : new GameplayConfig(),
            ghost = ghost != null ? ghost.settings : new GhostConfig(),
            background = background != null ? background.settings : new BackgroundConfig(),
            challenges = challenges != null ? challenges.settings : new ChallengeConfig()
        };
    }
}
