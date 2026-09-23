using UnityEngine;

[CreateAssetMenu(fileName = "ChallengeConfig", menuName = "ZigZag/Config/Challenges")]
public sealed class ChallengeConfigAsset : ScriptableObject
{
    public ChallengeConfig settings = new ChallengeConfig();
}
