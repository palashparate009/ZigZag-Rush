using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class GameProgress
{
    private const string LifetimeRunsStartedKey = "LifetimeRunsStarted";
    private static readonly int[] CarMasteryThresholds = { 0, 250, 700, 1500, 3000 };
    public static string[] SkinNames => ConfigManager.Current.economy.legacySkinNames;
    public static int[] SkinCosts => ConfigManager.Current.economy.legacySkinCosts;
    public static string[] ThemeNames => ConfigManager.Current.economy.themeNames;
    public static int[] ThemeCosts => ConfigManager.Current.economy.themeCosts;

    public static int BestScore => PlayerPrefs.GetInt("HighScore", 0);
    public static int TotalGems => PlayerPrefs.GetInt("TotalGems", 0);
    public static int LifetimeRunsStarted => PlayerPrefs.GetInt(LifetimeRunsStartedKey, 0);
    public static int SelectedSkin => SelectedCarIndex;
    public static int SelectedCarIndex
    {
        get
        {
            CarCatalog catalog = CarCatalog.Load();
            if (catalog == null || catalog.Count == 0) return Mathf.Clamp(PlayerPrefs.GetInt("SelectedSkin", 0), 0, SkinNames.Length - 1);
            int selected = catalog.IndexOf(PlayerPrefs.GetString("SelectedCarId", string.Empty));
            if (selected >= 0 && IsCarUnlocked(selected)) return selected;
            for (int i = 0; i < catalog.Count; i++)
                if (IsCarUnlocked(i)) return i;
            return 0;
        }
    }
    public static string SelectedCarId => CarCatalog.Load()?.Get(SelectedCarIndex)?.id ?? string.Empty;
    public static int SelectedTheme => Mathf.Clamp(PlayerPrefs.GetInt("SelectedTheme", 0), 0, ThemeNames.Length - 1);
    public static bool MusicEnabled => PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
    public static bool HapticsEnabled => PlayerPrefs.GetInt("HapticsEnabled", 1) == 1;
    public static bool DailyStoreGiftAvailable => PlayerPrefs.GetString("StoreDailyGiftDate", string.Empty) != TodayKey;
    public static int DailyTarget
    {
        get
        {
            ChallengeConfig config = ConfigManager.Current.challenges;
            return config.dailyTargetMin + (DateTime.UtcNow.Year * 367 + DateTime.UtcNow.DayOfYear) % Mathf.Max(1, config.dailyTargetRange);
        }
    }
    private static string TodayKey => DateTime.UtcNow.ToString("yyyyMMdd");

    private static string WeekKey
    {
        get
        {
            DateTime today = DateTime.UtcNow.Date;
            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            return today.AddDays(-daysSinceMonday).ToString("yyyyMMdd");
        }
    }

    public static int DailyBest
    {
        get { EnsureDailyState(); return PlayerPrefs.GetInt("DailyBest", 0); }
    }

    public static bool DailyCompleted
    {
        get { EnsureDailyState(); return PlayerPrefs.GetInt("DailyComplete", 0) == 1; }
    }

    public static string DailyChallengeSummary
    {
        get
        {
            EnsureDailyState();
            ChallengeConfig c = ConfigManager.Current.challenges;
            return $"SCORE RUSH - Reach {DailyTarget}\n{Mathf.Min(DailyBest, DailyTarget)}/{DailyTarget}  |  +{c.dailyTargetReward} GEMS\n\n" +
                   $"DAILY DRIVER - Finish {c.dailyRunsTarget} runs\n{Progress("DailyRuns", c.dailyRunsTarget)}  |  +{c.dailyRunsReward} GEMS\n\n" +
                   $"GEM HUNTER - Collect {c.dailyGemsTarget} gems\n{Progress("DailyGems", c.dailyGemsTarget)}  |  +{c.dailyGemsReward} GEMS\n\n" +
                   $"COMBO DASH - Reach combo {c.dailyComboTarget}\n{Progress("DailyCombo", c.dailyComboTarget)}  |  +{c.dailyComboReward} GEMS\n\n" +
                   $"SCORE SPRINT - Earn {c.dailyScoreTarget} score\n{Progress("DailyScore", c.dailyScoreTarget)}  |  +{c.dailyScoreReward} GEMS";
        }
    }

    public static string WeeklyChallengeSummary
    {
        get
        {
            EnsureWeeklyState();
            ChallengeConfig c = ConfigManager.Current.challenges;
            return $"ROAD REGULAR - Finish {c.weeklyRunsTarget} runs\n{Progress("WeeklyRuns", c.weeklyRunsTarget)}  |  +{c.weeklyRunsReward} GEMS\n\n" +
                   $"SCORE STORM - Earn {c.weeklyScoreTarget} score\n{Progress("WeeklyScore", c.weeklyScoreTarget)}  |  +{c.weeklyScoreReward} GEMS\n\n" +
                   $"GEM VAULT - Collect {c.weeklyGemsTarget} gems\n{Progress("WeeklyGems", c.weeklyGemsTarget)}  |  +{c.weeklyGemsReward} GEMS\n\n" +
                   $"COMBO CHAMPION - Reach combo {c.weeklyComboTarget}\n{Progress("WeeklyCombo", c.weeklyComboTarget)}  |  +{c.weeklyComboReward} GEMS\n\n" +
                   $"ROAD LEGEND - Reach score {c.weeklyBestTarget}\n{Progress("WeeklyBest", c.weeklyBestTarget)}  |  +{c.weeklyBestReward} GEMS";
        }
    }

    public static string MissionSummary
    {
        get
        {
            EnsureDailyState();
            ChallengeConfig c = ConfigManager.Current.challenges;
            return $"SCORE MASTER - Reach score {c.missionScoreTarget}\n{Progress("MissionScore", c.missionScoreTarget)}  |  +{c.missionScoreReward} GEMS\n\n" +
                   $"COMBO MASTER - Reach combo {c.missionComboTarget}\n{Progress("MissionCombo", c.missionComboTarget)}  |  +{c.missionComboReward} GEMS\n\n" +
                   $"GEM COLLECTOR - Collect {c.missionGemsTarget} gems\n{Progress("MissionGems", c.missionGemsTarget)}  |  +{c.missionGemsReward} GEMS\n\n" +
                   $"ROAD TEST - Finish {c.missionRunsTarget} runs\n{Progress("MissionRuns", c.missionRunsTarget)}  |  +{c.missionRunsReward} GEMS\n\n" +
                   $"POINT BANK - Earn {c.missionTotalScoreTarget} score\n{Progress("MissionTotalScore", c.missionTotalScoreTarget)}  |  +{c.missionTotalScoreReward} GEMS";
        }
    }

    public static string DailyMissionSummary => MissionSummary;

    public static bool HasSeenSpecialTileFtue(SpecialTileType type)
    {
        int bit = 1 << (int)type;
        return (PlayerPrefs.GetInt("FtueSeenMask", 0) & bit) != 0;
    }

    public static void MarkSpecialTileFtueSeen(SpecialTileType type)
    {
        int bit = 1 << (int)type;
        PlayerPrefs.SetInt("FtueSeenMask", PlayerPrefs.GetInt("FtueSeenMask", 0) | bit);
        Save();
    }

    public static int RegisterRunStarted(int beginnerRunCount)
    {
        beginnerRunCount = Mathf.Max(0, beginnerRunCount);
        if (!PlayerPrefs.HasKey(LifetimeRunsStartedKey))
        {
            // Existing installs predate this counter. Do not put experienced
            // players back into beginner speed for another three attempts.
            int recordedRuns = Mathf.Max(
                PlayerPrefs.GetInt("DailyRuns", 0),
                Mathf.Max(PlayerPrefs.GetInt("WeeklyRuns", 0), PlayerPrefs.GetInt("MissionRuns", 0)));
            if (BestScore > 0) recordedRuns = Mathf.Max(recordedRuns, beginnerRunCount);
            PlayerPrefs.SetInt(LifetimeRunsStartedKey, recordedRuns);
        }

        int runNumber = LifetimeRunsStarted + 1;
        PlayerPrefs.SetInt(LifetimeRunsStartedKey, runNumber);
        Save();
        return runNumber;
    }

    public static void AddGems(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt("TotalGems", TotalGems + amount);
        Save();
    }

    public static bool ClaimDailyStoreGift()
    {
        if (!DailyStoreGiftAvailable) return false;
        PlayerPrefs.SetString("StoreDailyGiftDate", TodayKey);
        PlayerPrefs.SetInt("TotalGems", TotalGems + ConfigManager.Current.economy.dailyGiftGems);
        Save();
        return true;
    }

    public static bool IsSkinUnlocked(int index) => (PlayerPrefs.GetInt("UnlockedSkins", 1) & (1 << index)) != 0;

    public static bool IsCarUnlocked(int index)
    {
        CarDefinition car = CarCatalog.Load()?.Get(index);
        if (car == null) return IsSkinUnlocked(Mathf.Clamp(index, 0, SkinNames.Length - 1));
        return car.startsUnlocked || GetUnlockedCarIds().Contains(car.id);
    }

    public static bool UnlockOrSelectCar(int index)
    {
        CarCatalog catalog = CarCatalog.Load();
        CarDefinition car = catalog?.Get(index);
        if (car == null) return UnlockOrSelectSkin(index);
        if (!IsCarUnlocked(index))
        {
            if (!SpendGems(car.gemCost)) return false;
            AddUnlockedCarId(car.id);
        }
        PlayerPrefs.SetString("SelectedCarId", car.id);
        PlayerPrefs.SetInt("SelectedSkin", index);
        Save();
        return true;
    }

    public static bool UnlockCarFromRewardedAd(int index)
    {
        CarDefinition car = CarCatalog.Load()?.Get(index);
        if (car == null || !car.rewardedAdUnlock) return false;
        AddUnlockedCarId(car.id);
        PlayerPrefs.SetString("SelectedCarId", car.id);
        PlayerPrefs.SetInt("SelectedSkin", index);
        Save();
        return true;
    }

    public static int GetCarMasteryXp(int index)
    {
        CarDefinition car = CarCatalog.Load()?.Get(index);
        return car == null ? 0 : PlayerPrefs.GetInt(MasteryKey(car.id), 0);
    }

    public static int GetCarMasteryLevel(int index)
    {
        int xp = GetCarMasteryXp(index);
        int level = 1;
        for (int i = 1; i < CarMasteryThresholds.Length; i++)
            if (xp >= CarMasteryThresholds[i]) level = i + 1;
        return level;
    }

    public static string GetCarMasteryProgressText(int index)
    {
        int xp = GetCarMasteryXp(index);
        int level = GetCarMasteryLevel(index);
        if (level >= CarMasteryThresholds.Length) return $"MASTERY {level}  MAX  •  PRISM TRAIL";
        int next = CarMasteryThresholds[level];
        string reward = level == 1 ? "NEON TRAIL" : level == 2 ? "GOLD PAINT" : level == 3 ? "LONG TRAIL" : "PRISM TRAIL";
        return $"MASTERY {level}  {xp}/{next}  •  NEXT: {reward}";
    }

    public static string AddCarMasteryProgress(int index, int xpAmount)
    {
        CarDefinition car = CarCatalog.Load()?.Get(index);
        if (car == null || xpAmount <= 0) return string.Empty;
        int previousLevel = GetCarMasteryLevel(index);
        string key = MasteryKey(car.id);
        int xp = Mathf.Clamp(PlayerPrefs.GetInt(key, 0) + xpAmount, 0, 9999999);
        PlayerPrefs.SetInt(key, xp);
        int level = GetCarMasteryLevel(index);
        Save();
        if (level <= previousLevel) return string.Empty;

        GameAnalytics.CarMasteryAdvanced(car.id, level, xp);
        string reward = level == 2 ? "NEON TRAIL" : level == 3 ? "GOLD PAINT" : level == 4 ? "LONG TRAIL" : "PRISM TRAIL";
        return $"{car.displayName.ToUpperInvariant()} MASTERY {level} — {reward} UNLOCKED";
    }

    public static List<string> GetCarMasteryForCloud()
    {
        List<string> result = new List<string>();
        CarCatalog catalog = CarCatalog.Load();
        if (catalog == null) return result;
        foreach (CarDefinition car in catalog.Cars)
        {
            if (car == null || string.IsNullOrWhiteSpace(car.id)) continue;
            int xp = PlayerPrefs.GetInt(MasteryKey(car.id), 0);
            if (xp <= 0) continue;
            string encodedId = Convert.ToBase64String(Encoding.UTF8.GetBytes(car.id));
            result.Add($"{encodedId}:{xp}");
        }
        return result;
    }

    public static void MergeCarMasteryFromCloud(IEnumerable<string> entries)
    {
        if (entries == null) return;
        foreach (string entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            int separator = entry.LastIndexOf(':');
            if (separator <= 0 || !int.TryParse(entry.Substring(separator + 1), out int xp)) continue;
            try
            {
                string carId = Encoding.UTF8.GetString(Convert.FromBase64String(entry.Substring(0, separator)));
                string key = MasteryKey(carId);
                PlayerPrefs.SetInt(key, Mathf.Max(PlayerPrefs.GetInt(key, 0), xp));
            }
            catch { }
        }
    }

    public static string GetPostRunObjective(int score, int bestScoreBeforeRun, out string objectiveType)
    {
        if (bestScoreBeforeRun > score && bestScoreBeforeRun - score <= Mathf.Max(5, bestScoreBeforeRun / 5))
        {
            objectiveType = "near_personal_best";
            int gap = bestScoreBeforeRun - score;
            return $"NEXT GOAL: {gap} POINT{(gap == 1 ? string.Empty : "S")} FROM YOUR BEST";
        }

        ChallengeConfig challenges = ConfigManager.Current.challenges;
        int dailyRuns = PlayerPrefs.GetInt("DailyRuns", 0);
        if (dailyRuns < challenges.dailyRunsTarget)
        {
            objectiveType = "daily_runs";
            int remaining = challenges.dailyRunsTarget - dailyRuns;
            return $"NEXT GOAL: {remaining} MORE RUN{(remaining == 1 ? string.Empty : "S")} FOR DAILY DRIVER";
        }

        int dailyCombo = PlayerPrefs.GetInt("DailyCombo", 0);
        if (dailyCombo < challenges.dailyComboTarget)
        {
            objectiveType = "daily_combo";
            return $"NEXT GOAL: REACH COMBO {challenges.dailyComboTarget}";
        }

        CarCatalog catalog = CarCatalog.Load();
        int cheapestGap = int.MaxValue;
        string carName = string.Empty;
        if (catalog != null)
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                CarDefinition car = catalog.Get(i);
                if (car == null || IsCarUnlocked(i) || car.gemCost <= TotalGems) continue;
                int gap = car.gemCost - TotalGems;
                if (gap >= cheapestGap) continue;
                cheapestGap = gap;
                carName = car.displayName;
            }
        }

        if (cheapestGap != int.MaxValue)
        {
            objectiveType = "next_car";
            return $"NEXT GOAL: {cheapestGap} GEMS UNTIL {carName.ToUpperInvariant()}";
        }

        objectiveType = score > bestScoreBeforeRun ? "beat_new_best" : "improve_combo";
        return score > bestScoreBeforeRun
            ? "NEXT GOAL: DEFEND YOUR NEW PERSONAL BEST"
            : "NEXT GOAL: BUILD A LONGER PERFECT COMBO";
    }

    public static string[] GetUnlockedCarIdsForCloud()
    {
        HashSet<string> ids = GetUnlockedCarIds();
        CarCatalog catalog = CarCatalog.Load();
        if (catalog != null)
            foreach (CarDefinition car in catalog.Cars)
                if (car != null && car.startsUnlocked) ids.Add(car.id);
        string[] result = new string[ids.Count];
        ids.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    public static void MergeUnlockedCarsFromCloud(IEnumerable<string> cloudIds)
    {
        HashSet<string> ids = GetUnlockedCarIds();
        if (cloudIds != null)
            foreach (string id in cloudIds)
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id.Trim());
        PlayerPrefs.SetString("UnlockedCarIds", string.Join("|", ids));
    }

    public static void SelectCloudCarIfUnlocked(string carId)
    {
        CarCatalog catalog = CarCatalog.Load();
        int index = catalog?.IndexOf(carId) ?? -1;
        if (index < 0 || !IsCarUnlocked(index)) return;
        PlayerPrefs.SetString("SelectedCarId", carId);
        PlayerPrefs.SetInt("SelectedSkin", index);
    }

    public static bool UnlockOrSelectSkin(int index)
    {
        CarCatalog catalog = CarCatalog.Load();
        if (catalog != null && catalog.Count > 0) return UnlockOrSelectCar(index);
        index = Mathf.Clamp(index, 0, SkinNames.Length - 1);
        if (!IsSkinUnlocked(index))
        {
            if (!SpendGems(SkinCosts[index])) return false;
            PlayerPrefs.SetInt("UnlockedSkins", PlayerPrefs.GetInt("UnlockedSkins", 1) | (1 << index));
        }
        PlayerPrefs.SetInt("SelectedSkin", index);
        Save();
        return true;
    }

    public static bool IsThemeUnlocked(int index) => (PlayerPrefs.GetInt("UnlockedThemes", 1) & (1 << index)) != 0;

    public static bool UnlockOrSelectTheme(int index)
    {
        index = Mathf.Clamp(index, 0, ThemeNames.Length - 1);
        if (!IsThemeUnlocked(index))
        {
            if (!SpendGems(ThemeCosts[index])) return false;
            PlayerPrefs.SetInt("UnlockedThemes", PlayerPrefs.GetInt("UnlockedThemes", 1) | (1 << index));
        }
        PlayerPrefs.SetInt("SelectedTheme", index);
        Save();
        return true;
    }

    public static string RecordRun(int score, int combo, int runGems, bool daily)
    {
        ChallengeConfig c = ConfigManager.Current.challenges;
        if (score > BestScore) PlayerPrefs.SetInt("HighScore", score);
        string unlocked = string.Empty;

        if (score >= c.achievementScore50Target && UnlockAchievement("AchievementScore50", c.achievementScore50Reward)) unlocked = $"ACHIEVEMENT: ROAD ROOKIE +{c.achievementScore50Reward}";
        if (combo >= c.achievementCombo10Target && UnlockAchievement("AchievementCombo10", c.achievementCombo10Reward)) unlocked = $"ACHIEVEMENT: COMBO MASTER +{c.achievementCombo10Reward}";
        if (score >= c.achievementScore200Target && UnlockAchievement("AchievementScore200", c.achievementScore200Reward)) unlocked = $"ACHIEVEMENT: ROAD LEGEND +{c.achievementScore200Reward}";

        EnsureDailyState();
        EnsureWeeklyState();
        PlayerPrefs.SetInt("DailyRuns", PlayerPrefs.GetInt("DailyRuns", 0) + 1);
        PlayerPrefs.SetInt("DailyGems", PlayerPrefs.GetInt("DailyGems", 0) + runGems);
        PlayerPrefs.SetInt("DailyCombo", Mathf.Max(PlayerPrefs.GetInt("DailyCombo", 0), combo));
        PlayerPrefs.SetInt("DailyScore", PlayerPrefs.GetInt("DailyScore", 0) + score);
        PlayerPrefs.SetInt("MissionScore", Mathf.Max(PlayerPrefs.GetInt("MissionScore", 0), score));
        PlayerPrefs.SetInt("MissionCombo", Mathf.Max(PlayerPrefs.GetInt("MissionCombo", 0), combo));
        PlayerPrefs.SetInt("MissionGems", PlayerPrefs.GetInt("MissionGems", 0) + runGems);
        PlayerPrefs.SetInt("MissionRuns", PlayerPrefs.GetInt("MissionRuns", 0) + 1);
        PlayerPrefs.SetInt("MissionTotalScore", PlayerPrefs.GetInt("MissionTotalScore", 0) + score);

        PlayerPrefs.SetInt("WeeklyRuns", PlayerPrefs.GetInt("WeeklyRuns", 0) + 1);
        PlayerPrefs.SetInt("WeeklyScore", PlayerPrefs.GetInt("WeeklyScore", 0) + score);
        PlayerPrefs.SetInt("WeeklyGems", PlayerPrefs.GetInt("WeeklyGems", 0) + runGems);
        PlayerPrefs.SetInt("WeeklyCombo", Mathf.Max(PlayerPrefs.GetInt("WeeklyCombo", 0), combo));
        PlayerPrefs.SetInt("WeeklyBest", Mathf.Max(PlayerPrefs.GetInt("WeeklyBest", 0), score));

        if (daily)
        {
            PlayerPrefs.SetInt("DailyBest", Mathf.Max(DailyBest, score));
            if (score >= DailyTarget && !DailyCompleted)
            {
                unlocked = "DAILY CHALLENGE READY - CLAIM GEMS IN CHALLENGES";
            }
        }

        Save();
        return unlocked;
    }

    public static void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt("MusicEnabled", enabled ? 1 : 0);
        Save();
    }

    public static void SetHapticsEnabled(bool enabled)
    {
        PlayerPrefs.SetInt("HapticsEnabled", enabled ? 1 : 0);
        Save();
    }

    public static void Save()
    {
        PlayerPrefs.Save();
        CloudSaveManager.NotifyLocalProgressChanged();
    }

    private static bool SpendGems(int amount)
    {
        if (TotalGems < amount) return false;
        PlayerPrefs.SetInt("TotalGems", TotalGems - amount);
        return true;
    }

    private static HashSet<string> GetUnlockedCarIds()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        string saved = PlayerPrefs.GetString("UnlockedCarIds", string.Empty);
        foreach (string id in saved.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)) ids.Add(id);
        return ids;
    }

    private static void AddUnlockedCarId(string id)
    {
        HashSet<string> ids = GetUnlockedCarIds();
        ids.Add(id);
        PlayerPrefs.SetString("UnlockedCarIds", string.Join("|", ids));
    }

    private static string MasteryKey(string carId)
    {
        return "CarMastery." + Hash128.Compute(carId ?? string.Empty);
    }

    private static bool UnlockAchievement(string key, int reward)
    {
        if (PlayerPrefs.GetInt(key, 0) == 1) return false;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.SetInt("TotalGems", TotalGems + reward);
        return true;
    }

    public static void PrepareChallengeState()
    {
        EnsureDailyState();
        EnsureWeeklyState();
    }

    private static string Progress(string key, int target)
    {
        return $"{Mathf.Min(PlayerPrefs.GetInt(key, 0), target)}/{target}";
    }

    private static void EnsureDailyState()
    {
        string today = TodayKey;
        if (PlayerPrefs.GetString("DailyDate", string.Empty) == today) return;
        PlayerPrefs.SetString("DailyDate", today);
        PlayerPrefs.SetInt("DailyBest", 0);
        PlayerPrefs.SetInt("DailyComplete", 0);
        PlayerPrefs.SetInt("DailyRuns", 0);
        PlayerPrefs.SetInt("DailyGems", 0);
        PlayerPrefs.SetInt("DailyCombo", 0);
        PlayerPrefs.SetInt("DailyScore", 0);
        PlayerPrefs.SetInt("DailyRunsRewarded", 0);
        PlayerPrefs.SetInt("DailyGemsRewarded", 0);
        PlayerPrefs.SetInt("DailyComboRewarded", 0);
        PlayerPrefs.SetInt("DailyScoreRewarded", 0);
        PlayerPrefs.SetInt("MissionScore", 0);
        PlayerPrefs.SetInt("MissionCombo", 0);
        PlayerPrefs.SetInt("MissionGems", 0);
        PlayerPrefs.SetInt("MissionRuns", 0);
        PlayerPrefs.SetInt("MissionTotalScore", 0);
        PlayerPrefs.SetInt("MissionScoreRewarded", 0);
        PlayerPrefs.SetInt("MissionComboRewarded", 0);
        PlayerPrefs.SetInt("MissionGemsRewarded", 0);
        PlayerPrefs.SetInt("MissionRunsRewarded", 0);
        PlayerPrefs.SetInt("MissionTotalScoreRewarded", 0);
        Save();
    }

    private static void EnsureWeeklyState()
    {
        string week = WeekKey;
        if (PlayerPrefs.GetString("WeeklyDate", string.Empty) == week) return;
        PlayerPrefs.SetString("WeeklyDate", week);
        PlayerPrefs.SetInt("WeeklyRuns", 0);
        PlayerPrefs.SetInt("WeeklyScore", 0);
        PlayerPrefs.SetInt("WeeklyGems", 0);
        PlayerPrefs.SetInt("WeeklyCombo", 0);
        PlayerPrefs.SetInt("WeeklyBest", 0);
        PlayerPrefs.SetInt("WeeklyRunsRewarded", 0);
        PlayerPrefs.SetInt("WeeklyScoreRewarded", 0);
        PlayerPrefs.SetInt("WeeklyGemsRewarded", 0);
        PlayerPrefs.SetInt("WeeklyComboRewarded", 0);
        PlayerPrefs.SetInt("WeeklyBestRewarded", 0);
        Save();
    }
}
