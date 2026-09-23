using UnityEngine;

[CreateAssetMenu(fileName = "NetworkConfig", menuName = "ZigZag/Config/Network")]
public sealed class NetworkConfigAsset : ScriptableObject
{
    public NetworkConfig settings = new NetworkConfig();
}
