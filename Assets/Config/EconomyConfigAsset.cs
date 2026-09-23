using UnityEngine;

[CreateAssetMenu(fileName = "EconomyConfig", menuName = "ZigZag/Config/Economy")]
public sealed class EconomyConfigAsset : ScriptableObject
{
    public EconomyConfig settings = new EconomyConfig();
}
