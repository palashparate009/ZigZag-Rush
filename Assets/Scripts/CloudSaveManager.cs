using System;
using System.Collections;
using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public sealed class CloudSaveManager : MonoBehaviour
{
    private const string CloudSaveDisabledKey = "CloudSaveDisabled";
    private const string LoginModeKey = "PlayerLoginMode";
    private const string LoginScreenTestKey = "ShowLoginScreenForTest";
    private const string PlayerNameKey = "PlayerName";
    private const string PlayerNameOwnerKey = "PlayerNameFirebaseUserId";
    private const string PlayGamesMode = "play_games";
    private const string GuestMode = "guest";
    private static CloudSaveManager instance;
    public static event Action ProgressSynchronized;
    public static bool IsInitialSyncComplete { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore firestore;
    private Coroutine saveRoutine;
    private bool firebaseReady;
    private bool syncInProgress;
    private bool writeInProgress;
    private bool writeQueued;
    private bool applyingCloudData;
    private bool signInInProgress;
    private bool deletingAccount;
    private string pendingPlayGamesAuthCode;
    private float firebaseSignInStartedAt;

    public static bool IsSignedIn => instance != null && instance.auth?.CurrentUser != null;
    public static bool IsCloudSaveDisabled => PlayerPrefs.GetInt(CloudSaveDisabledKey, 0) == 1;
    public static bool HasLoginChoice => PlayerPrefs.HasKey(LoginModeKey);
    public static bool ShouldShowLoginScreen => !HasLoginChoice || PlayerPrefs.GetInt(LoginScreenTestKey, 0) == 1;
    public static bool IsGuest => PlayerPrefs.GetString(LoginModeKey, string.Empty) == GuestMode;
    public static bool ShouldAutoSignIn => PlayerPrefs.GetString(LoginModeKey, string.Empty) == PlayGamesMode && !IsCloudSaveDisabled;
    public static string PlayerName => PlayerPrefs.GetString(PlayerNameKey, string.Empty).Trim();
    public static bool HasPlayerName => !string.IsNullOrWhiteSpace(PlayerName);
    public static string SuggestedPlayerName => IsSignedIn && !string.IsNullOrWhiteSpace(instance.auth.CurrentUser.DisplayName)
        ? instance.auth.CurrentUser.DisplayName.Trim()
        : string.Empty;
    public static string PlayerDisplayName => IsSignedIn
        ? SafeDisplayName(instance.auth.CurrentUser.DisplayName)
        : string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Firebase Cloud Save");
        instance = host.AddComponent<CloudSaveManager>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted || task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning($"Cloud save Firebase initialization failed: {task.Exception?.GetBaseException().Message ?? task.Result.ToString()}");
                GameAnalytics.CloudSaveStatus("firebase_unavailable");
                return;
            }

            auth = FirebaseAuth.DefaultInstance;
            firestore = FirebaseFirestore.DefaultInstance;
            firebaseReady = true;

            if (IsCloudSaveDisabled)
            {
                if (auth.CurrentUser != null) auth.SignOut();
                pendingPlayGamesAuthCode = null;
                return;
            }

            if (!string.IsNullOrEmpty(pendingPlayGamesAuthCode))
            {
                string authCode = pendingPlayGamesAuthCode;
                pendingPlayGamesAuthCode = null;
                SignIn(authCode);
                return;
            }

            if (auth.CurrentUser != null)
            {
                GameAnalytics.CloudSaveStatus("session_restored");
                PreparePlayerNameForCurrentAccount();
                SynchronizeProgress();
            }
        });
    }

    public static void SignInWithPlayGamesAuthCode(string serverAuthCode)
    {
        EnsureInstance();
        instance.SignIn(serverAuthCode);
    }

    private void SignIn(string serverAuthCode)
    {
        if (IsCloudSaveDisabled || deletingAccount) return;
        if (string.IsNullOrWhiteSpace(serverAuthCode))
        {
            GameAnalytics.CloudSaveStatus("missing_auth_code");
            return;
        }

        if (!firebaseReady || auth == null)
        {
            IsInitialSyncComplete = false;
            pendingPlayGamesAuthCode = serverAuthCode;
            GameAnalytics.CloudSaveStatus("sign_in_queued");
            return;
        }

        if (signInInProgress) return;
        IsInitialSyncComplete = false;
        signInInProgress = true;
        firebaseSignInStartedAt = Time.realtimeSinceStartup;
        GameHealthMonitor.StartTrace("firebase_credential_sign_in");
        GameAnalytics.SignInStep("firebase_credential", "started", 0f, false);

        Credential credential = PlayGamesAuthProvider.GetCredential(serverAuthCode);
        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            signInInProgress = false;
            float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - firebaseSignInStartedAt);
            if (task.IsCanceled || task.IsFaulted)
            {
                GameHealthMonitor.StopTrace("firebase_credential_sign_in", false);
                GameAnalytics.SignInStep("firebase_credential", task.IsCanceled ? "cancelled" : "failed", elapsed, false);
                Debug.LogWarning($"Firebase Play Games sign-in failed: {task.Exception?.GetBaseException().Message}");
                GameAnalytics.CloudSaveStatus("sign_in_failed");
                return;
            }

            GameHealthMonitor.StopTrace("firebase_credential_sign_in", true);
            GameAnalytics.SignInStep("firebase_credential", "success", elapsed, false);
            GameAnalytics.CloudSaveStatus("signed_in");
            PreparePlayerNameForCurrentAccount();
            SynchronizeProgress();
        });
    }

    public static void NotifyLocalProgressChanged()
    {
        if (instance == null || instance.applyingCloudData || instance.deletingAccount || IsCloudSaveDisabled || !IsSignedIn) return;
        instance.ScheduleSave();
    }

    public static void EnableCloudSave()
    {
        PlayerPrefs.DeleteKey(CloudSaveDisabledKey);
        PlayerPrefs.Save();
    }

    public static void MigrateLegacyLoginChoice()
    {
        if (HasLoginChoice) return;
        bool hasExistingProgress = PlayerPrefs.GetInt("TutorialSeen", 0) == 1 ||
                                   PlayerPrefs.GetInt("HighScore", 0) > 0 ||
                                   PlayerPrefs.GetInt("TotalGems", 0) > 0;
        if (!hasExistingProgress) return;
        PlayerPrefs.SetString(LoginModeKey, IsCloudSaveDisabled ? GuestMode : PlayGamesMode);
        PlayerPrefs.Save();
    }

    public static void SelectGuestMode()
    {
        EnsureInstance();
        PlayerPrefs.SetString(LoginModeKey, GuestMode);
        PlayerPrefs.DeleteKey(LoginScreenTestKey);
        PlayerPrefs.SetInt(CloudSaveDisabledKey, 1);
        PlayerPrefs.Save();
        instance.pendingPlayGamesAuthCode = null;
        if (instance.firebaseReady && instance.auth?.CurrentUser != null) instance.auth.SignOut();
        GameAnalytics.CloudSaveStatus("guest_mode_selected");
    }

    public static void SelectPlayGamesMode()
    {
        PlayerPrefs.SetString(LoginModeKey, PlayGamesMode);
        PlayerPrefs.DeleteKey(LoginScreenTestKey);
        PlayerPrefs.DeleteKey(CloudSaveDisabledKey);
        PlayerPrefs.Save();
        GameAnalytics.CloudSaveStatus("play_games_mode_selected");
    }

    public static void SetPlayerName(string playerName)
    {
        string cleanName = playerName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(cleanName)) return;
        PlayerPrefs.SetString(PlayerNameKey, cleanName);
        if (IsSignedIn)
            PlayerPrefs.SetString(PlayerNameOwnerKey, instance.auth.CurrentUser.UserId);
        PlayerPrefs.Save();
        NotifyLocalProgressChanged();

        if (!IsSignedIn || string.Equals(instance.auth.CurrentUser.DisplayName, cleanName, StringComparison.Ordinal)) return;
        UserProfile profile = new UserProfile { DisplayName = cleanName };
        instance.auth.CurrentUser.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
                Debug.LogWarning($"Player display name update failed: {task.Exception?.GetBaseException().Message}");
        });
    }

    public static void DeleteAccount(Action<bool, string> completed)
    {
        EnsureInstance();
        instance.DeleteCurrentAccount(completed);
    }

    private void DeleteCurrentAccount(Action<bool, string> completed)
    {
        if (deletingAccount) return;
        if (!firebaseReady || auth == null || firestore == null)
        {
            completed?.Invoke(false, "CLOUD SERVICE IS NOT READY");
            return;
        }

        FirebaseUser user = auth.CurrentUser;
        if (user == null)
        {
            ClearLocalAccountData();
            completed?.Invoke(true, "LOCAL ACCOUNT DATA DELETED");
            return;
        }

        deletingAccount = true;
        if (saveRoutine != null)
        {
            StopCoroutine(saveRoutine);
            saveRoutine = null;
        }

        string userId = user.UserId;
        firestore.Collection(ConfigManager.Current.cloud.playersCollection).Document(userId).DeleteAsync().ContinueWithOnMainThread(documentTask =>
        {
            if (documentTask.IsCanceled || documentTask.IsFaulted)
            {
                deletingAccount = false;
                Debug.LogWarning($"Cloud account data deletion failed: {documentTask.Exception?.GetBaseException().Message}");
                completed?.Invoke(false, "COULD NOT DELETE CLOUD DATA");
                return;
            }

            user.DeleteAsync().ContinueWithOnMainThread(userTask =>
            {
                deletingAccount = false;
                if (userTask.IsCanceled || userTask.IsFaulted)
                {
                    Debug.LogWarning($"Firebase account deletion failed: {userTask.Exception?.GetBaseException().Message}");
                    completed?.Invoke(false, "SIGN IN AGAIN, THEN RETRY DELETE");
                    return;
                }

                auth.SignOut();
                ClearLocalAccountData();
                GameAnalytics.CloudSaveStatus("account_deleted");
                completed?.Invoke(true, "ACCOUNT DELETED");
            });
        });
    }

    private static void ClearLocalAccountData()
    {
        int analyticsConsent = PlayerPrefs.GetInt("AnalyticsConsent", -1);
        int adConsent = PlayerPrefs.GetInt("AdPersonalizationConsent", -1);
        PlayerPrefs.DeleteAll();
        if (analyticsConsent >= 0) PlayerPrefs.SetInt("AnalyticsConsent", analyticsConsent);
        if (adConsent >= 0) PlayerPrefs.SetInt("AdPersonalizationConsent", adConsent);
        PlayerPrefs.SetInt(CloudSaveDisabledKey, 1);
        PlayerPrefs.Save();
    }

    private void ScheduleSave()
    {
        if (saveRoutine != null) StopCoroutine(saveRoutine);
        saveRoutine = StartCoroutine(SaveAfterDelay());
    }

    private IEnumerator SaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, ConfigManager.Current.cloud.saveDelaySeconds));
        saveRoutine = null;
        UploadProgress();
    }

    private void SynchronizeProgress()
    {
        if (!firebaseReady || firestore == null || auth?.CurrentUser == null || syncInProgress) return;
        IsInitialSyncComplete = false;
        syncInProgress = true;
        string syncUserId = auth.CurrentUser.UserId;
        DocumentReference playerDocument = firestore
            .Collection(ConfigManager.Current.cloud.playersCollection)
            .Document(syncUserId);
        playerDocument.GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(task =>
        {
            syncInProgress = false;

            // A Play Games account can change while the previous Firebase profile
            // is still downloading. Never merge that stale profile into the newly
            // authenticated user; immediately restart for the current account.
            if (auth?.CurrentUser == null ||
                !string.Equals(auth.CurrentUser.UserId, syncUserId, StringComparison.Ordinal))
            {
                GameAnalytics.CloudSaveStatus("stale_download_ignored");
                if (auth?.CurrentUser != null) SynchronizeProgress();
                return;
            }

            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogWarning($"Cloud save download failed: {task.Exception?.GetBaseException().Message}");
                GameAnalytics.CloudSaveStatus("download_failed");
                CompleteInitialSync();
                return;
            }

            DocumentSnapshot snapshot = task.Result;
            if (snapshot.Exists)
            {
                MergeCloudProgress(snapshot.ToDictionary());
                GameAnalytics.CloudSaveStatus("progress_merged");
            }
            else
            {
                GameAnalytics.CloudSaveStatus("new_cloud_save");
            }

            UploadProgress();
            CompleteInitialSync();
        });
    }

    private static void CompleteInitialSync()
    {
        IsInitialSyncComplete = true;
        // Firebase authentication and cloud merge are now complete, so this score
        // is written to the correct account rather than a previous restored session.
        FirebaseLeaderboardManager.SyncPermanentBest();
        ProgressSynchronized?.Invoke();
    }

    private void MergeCloudProgress(Dictionary<string, object> cloud)
    {
        applyingCloudData = true;
        try
        {
            _ = GameProgress.DailyBest;
            _ = GameProgress.WeeklyChallengeSummary;
            SetMax("HighScore", ReadInt(cloud, "best_score"));
            SetMax("TotalGems", ReadInt(cloud, "total_gems"));
            SetMax("LifetimeRunsStarted", ReadInt(cloud, "lifetime_runs_started"));
            SetBitwiseUnion("UnlockedSkins", ReadInt(cloud, "unlocked_skins", 1));
            SetBitwiseUnion("UnlockedThemes", ReadInt(cloud, "unlocked_themes", 1));
            SetBitwiseUnion("FtueSeenMask", ReadInt(cloud, "ftue_seen_mask"), 0);
            GameProgress.MergeUnlockedCarsFromCloud(ReadStringList(cloud, "unlocked_car_ids"));
            GameProgress.MergeCarMasteryFromCloud(ReadStringList(cloud, "car_mastery"));

            SetTrueIfCloudTrue("TutorialSeen", ReadBool(cloud, "tutorial_seen"));
            SetTrueIfCloudTrue("AchievementScore50", ReadBool(cloud, "achievement_score_50"));
            SetTrueIfCloudTrue("AchievementCombo10", ReadBool(cloud, "achievement_combo_10"));
            SetTrueIfCloudTrue("AchievementScore200", ReadBool(cloud, "achievement_score_200"));

            string cloudPlayerName = ReadString(cloud, "player_name").Trim();
            string authenticatedAccountName = PlayGamesSignInManager.AuthenticatedPlayerName;
            string resolvedPlayerName = !string.IsNullOrWhiteSpace(authenticatedAccountName)
                ? authenticatedAccountName.Trim()
                : cloudPlayerName;
            if (string.IsNullOrWhiteSpace(resolvedPlayerName))
                resolvedPlayerName = auth.CurrentUser.DisplayName?.Trim() ?? string.Empty;

            // The authenticated Play Games identity is authoritative. This also
            // repairs cloud documents that an older build populated with the
            // previously signed-in player's cached name.
            if (!string.IsNullOrEmpty(resolvedPlayerName))
                PlayerPrefs.SetString(PlayerNameKey, resolvedPlayerName);
            PlayerPrefs.SetString(PlayerNameOwnerKey, auth.CurrentUser.UserId);

            string localDailyDate = PlayerPrefs.GetString("DailyDate", string.Empty);
            string cloudDailyDate = ReadString(cloud, "daily_date");
            if (string.Equals(localDailyDate, cloudDailyDate, StringComparison.Ordinal))
            {
                SetMax("DailyBest", ReadInt(cloud, "daily_best"));
                SetTrueIfCloudTrue("DailyComplete", ReadBool(cloud, "daily_complete"));
                SetMax("MissionScore", ReadInt(cloud, "mission_score"));
                SetMax("MissionCombo", ReadInt(cloud, "mission_combo"));
                SetMax("MissionGems", ReadInt(cloud, "mission_gems"));
                SetMax("MissionRuns", ReadInt(cloud, "mission_runs"));
                SetMax("MissionTotalScore", ReadInt(cloud, "mission_total_score"));
                SetMax("DailyRuns", ReadInt(cloud, "daily_runs"));
                SetMax("DailyGems", ReadInt(cloud, "daily_gems"));
                SetMax("DailyCombo", ReadInt(cloud, "daily_combo"));
                SetMax("DailyScore", ReadInt(cloud, "daily_score"));
                SetTrueIfCloudTrue("DailyRunsRewarded", ReadBool(cloud, "daily_runs_rewarded"));
                SetTrueIfCloudTrue("DailyGemsRewarded", ReadBool(cloud, "daily_gems_rewarded"));
                SetTrueIfCloudTrue("DailyComboRewarded", ReadBool(cloud, "daily_combo_rewarded"));
                SetTrueIfCloudTrue("DailyScoreRewarded", ReadBool(cloud, "daily_score_rewarded"));
                SetTrueIfCloudTrue("MissionScoreRewarded", ReadBool(cloud, "mission_score_rewarded"));
                SetTrueIfCloudTrue("MissionComboRewarded", ReadBool(cloud, "mission_combo_rewarded"));
                SetTrueIfCloudTrue("MissionGemsRewarded", ReadBool(cloud, "mission_gems_rewarded"));
                SetTrueIfCloudTrue("MissionRunsRewarded", ReadBool(cloud, "mission_runs_rewarded"));
                SetTrueIfCloudTrue("MissionTotalScoreRewarded", ReadBool(cloud, "mission_total_score_rewarded"));
            }

            string localWeeklyDate = PlayerPrefs.GetString("WeeklyDate", string.Empty);
            string cloudWeeklyDate = ReadString(cloud, "weekly_date");
            if (string.Equals(localWeeklyDate, cloudWeeklyDate, StringComparison.Ordinal))
            {
                SetMax("WeeklyRuns", ReadInt(cloud, "weekly_runs"));
                SetMax("WeeklyScore", ReadInt(cloud, "weekly_score"));
                SetMax("WeeklyGems", ReadInt(cloud, "weekly_gems"));
                SetMax("WeeklyCombo", ReadInt(cloud, "weekly_combo"));
                SetMax("WeeklyBest", ReadInt(cloud, "weekly_best"));
                SetTrueIfCloudTrue("WeeklyRunsRewarded", ReadBool(cloud, "weekly_runs_rewarded"));
                SetTrueIfCloudTrue("WeeklyScoreRewarded", ReadBool(cloud, "weekly_score_rewarded"));
                SetTrueIfCloudTrue("WeeklyGemsRewarded", ReadBool(cloud, "weekly_gems_rewarded"));
                SetTrueIfCloudTrue("WeeklyComboRewarded", ReadBool(cloud, "weekly_combo_rewarded"));
                SetTrueIfCloudTrue("WeeklyBestRewarded", ReadBool(cloud, "weekly_best_rewarded"));
            }

            string cloudGiftDate = ReadString(cloud, "store_daily_gift_date");
            string localGiftDate = PlayerPrefs.GetString("StoreDailyGiftDate", string.Empty);
            if (string.CompareOrdinal(cloudGiftDate, localGiftDate) > 0)
                PlayerPrefs.SetString("StoreDailyGiftDate", cloudGiftDate);

            int selectedSkin = Mathf.Clamp(ReadInt(cloud, "selected_skin", GameProgress.SelectedSkin),
                0, GameProgress.SkinNames.Length - 1);
            int selectedTheme = Mathf.Clamp(ReadInt(cloud, "selected_theme", GameProgress.SelectedTheme),
                0, GameProgress.ThemeNames.Length - 1);
            if (GameProgress.IsSkinUnlocked(selectedSkin)) PlayerPrefs.SetInt("SelectedSkin", selectedSkin);
            if (GameProgress.IsThemeUnlocked(selectedTheme)) PlayerPrefs.SetInt("SelectedTheme", selectedTheme);
            GameProgress.SelectCloudCarIfUnlocked(ReadString(cloud, "selected_car_id"));
            PlayerPrefs.Save();
            PlayGamesLeaderboardManager.SyncSavedBestScores();
        }
        finally
        {
            applyingCloudData = false;
        }
    }

    private void UploadProgress()
    {
        if (!firebaseReady || firestore == null || auth?.CurrentUser == null || deletingAccount || IsCloudSaveDisabled) return;
        if (writeInProgress)
        {
            writeQueued = true;
            return;
        }

        writeInProgress = true;
        PlayerDocument().SetAsync(CaptureProgress(), SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            writeInProgress = false;
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogWarning($"Cloud save upload failed: {task.Exception?.GetBaseException().Message}");
                GameAnalytics.CloudSaveStatus("upload_failed");
            }
            else
            {
                GameAnalytics.CloudSaveStatus("upload_complete");
            }

            if (!writeQueued) return;
            writeQueued = false;
            UploadProgress();
        });
    }

    private void PreparePlayerNameForCurrentAccount()
    {
        if (auth?.CurrentUser == null) return;

        string currentUserId = auth.CurrentUser.UserId;
        string nameOwnerId = PlayerPrefs.GetString(PlayerNameOwnerKey, string.Empty);
        if (string.Equals(nameOwnerId, currentUserId, StringComparison.Ordinal)) return;

        // The locally cached name belonged to another Firebase/Play Games user.
        // Seed the new account with its Play Games identity; its own cloud name,
        // when present, will replace this during MergeCloudProgress.
        string accountName = PlayGamesSignInManager.AuthenticatedPlayerName;
        if (string.IsNullOrWhiteSpace(accountName))
            accountName = auth.CurrentUser.DisplayName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(accountName))
            PlayerPrefs.DeleteKey(PlayerNameKey);
        else
            PlayerPrefs.SetString(PlayerNameKey, accountName.Trim());

        PlayerPrefs.SetString(PlayerNameOwnerKey, currentUserId);
        PlayerPrefs.Save();
    }

    private Dictionary<string, object> CaptureProgress()
    {
        _ = GameProgress.DailyBest;
        _ = GameProgress.WeeklyChallengeSummary;
        return new Dictionary<string, object>
        {
            { "schema_version", ConfigManager.Current.cloud.schemaVersion },
            { "player_name", PlayerName },
            { "best_score", GameProgress.BestScore },
            { "total_gems", GameProgress.TotalGems },
            { "lifetime_runs_started", GameProgress.LifetimeRunsStarted },
            { "selected_skin", GameProgress.SelectedSkin },
            { "selected_car_id", GameProgress.SelectedCarId },
            { "selected_theme", GameProgress.SelectedTheme },
            { "unlocked_skins", PlayerPrefs.GetInt("UnlockedSkins", 1) },
            { "unlocked_car_ids", new List<string>(GameProgress.GetUnlockedCarIdsForCloud()) },
            { "car_mastery", GameProgress.GetCarMasteryForCloud() },
            { "unlocked_themes", PlayerPrefs.GetInt("UnlockedThemes", 1) },
            { "ftue_seen_mask", PlayerPrefs.GetInt("FtueSeenMask", 0) },
            { "tutorial_seen", PlayerPrefs.GetInt("TutorialSeen", 0) == 1 },
            { "achievement_score_50", PlayerPrefs.GetInt("AchievementScore50", 0) == 1 },
            { "achievement_combo_10", PlayerPrefs.GetInt("AchievementCombo10", 0) == 1 },
            { "achievement_score_200", PlayerPrefs.GetInt("AchievementScore200", 0) == 1 },
            { "daily_date", PlayerPrefs.GetString("DailyDate", string.Empty) },
            { "daily_best", PlayerPrefs.GetInt("DailyBest", 0) },
            { "daily_complete", PlayerPrefs.GetInt("DailyComplete", 0) == 1 },
            { "daily_runs", PlayerPrefs.GetInt("DailyRuns", 0) },
            { "daily_gems", PlayerPrefs.GetInt("DailyGems", 0) },
            { "daily_combo", PlayerPrefs.GetInt("DailyCombo", 0) },
            { "daily_score", PlayerPrefs.GetInt("DailyScore", 0) },
            { "daily_runs_rewarded", PlayerPrefs.GetInt("DailyRunsRewarded", 0) == 1 },
            { "daily_gems_rewarded", PlayerPrefs.GetInt("DailyGemsRewarded", 0) == 1 },
            { "daily_combo_rewarded", PlayerPrefs.GetInt("DailyComboRewarded", 0) == 1 },
            { "daily_score_rewarded", PlayerPrefs.GetInt("DailyScoreRewarded", 0) == 1 },
            { "mission_score", PlayerPrefs.GetInt("MissionScore", 0) },
            { "mission_combo", PlayerPrefs.GetInt("MissionCombo", 0) },
            { "mission_gems", PlayerPrefs.GetInt("MissionGems", 0) },
            { "mission_runs", PlayerPrefs.GetInt("MissionRuns", 0) },
            { "mission_total_score", PlayerPrefs.GetInt("MissionTotalScore", 0) },
            { "mission_score_rewarded", PlayerPrefs.GetInt("MissionScoreRewarded", 0) == 1 },
            { "mission_combo_rewarded", PlayerPrefs.GetInt("MissionComboRewarded", 0) == 1 },
            { "mission_gems_rewarded", PlayerPrefs.GetInt("MissionGemsRewarded", 0) == 1 },
            { "mission_runs_rewarded", PlayerPrefs.GetInt("MissionRunsRewarded", 0) == 1 },
            { "mission_total_score_rewarded", PlayerPrefs.GetInt("MissionTotalScoreRewarded", 0) == 1 },
            { "weekly_date", PlayerPrefs.GetString("WeeklyDate", string.Empty) },
            { "weekly_runs", PlayerPrefs.GetInt("WeeklyRuns", 0) },
            { "weekly_score", PlayerPrefs.GetInt("WeeklyScore", 0) },
            { "weekly_gems", PlayerPrefs.GetInt("WeeklyGems", 0) },
            { "weekly_combo", PlayerPrefs.GetInt("WeeklyCombo", 0) },
            { "weekly_best", PlayerPrefs.GetInt("WeeklyBest", 0) },
            { "weekly_runs_rewarded", PlayerPrefs.GetInt("WeeklyRunsRewarded", 0) == 1 },
            { "weekly_score_rewarded", PlayerPrefs.GetInt("WeeklyScoreRewarded", 0) == 1 },
            { "weekly_gems_rewarded", PlayerPrefs.GetInt("WeeklyGemsRewarded", 0) == 1 },
            { "weekly_combo_rewarded", PlayerPrefs.GetInt("WeeklyComboRewarded", 0) == 1 },
            { "weekly_best_rewarded", PlayerPrefs.GetInt("WeeklyBestRewarded", 0) == 1 },
            { "store_daily_gift_date", PlayerPrefs.GetString("StoreDailyGiftDate", string.Empty) },
            { "app_version", Application.version },
            { "updated_at", FieldValue.ServerTimestamp }
        };
    }

    private DocumentReference PlayerDocument()
    {
        return firestore.Collection(ConfigManager.Current.cloud.playersCollection).Document(auth.CurrentUser.UserId);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && IsSignedIn) UploadProgress();
    }

    private void OnApplicationQuit()
    {
        if (IsSignedIn) UploadProgress();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private static void SetMax(string key, int cloudValue)
    {
        PlayerPrefs.SetInt(key, Mathf.Max(PlayerPrefs.GetInt(key, 0), cloudValue));
    }

    private static void SetBitwiseUnion(string key, int cloudValue, int localFallback = 1)
    {
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, localFallback) | cloudValue);
    }

    private static void SetTrueIfCloudTrue(string key, bool cloudValue)
    {
        if (cloudValue) PlayerPrefs.SetInt(key, 1);
    }

    private static int ReadInt(Dictionary<string, object> data, string key, int fallback = 0)
    {
        if (!data.TryGetValue(key, out object value) || value == null) return fallback;
        try { return Convert.ToInt32(value); }
        catch { return fallback; }
    }

    private static bool ReadBool(Dictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out object value) && value is bool boolean && boolean;
    }

    private static string ReadString(Dictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out object value) ? value as string ?? string.Empty : string.Empty;
    }

    private static IEnumerable<string> ReadStringList(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out object value) || value == null) yield break;
        if (value is IEnumerable<object> objects)
        {
            foreach (object item in objects)
                if (item is string text) yield return text;
            yield break;
        }
        if (value is IEnumerable<string> strings)
            foreach (string text in strings) yield return text;
    }

    private static string SafeDisplayName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
    }
}
