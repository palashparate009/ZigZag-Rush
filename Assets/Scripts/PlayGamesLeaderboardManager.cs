using System;
using System.Collections.Generic;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public static class PlayGamesLeaderboardManager
{
    public const int PageSize = 20;

    public sealed class LeaderboardEntry
    {
        public int Rank;
        public string PlayerName;
        public string FormattedScore;
        public bool IsLocalPlayer;
    }

    public sealed class LeaderboardPage
    {
        public readonly List<LeaderboardEntry> Entries = new List<LeaderboardEntry>();
        public object PreviousPageToken;
        public object NextPageToken;
    }

    private sealed class FirebasePageToken
    {
        public readonly int PageNumber;
        public FirebasePageToken(int pageNumber) { PageNumber = pageNumber; }
    }

    private const string PendingMainScoreKey = "PendingMainLeaderboardScore";
    private const string PendingDailyScoreKey = "PendingDailyLeaderboardScore";
    private const string PendingDailyScoreDateKey = "PendingDailyLeaderboardScoreDate";

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ConfigManager.Current.services.mainLeaderboardId);

    public static void SubmitRunScore(int score, bool dailyRun)
    {
        // The in-game board uses Firebase so every authenticated player is visible.
        // Google Play Games remains a platform mirror for its native leaderboard UI.
        FirebaseLeaderboardManager.SubmitRunScore(score, dailyRun);
        int mainScore = Mathf.Max(score, GameProgress.BestScore);
        if (mainScore <= 0) return;
        SavePending(PendingMainScoreKey, mainScore);
        if (dailyRun)
        {
            EnsureCurrentDailyPendingScore();
            SavePending(PendingDailyScoreKey, Mathf.Max(score, GameProgress.DailyBest));
        }
        Debug.Log($"Play Games leaderboard: queued main best {mainScore}, run score {score} " +
                  $"(daily run: {dailyRun}).");
        TryFlushPendingScores();
    }

    public static void SyncSavedBestScores()
    {
        int mainBest = GameProgress.BestScore;
        if (mainBest > 0) SavePending(PendingMainScoreKey, mainBest);

        int dailyBest = GameProgress.DailyBest;
        EnsureCurrentDailyPendingScore();
        if (dailyBest > 0) SavePending(PendingDailyScoreKey, dailyBest);

        Debug.Log($"Play Games leaderboard: synchronizing saved bests (main {mainBest}, daily {dailyBest}).");
        TryFlushPendingScores();
    }

    public static void TryFlushPendingScores()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!PlayGamesPlatform.Instance.IsAuthenticated())
        {
            Debug.LogWarning("Play Games leaderboard: score is saved locally and will upload after sign-in.");
            return;
        }
        ReportPending(PendingMainScoreKey, ConfigManager.Current.services.mainLeaderboardId, "main");
        EnsureCurrentDailyPendingScore();
        ReportPending(PendingDailyScoreKey, ConfigManager.Current.services.dailyLeaderboardId, "daily_seed");
#endif
    }

    public static bool ShowMain(LeaderboardTimeSpan span)
    {
        return Show(ConfigManager.Current.services.mainLeaderboardId, "main", span);
    }

    public static bool ShowDaily(LeaderboardTimeSpan span)
    {
        string dailyId = ConfigManager.Current.services.dailyLeaderboardId;
        if (string.IsNullOrWhiteSpace(dailyId)) dailyId = ConfigManager.Current.services.mainLeaderboardId;
        return Show(dailyId, "daily_seed", span);
    }

    public static void LoadMainScores(LeaderboardTimeSpan span, object pageToken,
        Action<bool, LeaderboardPage, string> completed)
    {
        bool dailyRunBoard = span == LeaderboardTimeSpan.Daily &&
                             !string.IsNullOrWhiteSpace(ConfigManager.Current.services.dailyLeaderboardId);
        string leaderboardId = dailyRunBoard
            ? ConfigManager.Current.services.dailyLeaderboardId
            : ConfigManager.Current.services.mainLeaderboardId;
        string boardName = dailyRunBoard ? "daily_run" : "main";
        string pendingScoreKey = dailyRunBoard ? PendingDailyScoreKey : PendingMainScoreKey;
        if (string.IsNullOrWhiteSpace(leaderboardId))
        {
            GameAnalytics.LeaderboardAction("load_not_configured", boardName, false);
            completed?.Invoke(false, new LeaderboardPage(), "LEADERBOARD IS NOT CONFIGURED");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!PlayGamesPlatform.Instance.IsAuthenticated())
        {
            GameAnalytics.LeaderboardAction("load_not_signed_in", boardName, false);
            completed?.Invoke(false, new LeaderboardPage(),
                "SIGN IN WITH GOOGLE PLAY GAMES TO VIEW SCORES");
            return;
        }

        int savedBest = dailyRunBoard ? GameProgress.DailyBest : GameProgress.BestScore;
        if (dailyRunBoard) EnsureCurrentDailyPendingScore();
        if (savedBest > 0) SavePending(pendingScoreKey, savedBest);

        bool pendingUploadSucceeded = true;
        void LoadGooglePlayGamesScores()
        {
            bool triedPlayerCentered = false;
            Action<LeaderboardScoreData> handleScores = null;
            handleScores = data =>
            {
                if (data == null || !data.Valid)
                {
                    string status = data == null ? "NO RESPONSE" : data.Status.ToString().ToUpperInvariant();
                    Debug.LogWarning($"Play Games leaderboard: load failed ({status}).");
                    GameAnalytics.LeaderboardAction("load_" + status.ToLowerInvariant(), boardName, false);
                    completed?.Invoke(false, new LeaderboardPage(), $"COULD NOT LOAD SCORES ({status})");
                    return;
                }

                List<IScore> scores = new List<IScore>(data.Scores ?? Array.Empty<IScore>());
                int serverPageScoreCount = scores.Count;
                string playerScoreSummary = data.PlayerScore == null
                    ? "none"
                    : $"{data.PlayerScore.value} at rank {data.PlayerScore.rank}";
                Debug.Log($"Play Games leaderboard: loaded {scores.Count} score(s) for {span}; " +
                          $"approximate count {data.ApproximateCount}; player score {playerScoreSummary}; " +
                          $"status {data.Status}.");

                string localPlayerId = PlayGamesPlatform.Instance.GetUserId();
                IScore localPlayerScore = FindLocalPlayerScore(data, localPlayerId);

                void FinishPage()
                {
                    // Play Games can return the signed-in player's score separately from Scores,
                    // especially for a private/new profile or while its public rank is settling.
                    // Always merge that row instead of only doing so when the public page is empty.
                    // Pin the signed-in player's row only on the first page. The SDK includes
                    // PlayerScore metadata on later pages too; adding it there would repeat the
                    // same player on every page.
                    if (pageToken == null && localPlayerScore != null &&
                        !ContainsScore(scores, localPlayerScore, localPlayerId))
                        scores.Add(localPlayerScore);

                    LeaderboardPage page = new LeaderboardPage
                    {
                        // Google creates a backward token even for the first top-scores request.
                        // Hiding it here prevents a bogus PREVIOUS button on page 1.
                        PreviousPageToken = pageToken == null ? null : data.PrevPageToken,
                        // The Android plugin constructs a forward token unconditionally. A short
                        // server page is the reliable indication that there is no following page.
                        NextPageToken = serverPageScoreCount >= PageSize ? data.NextPageToken : null
                    };
                    if (scores.Count == 0)
                    {
                        GameAnalytics.LeaderboardAction("load_empty", boardName, true);
                        completed?.Invoke(true, page, pendingUploadSucceeded
                            ? "NO SCORES YET - SET THE FIRST SCORE!"
                            : "YOUR SCORE COULD NOT UPLOAD - CHECK PLAY GAMES SETUP");
                        return;
                    }

                    HashSet<string> uniqueUserIds = new HashSet<string>();
                    foreach (IScore score in scores)
                        if (!string.IsNullOrWhiteSpace(score.userID)) uniqueUserIds.Add(score.userID);

                    void PopulateEntries(IUserProfile[] profiles)
                    {
                        Dictionary<string, string> names = new Dictionary<string, string>();
                        if (profiles != null)
                        {
                            foreach (IUserProfile profile in profiles)
                            {
                                if (profile == null || string.IsNullOrWhiteSpace(profile.id)) continue;
                                names[profile.id] = profile.userName;
                            }
                        }

                        foreach (IScore score in scores)
                        {
                            bool isLocalPlayer = ReferenceEquals(score, localPlayerScore) ||
                                                 (!string.IsNullOrWhiteSpace(localPlayerId) &&
                                                  score.userID == localPlayerId);
                            names.TryGetValue(score.userID ?? string.Empty, out string playerName);
                            if (string.IsNullOrWhiteSpace(playerName) && isLocalPlayer)
                                playerName = PlayGamesSignInManager.AuthenticatedPlayerName;
                            if (string.IsNullOrWhiteSpace(playerName)) playerName = "PLAYER";
                            page.Entries.Add(new LeaderboardEntry
                            {
                                Rank = score.rank,
                                PlayerName = playerName,
                                FormattedScore = string.IsNullOrWhiteSpace(score.formattedValue)
                                    ? score.value.ToString()
                                    : score.formattedValue,
                                IsLocalPlayer = isLocalPlayer
                            });
                        }

                        GameAnalytics.LeaderboardAction("load", boardName, true);
                        completed?.Invoke(true, page, string.Empty);
                    }

                    if (uniqueUserIds.Count == 0)
                        PopulateEntries(Array.Empty<IUserProfile>());
                    else
                        PlayGamesPlatform.Instance.LoadUsers(new List<string>(uniqueUserIds).ToArray(), PopulateEntries);
                }

                if (localPlayerScore != null || pageToken != null || triedPlayerCentered)
                {
                    FinishPage();
                    return;
                }

                // Query Google's dedicated current-player endpoint first. Top/player-centred
                // pages can omit a newly submitted or private profile even though the write was
                // accepted by the server.
                triedPlayerCentered = true;
                PlayGamesPlatform.Instance.LoadCurrentPlayerScore(leaderboardId,
                    LeaderboardCollection.Public, span, directPlayerScore =>
                    {
                        localPlayerScore = directPlayerScore;
                        if (localPlayerScore != null)
                        {
                            FinishPage();
                            return;
                        }

                        // Retain the player-centred query for older Play Games service builds.
                        PlayGamesPlatform.Instance.LoadScores(leaderboardId,
                            LeaderboardStart.PlayerCentered, PageSize,
                            LeaderboardCollection.Public, span, centeredData =>
                            {
                                if (centeredData != null && centeredData.Valid)
                                    localPlayerScore = FindLocalPlayerScore(centeredData, localPlayerId);
                                FinishPage();
                            });
                    });
            };

            if (pageToken is ScorePageToken token)
                PlayGamesPlatform.Instance.LoadMoreScores(token, PageSize, handleScores);
            else
                PlayGamesPlatform.Instance.LoadScores(leaderboardId, LeaderboardStart.TopScores, PageSize,
                    LeaderboardCollection.Public, span, handleScores);
        }

        void LoadScores()
        {
            int requestedPage = pageToken is FirebasePageToken firebaseToken
                ? firebaseToken.PageNumber
                : 1;
            if (FirebaseLeaderboardManager.TryLoadScores(span, requestedPage, PageSize,
                    (success, firebasePage, message) =>
                    {
                        if (!success)
                        {
                            // Preserve leaderboard availability if Firebase is temporarily down
                            // or its rules have not been deployed yet.
                            LoadGooglePlayGamesScores();
                            return;
                        }

                        LeaderboardPage page = new LeaderboardPage
                        {
                            PreviousPageToken = firebasePage.HasPrevious
                                ? new FirebasePageToken(firebasePage.PageNumber - 1)
                                : null,
                            NextPageToken = firebasePage.HasNext
                                ? new FirebasePageToken(firebasePage.PageNumber + 1)
                                : null
                        };
                        foreach (FirebaseLeaderboardManager.Entry entry in firebasePage.Entries)
                        {
                            page.Entries.Add(new LeaderboardEntry
                            {
                                Rank = entry.Rank,
                                PlayerName = entry.PlayerName,
                                FormattedScore = entry.Score.ToString(),
                                IsLocalPlayer = entry.IsLocalPlayer
                            });
                        }

                        GameAnalytics.LeaderboardAction("load_firebase", boardName, true);
                        completed?.Invoke(true, page, message);
                    }))
                return;

            LoadGooglePlayGamesScores();
        }

        // A player commonly opens this screen immediately after game over. Finish uploading
        // the pending score first so the first page cannot race the score submission callback.
        if (pageToken == null)
            ReportPending(pendingScoreKey, leaderboardId, boardName, success =>
            {
                pendingUploadSucceeded = success;
                LoadScores();
            });
        else
            LoadScores();
#else
        GameAnalytics.LeaderboardAction("load_editor_preview", boardName, false);
        completed?.Invoke(false, new LeaderboardPage(),
            "LIVE SCORES ARE AVAILABLE IN THE ANDROID BUILD");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static IScore FindLocalPlayerScore(LeaderboardScoreData data, string localPlayerId)
    {
        if (data?.PlayerScore != null) return data.PlayerScore;
        if (string.IsNullOrWhiteSpace(localPlayerId) || data?.Scores == null) return null;
        foreach (IScore score in data.Scores)
            if (score != null && score.userID == localPlayerId) return score;
        return null;
    }

    private static bool ContainsScore(List<IScore> scores, IScore candidate, string localPlayerId)
    {
        foreach (IScore score in scores)
        {
            if (ReferenceEquals(score, candidate)) return true;
            if (!string.IsNullOrWhiteSpace(candidate.userID) && score.userID == candidate.userID) return true;
            if (!string.IsNullOrWhiteSpace(localPlayerId) && score.userID == localPlayerId) return true;
        }
        return false;
    }
#endif

    private static bool Show(string leaderboardId, string boardName, LeaderboardTimeSpan span)
    {
        if (string.IsNullOrWhiteSpace(leaderboardId))
        {
            GameAnalytics.LeaderboardAction("open_not_configured", boardName, false);
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!PlayGamesPlatform.Instance.IsAuthenticated())
        {
            GameAnalytics.LeaderboardAction("open_not_signed_in", boardName, false);
            return false;
        }

        PlayGamesPlatform.Instance.ShowLeaderboardUI(leaderboardId, span, status =>
            GameAnalytics.LeaderboardAction("open", boardName, status == UIStatus.Valid));
        return true;
#else
        GameAnalytics.LeaderboardAction("open_editor_preview", boardName, false);
        return false;
#endif
    }

    private static void SavePending(string key, int score)
    {
        if (score <= PlayerPrefs.GetInt(key, 0)) return;
        PlayerPrefs.SetInt(key, score);
        PlayerPrefs.Save();
    }

    private static void EnsureCurrentDailyPendingScore()
    {
        string today = DateTime.UtcNow.ToString("yyyyMMdd");
        if (PlayerPrefs.GetString(PendingDailyScoreDateKey, string.Empty) == today) return;
        PlayerPrefs.SetString(PendingDailyScoreDateKey, today);
        PlayerPrefs.DeleteKey(PendingDailyScoreKey);
        PlayerPrefs.Save();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void ReportPending(string key, string leaderboardId, string boardName,
        Action<bool> completed = null)
    {
        int score = PlayerPrefs.GetInt(key, 0);
        if (score <= 0 || string.IsNullOrWhiteSpace(leaderboardId))
        {
            completed?.Invoke(true);
            return;
        }

        Debug.Log($"Play Games leaderboard: submitting {score} to {boardName} ({leaderboardId}).");
        PlayGamesPlatform.Instance.ReportScore(score, leaderboardId, success =>
        {
            Debug.Log($"Play Games leaderboard: submission to {boardName} was " +
                      (success ? "confirmed by the Google Play Games server." : "REJECTED BY GOOGLE PLAY GAMES."));
            GameAnalytics.LeaderboardAction("submit", boardName, success);
            // Keep the best pending value so it can be safely retried after a temporary service
            // or Console-configuration problem. Google ignores duplicate/lower submissions.
            completed?.Invoke(success);
        });
    }
#endif
}
