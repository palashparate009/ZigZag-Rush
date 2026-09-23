using UnityEngine;

[CreateAssetMenu(fileName = "CloudConfig", menuName = "ZigZag/Config/Cloud")]
public sealed class CloudConfigAsset : ScriptableObject
{
    public CloudConfig settings = new CloudConfig();
}
