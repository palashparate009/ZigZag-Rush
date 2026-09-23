using UnityEngine;

[CreateAssetMenu(fileName = "GameplayConfig", menuName = "ZigZag/Config/Gameplay")]
public sealed class GameplayConfigAsset : ScriptableObject
{
    public GameplayConfig settings = new GameplayConfig();
}
