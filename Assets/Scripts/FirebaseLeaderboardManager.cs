using System;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using GooglePlayGames.BasicApi;
using UnityEngine;

/// <summary>
/// Authoritative in-game leaderboard. Google Play Games is still mirrored for the
/// platform UI, but its Public collection intentionally excludes private profiles.
/// Firebase lets every authenticated ZigZag Rush player appear in our own UI.
/// </summary>
public static class FirebaseLeaderboardManager
{
    public sealed class Entry
    {
        public int Rank;
        public string PlayerName;
        public long Score;
        public bool IsLocalPlayer;
    }

    public sealed class Page
    {
        public readonly List<Entry> Entries = new List<Entry>();
        public int PageNumber;
        public bool HasPrevious;
        public bool HasNext;
    }

    private const string RootCollection = "leaderboards";
    private const string ScoresCollection = "scores";
    private const string PendingPrefix = "FirebaseLeaderboard.Pending.";
    private const string UploadedPrefix = "FirebaseLeaderboard.Uploaded.";
    private const int MaximumScore = 1000000;

    public static void SubmitRunScore(int score, bool dailyRun)
    {
        score = Mathf.Clamp(score, 0, MaximumScore);
        if (score <= 0) return;

        QueueMaximum(AllTimeBoardKey, Mathf.Max(score, GameProgress.BestScore));
        QueueMaximum(WeeklyBoardKey, score);
        if (dailyRun) QueueMaximum(DailyBoardKey, score);
        FlushPendingScores();
    }

    public static void SyncPermanentBest()
    {
        int best = Mathf.Clamp(GameProgress.BestScore, 0, MaximumScore);
        if (best > 0) QueueMaximum(AllTimeBoardKey, best);
        FlushPendingScores();
    }

    public static void FlushPendingScores()
    {
        if (!TryGetServices(out FirebaseAuth auth, out FirebaseFirestore firestore)) return;

        FlushBoard(auth, firestore, AllTimeBoardKey);
        FlushBoard(auth, firestore, WeeklyBoardKey);
        FlushBoard(auth, firestore, DailyBoardKey);
    }

    public static bool TryLoadScores(LeaderboardTimeSpan span, int pageNumber, int pageSize,
        Action<bool, Page, string> completed)
    {
        if (!TryGetServices(out FirebaseAuth auth, out FirebaseFirestore firestore)) return false;

        pageNumber = Mathf.Max(1, pageNumber);
        pageSize = Mathf.Max(1, pageSize);
        string boardKey = BoardKey(span);
        int requestedCount = checked(pageNumber * pageSize + 1);
        string localUserId = auth.CurrentUser.UserId;

        Query query = Scores(firestore, boardKey).OrderByDescending("score").Limit(requestedCount);
        query.GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                string reason = task.Exception?.GetBaseException().Message ?? "request cancelled";
                Debug.LogWarning($"Firebase leaderboard load failed for {boardKey}: {reason}");
                completed?.Invoke(false, new Page { PageNumber = pageNumber },
                    "COULD NOT LOAD SCORES");
                return;
            }

            List<Entry> ranked = new List<Entry>();
            int rank = 1;
            foreach (DocumentSnapshot document in task.Result.Documents)
            {
                Dictionary<string, object> data = document.ToDictionary();
                long score = ReadLong(data, "score");
                if (score <= 0) continue;
                ranked.Add(new Entry
                {
                    Rank = rank++,
                    PlayerName = CleanName(ReadString(data, "name")),
                    Score = score,
                    IsLocalPlayer = string.Equals(document.Id, localUserId, StringComparison.Ordinal)
                });
            }

            int start = (pageNumber - 1) * pageSize;
            Page page = new Page
            {
                PageNumber = pageNumber,
                HasPrevious = pageNumber > 1,
                HasNext = ranked.Count > start + pageSize
            };
            for (int i = start; i < ranked.Count && page.Entries.Count < pageSize; i++)
                page.Entries.Add(ranked[i]);

            Debug.Log($"Firebase leaderboard: loaded {page.Entries.Count} {boardKey} score(s) " +
                      $"for page {pageNumber}.");
            completed?.Invoke(true, page, page.Entries.Count == 0 ? "NO SCORES YET" : string.Empty);
        });
        return true;
    }

    private static void FlushBoard(FirebaseAuth auth, FirebaseFirestore firestore, string boardKey)
    {
        string pendingKey = PendingPrefix + boardKey;
        int score = PlayerPrefs.GetInt(pendingKey, 0);
        if (score <= 0) return;

        string userId = auth.CurrentUser.UserId;
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "name", CurrentPlayerName() },
            { "score", (long)score },
            { "updated_at", FieldValue.ServerTimestamp }
        };

        Scores(firestore, boardKey).Document(userId).SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogWarning($"Firebase leaderboard upload failed for {boardKey}: " +
                                     (task.Exception?.GetBaseException().Message ?? "request cancelled"));
                    return;
                }

                // Only remove the value that this request uploaded. A better score may have
                // been queued while the network request was in flight.
                if (PlayerPrefs.GetInt(pendingKey, 0) <= score)
                {
                    PlayerPrefs.DeleteKey(pendingKey);
                }
                string uploadedKey = UploadedPrefix + boardKey;
                PlayerPrefs.SetInt(uploadedKey,
                    Mathf.Max(PlayerPrefs.GetInt(uploadedKey, 0), score));
                PlayerPrefs.Save();
                Debug.Log($"Firebase leaderboard: uploaded {score} to {boardKey}.");
            });
    }

    private static CollectionReference Scores(FirebaseFirestore firestore, string boardKey)
    {
        return firestore.Collection(RootCollection).Document(boardKey).Collection(ScoresCollection);
    }

    private static bool TryGetServices(out FirebaseAuth auth, out FirebaseFirestore firestore)
    {
        auth = null;
        firestore = null;
        if (!CloudSaveManager.IsSignedIn || CloudSaveManager.IsCloudSaveDisabled) return false;
        try
        {
            auth = FirebaseAuth.DefaultInstance;
            if (auth?.CurrentUser == null) return false;
            firestore = FirebaseFirestore.DefaultInstance;
            return firestore != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Firebase leaderboard is unavailable: {exception.Message}");
            return false;
        }
    }

    private static void QueueMaximum(string boardKey, int score)
    {
        string key = PendingPrefix + boardKey;
        int knownMaximum = Mathf.Max(PlayerPrefs.GetInt(key, 0),
            PlayerPrefs.GetInt(UploadedPrefix + boardKey, 0));
        if (score <= knownMaximum) return;
        PlayerPrefs.SetInt(key, score);
        PlayerPrefs.Save();
    }

    private static string BoardKey(LeaderboardTimeSpan span)
    {
        switch (span)
        {
            case LeaderboardTimeSpan.Daily: return DailyBoardKey;
            case LeaderboardTimeSpan.Weekly: return WeeklyBoardKey;
            default: return AllTimeBoardKey;
        }
    }

    private static string AllTimeBoardKey => "all_time";
    private static string DailyBoardKey => "daily_" + DateTime.UtcNow.ToString("yyyyMMdd");
    private static string WeeklyBoardKey
    {
        get
        {
            DateTime today = DateTime.UtcNow.Date;
            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            return "weekly_" + today.AddDays(-daysSinceMonday).ToString("yyyyMMdd");
        }
    }

    private static string CurrentPlayerName()
    {
        string name = PlayGamesSignInManager.AuthenticatedPlayerName;
        if (string.IsNullOrWhiteSpace(name)) name = CloudSaveManager.PlayerName;
        return CleanName(name);
    }

    private static string CleanName(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "PLAYER" : name.Trim();
        return name.Length <= 32 ? name : name.Substring(0, 32);
    }

    private static long ReadLong(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out object value) || value == null) return 0;
        try { return Convert.ToInt64(value); }
        catch { return 0; }
    }

    private static string ReadString(Dictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out object value) ? value as string ?? string.Empty : string.Empty;
    }
}
