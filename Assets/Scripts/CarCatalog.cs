using System;
using UnityEngine;

[Serializable]
public sealed class CarDefinition
{
    public string id;
    public string displayName;
    public string familyName;
    public string variantName;
    public GameObject prefab;
    [Min(0)] public int gemCost;
    public bool startsUnlocked;
    public bool rewardedAdUnlock = true;
    [Range(0.8f, 1.2f)] public float speedMultiplier = 1f;
    [Range(0.8f, 1.3f)] public float perfectWindowMultiplier = 1f;
    public string drivingStyle;
}

[CreateAssetMenu(fileName = "CarCatalog", menuName = "ZigZag/Car Catalog")]
public sealed class CarCatalog : ScriptableObject
{
    [SerializeField] private string sourceSignature;
    [SerializeField] private CarDefinition[] cars = Array.Empty<CarDefinition>();

    public string SourceSignature => sourceSignature;
    public int Count => cars?.Length ?? 0;
    public CarDefinition[] Cars => cars;

    public static CarCatalog Load()
    {
        return Resources.Load<CarCatalog>("Cars/CarCatalog");
    }

    public CarDefinition Get(int index)
    {
        if (Count == 0) return null;
        return cars[Mathf.Clamp(index, 0, Count - 1)];
    }

    public int IndexOf(string id)
    {
        if (string.IsNullOrEmpty(id) || cars == null) return -1;
        for (int i = 0; i < cars.Length; i++)
            if (string.Equals(cars[i].id, id, StringComparison.Ordinal)) return i;
        return -1;
    }

#if UNITY_EDITOR
    public void SetGeneratedContent(string signature, CarDefinition[] generatedCars)
    {
        sourceSignature = signature;
        cars = generatedCars ?? Array.Empty<CarDefinition>();
    }
#endif
}
