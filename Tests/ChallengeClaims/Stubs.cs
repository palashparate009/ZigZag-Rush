// In-memory adapters: tests cannot modify the game's real PlayerPrefs or Firebase.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static int Clamp(int x, int lo, int hi) => Math.Clamp(x, lo, hi);
    }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> values = new();
        public static int GetInt(string key, int fallback = 0) => values.TryGetValue(key, out var x) ? (int)x : fallback;
        public static string GetString(string key, string fallback = "") => values.TryGetValue(key, out var x) ? (string)x : fallback;
        public static void SetInt(string key, int value) => values[key] = value;
        public static void SetString(string key, string value) => values[key] = value;
        public static void Save() {}
        public static void DeleteAll() => values.Clear();
    }
}
public static class ConfigManager { public static GameConfig Current = new(); }
public static class CloudSaveManager
{
    public static int Notifications;
    public static void NotifyLocalProgressChanged() => Notifications++;
}
public enum SpecialTileType { Normal }
public sealed class CarDefinition
{
    public string id = "";
    public bool startsUnlocked, rewardedAdUnlock;
    public int gemCost;
}
public sealed class CarCatalog
{
    public static CarCatalog Load() => null;
    public int Count => 0;
    public CarDefinition[] Cars => Array.Empty<CarDefinition>();
    public CarDefinition Get(int index) => null;
    public int IndexOf(string id) => -1;
}
