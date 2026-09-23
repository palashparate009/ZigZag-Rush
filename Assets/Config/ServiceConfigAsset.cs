using UnityEngine;

[CreateAssetMenu(fileName = "ServiceConfig", menuName = "ZigZag/Config/Services")]
public sealed class ServiceConfigAsset : ScriptableObject
{
    public ServiceConfig settings = new ServiceConfig();
}
