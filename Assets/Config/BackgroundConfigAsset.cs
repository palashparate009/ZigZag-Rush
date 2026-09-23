using UnityEngine;

[CreateAssetMenu(fileName = "BackgroundConfig", menuName = "ZigZag/Config/Background")]
public sealed class BackgroundConfigAsset : ScriptableObject
{
    public BackgroundConfig settings = new BackgroundConfig();
}
