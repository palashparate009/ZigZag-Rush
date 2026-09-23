using System;
using UnityEngine;

static class Program
{
    private static int checks;
    private static void Check(bool valid, string message)
    {
        checks++;
        if (!valid) throw new Exception(message);
    }

    private static void Reset()
    {
        PlayerPrefs.DeleteAll();
        GameProgress.PrepareChallengeState();
        // Achievements retain their automatic payouts; isolate challenge payouts.
        PlayerPrefs.SetInt("AchievementScore50", 1);
        PlayerPrefs.SetInt("AchievementScore200", 1);
        PlayerPrefs.SetInt("AchievementCombo10", 1);
    }

    public static void Main()
    {
        for (int tab = 0; tab < 3; tab++)
        for (int index = 0; index < ChallengeProgress.CardsPerTab; index++)
        {
            Reset();
            var incomplete = ChallengeProgress.Get(tab, index);
            Check(!ChallengeProgress.TryClaim(tab, index, incomplete.Period, out _), "Incomplete reward paid");
            for (int run = 0; run < 15; run++) GameProgress.RecordRun(1500, 30, 100, true);
            Check(GameProgress.TotalGems == 0, "Run automatically paid challenge gems");
            var ready = ChallengeProgress.Get(tab, index);
            Check(ready.CanClaim && ready.Remaining == 0, "Completed challenge is not claimable");
            Check(!ChallengeProgress.TryClaim(tab, index, "expired-period", out _), "Stale card paid");
            int notifications = CloudSaveManager.Notifications;
            Check(ChallengeProgress.TryClaim(tab, index, ready.Period, out int reward), "Claim failed");
            Check(reward == ready.Reward && GameProgress.TotalGems == reward, "Incorrect reward");
            Check(CloudSaveManager.Notifications > notifications, "Claim not queued for cloud save");
            Check(ChallengeProgress.Get(tab, index).Claimed, "Claim was not persisted");
            Check(!ChallengeProgress.TryClaim(tab, index, ready.Period, out _), "Repeated tap paid twice");
            Check(GameProgress.TotalGems == reward, "Repeated tap changed balance");
        }

        Reset();
        GameProgress.RecordRun(1500, 30, 100, true);
        var legacy = ChallengeProgress.Get(0, 0);
        PlayerPrefs.SetInt("DailyComplete", 1);
        Check(!ChallengeProgress.TryClaim(0, 0, legacy.Period, out _), "Legacy auto-paid reward paid again");
        var daily = ChallengeProgress.Get(0, 2);
        PlayerPrefs.SetString("DailyDate", "20000101");
        Check(!ChallengeProgress.TryClaim(0, 2, daily.Period, out _), "Expired daily progress paid");
        Check(ChallengeProgress.Get(0, 2).Current == 0, "Daily progress not reset");
        Check(ChallengeProgress.Get(2, 2).Current == 0, "Mission reset changed");
        var weekly = ChallengeProgress.Get(1, 2);
        Check(weekly.Current > 0, "Daily reset erased weekly progress");
        PlayerPrefs.SetString("WeeklyDate", "20000101");
        Check(!ChallengeProgress.TryClaim(1, 2, weekly.Period, out _), "Expired weekly progress paid");
        Check(ChallengeProgress.Get(1, 2).Current == 0, "Weekly progress not reset");

        Reset();
        GameProgress.RecordRun(1500, 30, 100, true);
        var full = ChallengeProgress.Get(0, 0);
        PlayerPrefs.SetInt("TotalGems", int.MaxValue);
        Check(!ChallengeProgress.TryClaim(0, 0, full.Period, out _), "Balance overflow");
        Check(!ChallengeProgress.Get(0, 0).Claimed, "Failed claim consumed reward");
        Check(!ChallengeProgress.TryClaim(5, 5, full.Period, out _), "Invalid challenge accepted");
        Console.WriteLine($"PASS: {checks} challenge checks; real progress/claim code, in-memory storage.");
    }
}
