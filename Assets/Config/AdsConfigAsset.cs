using UnityEngine;

[CreateAssetMenu(fileName = "AdsConfig", menuName = "ZigZag/Config/Ads")]
public sealed class AdsConfigAsset : ScriptableObject
{
    public AdsConfig settings = new AdsConfig();
}
