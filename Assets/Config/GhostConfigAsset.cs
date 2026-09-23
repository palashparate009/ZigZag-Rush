using UnityEngine;

[CreateAssetMenu(fileName = "GhostConfig", menuName = "ZigZag/Config/Ghost")]
public sealed class GhostConfigAsset : ScriptableObject
{
    public GhostConfig settings = new GhostConfig();
}
