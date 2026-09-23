using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
internal sealed class CarCatalogBuilder : AssetPostprocessor, IPreprocessBuildWithReport
{
    private const string SourceRoot = "Assets/Prefabs/Cars";
    private const string ResourcesFolder = "Assets/Resources/Cars";
    private const string CatalogPath = ResourcesFolder + "/CarCatalog.asset";
    private static bool rebuildQueued;
    public int callbackOrder => -1000;

    private static readonly string[] FreeCars =
    {
        "muscle type c color 2",
        "samurai_blue_a",
        "taxi sedan",
        "sports type b color 3"
    };

    static CarCatalogBuilder()
    {
        QueueRebuild();
    }

    [MenuItem("ZigZag/Garage/Rebuild Car Catalog")]
    public static void RebuildFromMenu()
    {
        Rebuild(true);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        Rebuild(true);
        if (AssetDatabase.LoadAssetAtPath<CarCatalog>(CatalogPath) == null)
            throw new BuildFailedException("The garage car catalog could not be generated.");
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (TouchesCarFolder(imported) || TouchesCarFolder(deleted) || TouchesCarFolder(moved) || TouchesCarFolder(movedFrom))
            QueueRebuild();
    }

    private static bool TouchesCarFolder(IEnumerable<string> paths)
    {
        return paths != null && paths.Any(path => path.Replace('\\', '/').StartsWith(SourceRoot + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static void QueueRebuild()
    {
        if (rebuildQueued) return;
        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueRebuild();
                return;
            }
            Rebuild(false);
        };
    }

    private static void Rebuild(bool force)
    {
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { SourceRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string signature = BuildSignature(paths);
        CarCatalog catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CatalogPath);
        if (!force && catalog != null && catalog.SourceSignature == signature) return;

        EnsureFolder("Assets/Resources", "Cars");
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<CarCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        List<CarDefinition> definitions = paths.Select(CreateDefinition).ToList();
        definitions.Sort((left, right) =>
        {
            int leftFree = FreeRank(Path.GetFileNameWithoutExtension(left.id));
            int rightFree = FreeRank(Path.GetFileNameWithoutExtension(right.id));
            if (leftFree != rightFree) return leftFree.CompareTo(rightFree);
            int family = string.Compare(left.familyName, right.familyName, StringComparison.OrdinalIgnoreCase);
            return family != 0 ? family : string.Compare(left.variantName, right.variantName, StringComparison.OrdinalIgnoreCase);
        });

        catalog.SetGeneratedContent(signature, definitions.ToArray());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Garage car catalog rebuilt with {definitions.Count} cars from {SourceRoot}.");
    }

    private static CarDefinition CreateDefinition(string assetPath)
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        string relative = normalizedPath.Substring(SourceRoot.Length + 1);
        string fileName = Path.GetFileNameWithoutExtension(relative);
        bool standalone = !relative.Contains("/");
        ParseNames(fileName, standalone, out string family, out string variant, out string displayName);
        bool free = FreeRank(fileName) < FreeCars.Length;
        GetDrivingProfile(family, fileName, out float speed, out float perfectWindow, out string style);

        return new CarDefinition
        {
            id = relative.Substring(0, relative.Length - Path.GetExtension(relative).Length).ToLowerInvariant(),
            displayName = displayName,
            familyName = family,
            variantName = variant,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath),
            gemCost = free ? 0 : CalculatePrice(fileName, standalone),
            startsUnlocked = free,
            rewardedAdUnlock = !free,
            speedMultiplier = speed,
            perfectWindowMultiplier = perfectWindow,
            drivingStyle = style
        };
    }

    private static void ParseNames(string fileName, bool standalone, out string family, out string variant, out string displayName)
    {
        Match samurai = Regex.Match(fileName, @"^Samurai_(.+)_([AB])$", RegexOptions.IgnoreCase);
        Match colour = Regex.Match(fileName, @"^(.*?)\s+color\s+(\d+)$", RegexOptions.IgnoreCase);
        if (samurai.Success)
        {
            family = "Samurai " + samurai.Groups[2].Value.ToUpperInvariant();
            variant = Pretty(samurai.Groups[1].Value);
            displayName = family + " - " + variant;
        }
        else if (colour.Success)
        {
            family = Pretty(colour.Groups[1].Value);
            variant = "Color " + colour.Groups[2].Value;
            displayName = family + " - " + variant;
        }
        else
        {
            family = standalone ? "Road Classics" : Pretty(fileName);
            variant = standalone ? Pretty(fileName) : "Standard";
            displayName = standalone ? variant : family;
        }
    }

    private static int CalculatePrice(string fileName, bool standalone)
    {
        Match colour = Regex.Match(fileName, @"color\s+(\d+)$", RegexOptions.IgnoreCase);
        if (colour.Success && int.TryParse(colour.Groups[1].Value, out int colourNumber))
            return 100 + Mathf.Max(0, colourNumber - 1) * 75;

        Match samurai = Regex.Match(fileName, @"^Samurai_(.+)_[AB]$", RegexOptions.IgnoreCase);
        if (samurai.Success)
        {
            string[] colours = { "Blue", "Green", "Red", "Yellow", "White", "Grey", "Black", "BlueDark", "GreenDark", "RedDark" };
            int rank = Array.FindIndex(colours, value => string.Equals(value, samurai.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            return 150 + Mathf.Max(0, rank) * 50;
        }

        Match numberedCar = Regex.Match(fileName, @"^Car(\d+)$", RegexOptions.IgnoreCase);
        if (standalone && numberedCar.Success && int.TryParse(numberedCar.Groups[1].Value, out int number))
            return 300 + Mathf.Max(0, number - 1) * 75;
        return 350;
    }

    private static void GetDrivingProfile(string family, string fileName, out float speed, out float perfectWindow, out string style)
    {
        string key = (family + " " + fileName).ToLowerInvariant();
        if (key.Contains("sports"))
        {
            speed = 1.08f; perfectWindow = 0.90f; style = "SPRINTER: faster, with more precise turns";
        }
        else if (key.Contains("muscle"))
        {
            speed = 1.03f; perfectWindow = 1.08f; style = "POWER: quick, with forgiving turns";
        }
        else if (key.Contains("samurai"))
        {
            speed = 1.00f; perfectWindow = 1.15f; style = "PRECISION: balanced speed and excellent handling";
        }
        else if (key.Contains("pickup"))
        {
            speed = 0.94f; perfectWindow = 1.22f; style = "CONTROL: slower, with the widest PERFECT window";
        }
        else if (key.Contains("cop"))
        {
            speed = 1.06f; perfectWindow = 0.96f; style = "PURSUIT: fast and responsive";
        }
        else if (key.Contains("taxi") || key.Contains("sedan"))
        {
            speed = 0.98f; perfectWindow = 1.12f; style = "CRUISER: steady and easy to control";
        }
        else
        {
            int profile = StableHash(fileName) % 3;
            speed = profile == 0 ? 0.97f : profile == 1 ? 1.02f : 1.05f;
            perfectWindow = profile == 0 ? 1.16f : profile == 1 ? 1.04f : 0.96f;
            style = profile == 0 ? "CONTROL: relaxed speed and easier turns" :
                profile == 1 ? "BALANCED: an all-round road car" : "RUSH: higher speed and tighter turns";
        }
    }

    private static int FreeRank(string fileName)
    {
        for (int i = 0; i < FreeCars.Length; i++)
            if (string.Equals(fileName, FreeCars[i], StringComparison.OrdinalIgnoreCase)) return i;
        return FreeCars.Length;
    }

    private static string Pretty(string value)
    {
        string spaced = Regex.Replace(value.Replace('_', ' '), "([a-z])([A-Z])", "$1 $2");
        return string.Join(" ", spaced.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant()));
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char character in value) hash = hash * 31 + char.ToLowerInvariant(character);
            return Math.Abs(hash == int.MinValue ? 0 : hash);
        }
    }

    private static string BuildSignature(IEnumerable<string> paths)
    {
        StringBuilder signature = new StringBuilder();
        foreach (string path in paths) signature.Append(path).Append(':').Append(AssetDatabase.AssetPathToGUID(path)).Append('|');
        return Hash128.Compute(signature.ToString()).ToString();
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
