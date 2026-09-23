using System;
using UnityEngine;

// Uses the existing cloud-synced progress and rewarded keys.
public static class ChallengeProgress
{
    public const int CardsPerTab = 5;

    public readonly struct Entry
    {
        public readonly string Title, Description, ClaimKey, Period;
        public readonly int Current, Target, Reward;
        public readonly bool Claimed;
        public bool CanClaim => !Claimed && Current >= Target && Reward > 0;
        public int Remaining => Mathf.Max(0, Target - Current);

        public Entry(string title, string description, string progressKey, string claimKey,
            int target, int reward, string period)
        {
            Title = title;
            Description = description;
            ClaimKey = claimKey;
            Period = period;
            Target = Mathf.Max(1, target);
            Reward = Mathf.Max(0, reward);
            Current = Mathf.Clamp(PlayerPrefs.GetInt(progressKey, 0), 0, Target);
            Claimed = PlayerPrefs.GetInt(claimKey, 0) == 1;
        }
    }

    public static Entry Get(int tab, int index)
    {
        if (tab < 0 || tab > 2 || index < 0 || index >= CardsPerTab)
            throw new ArgumentOutOfRangeException();
        GameProgress.PrepareChallengeState();
        ChallengeConfig c = ConfigManager.Current.challenges;
        string period = PlayerPrefs.GetString(tab == 1 ? "WeeklyDate" : "DailyDate", string.Empty);
        switch (tab * CardsPerTab + index)
        {
            case 0: return new Entry("SCORE RUSH", "Reach this score in a daily run", "DailyBest", "DailyComplete", GameProgress.DailyTarget, c.dailyTargetReward, period);
            case 1: return new Entry("DAILY DRIVER", "Finish runs", "DailyRuns", "DailyRunsRewarded", c.dailyRunsTarget, c.dailyRunsReward, period);
            case 2: return new Entry("GEM HUNTER", "Collect gems", "DailyGems", "DailyGemsRewarded", c.dailyGemsTarget, c.dailyGemsReward, period);
            case 3: return new Entry("COMBO DASH", "Reach a combo", "DailyCombo", "DailyComboRewarded", c.dailyComboTarget, c.dailyComboReward, period);
            case 4: return new Entry("SCORE SPRINT", "Earn total score", "DailyScore", "DailyScoreRewarded", c.dailyScoreTarget, c.dailyScoreReward, period);
            case 5: return new Entry("ROAD REGULAR", "Finish runs", "WeeklyRuns", "WeeklyRunsRewarded", c.weeklyRunsTarget, c.weeklyRunsReward, period);
            case 6: return new Entry("SCORE STORM", "Earn total score", "WeeklyScore", "WeeklyScoreRewarded", c.weeklyScoreTarget, c.weeklyScoreReward, period);
            case 7: return new Entry("GEM VAULT", "Collect gems", "WeeklyGems", "WeeklyGemsRewarded", c.weeklyGemsTarget, c.weeklyGemsReward, period);
            case 8: return new Entry("COMBO CHAMPION", "Reach a combo", "WeeklyCombo", "WeeklyComboRewarded", c.weeklyComboTarget, c.weeklyComboReward, period);
            case 9: return new Entry("ROAD LEGEND", "Reach a score", "WeeklyBest", "WeeklyBestRewarded", c.weeklyBestTarget, c.weeklyBestReward, period);
            case 10: return new Entry("SCORE MASTER", "Reach a score", "MissionScore", "MissionScoreRewarded", c.missionScoreTarget, c.missionScoreReward, period);
            case 11: return new Entry("COMBO MASTER", "Reach a combo", "MissionCombo", "MissionComboRewarded", c.missionComboTarget, c.missionComboReward, period);
            case 12: return new Entry("GEM COLLECTOR", "Collect gems", "MissionGems", "MissionGemsRewarded", c.missionGemsTarget, c.missionGemsReward, period);
            case 13: return new Entry("ROAD TEST", "Finish runs", "MissionRuns", "MissionRunsRewarded", c.missionRunsTarget, c.missionRunsReward, period);
            case 14: return new Entry("POINT BANK", "Earn total score", "MissionTotalScore", "MissionTotalScoreRewarded", c.missionTotalScoreTarget, c.missionTotalScoreReward, period);
            default: throw new ArgumentOutOfRangeException();
        }
    }

    public static bool TryClaim(int tab, int index, string displayedPeriod, out int reward)
    {
        reward = 0;
        if (tab < 0 || tab > 2 || index < 0 || index >= CardsPerTab) return false;
        Entry entry = Get(tab, index); // Recheck the saved state on every click.
        if (!entry.CanClaim || entry.Period != displayedPeriod ||
            (long)GameProgress.TotalGems + entry.Reward > int.MaxValue) return false;
        PlayerPrefs.SetInt(entry.ClaimKey, 1);
        PlayerPrefs.SetInt("TotalGems", GameProgress.TotalGems + entry.Reward);
        GameProgress.Save();
        reward = entry.Reward;
        return true;
    }
}
