using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

[DefaultExecutionOrder(-80)]
public sealed class PlayGamesSignInManager : MonoBehaviour
{
    private static PlayGamesSignInManager instance;
    private static string authenticatedPlayerName;
    private bool authenticationInProgress;
    private bool serverCodeRequestInProgress;
    private bool currentAttemptAutomatic;
    private float authenticationStartedAt;
    private float serverCodeStartedAt;
    public static string AuthenticatedPlayerName
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(authenticatedPlayerName) &&
                PlayGamesPlatform.Instance.IsAuthenticated())
            {
                authenticatedPlayerName = PlayGamesPlatform.Instance.GetUserDisplayName();

                // Some devices populate the Unity social profile shortly after the
                // authentication callback, so use it as a second source.
                if (string.IsNullOrWhiteSpace(authenticatedPlayerName) &&
                    Social.localUser != null && Social.localUser.authenticated)
                {
                    authenticatedPlayerName = Social.localUser.userName;
                }
            }
#endif
            return authenticatedPlayerName?.Trim() ?? string.Empty;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        EnsureInstance();
#endif
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Google Play Games Sign-In");
        instance = host.AddComponent<PlayGamesSignInManager>();
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
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayGamesPlatform.Activate();
        CloudSaveManager.MigrateLegacyLoginChoice();
        if (CloudSaveManager.ShouldAutoSignIn) BeginAutomaticSignIn();
#endif
    }

    private void BeginAutomaticSignIn()
    {
        if (authenticationInProgress) return;
        authenticationInProgress = true;
        currentAttemptAutomatic = true;
        authenticationStartedAt = Time.realtimeSinceStartup;
        GameHealthMonitor.StartTrace("play_games_auth");
        GameAnalytics.SignInStep("play_games_auth", "started", 0f, true);
        GameAnalytics.CloudSaveStatus("play_games_auth_started");
        PlayGamesPlatform.Instance.Authenticate(HandleAuthenticationResult);
    }

    public static void RetrySignIn()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        EnsureInstance();
        instance.BeginManualSignIn();
#endif
    }

    public static void EnableCloudSaveAndSignIn()
    {
        CloudSaveManager.EnableCloudSave();
        RetrySignIn();
    }

    private void BeginManualSignIn()
    {
        if (authenticationInProgress) return;
        authenticationInProgress = true;
        currentAttemptAutomatic = false;
        authenticationStartedAt = Time.realtimeSinceStartup;
        GameHealthMonitor.StartTrace("play_games_auth");
        GameAnalytics.SignInStep("play_games_auth", "started", 0f, false);
        GameAnalytics.CloudSaveStatus("play_games_manual_auth_started");
        PlayGamesPlatform.Instance.ManuallyAuthenticate(HandleAuthenticationResult);
    }

    private void HandleAuthenticationResult(SignInStatus status)
    {
        authenticationInProgress = false;
        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - authenticationStartedAt);
        bool authenticated = status == SignInStatus.Success;
        GameHealthMonitor.StopTrace("play_games_auth", authenticated);
        GameAnalytics.SignInStep("play_games_auth", status.ToString().ToLowerInvariant(), elapsed, currentAttemptAutomatic);
        if (status != SignInStatus.Success)
        {
            Debug.LogWarning($"Google Play Games sign-in did not complete: {status}");
            GameAnalytics.CloudSaveStatus($"play_games_auth_{status.ToString().ToLowerInvariant()}");
            return;
        }

        GameAnalytics.CloudSaveStatus("play_games_authenticated");
        authenticatedPlayerName = null;
        string playerName = AuthenticatedPlayerName;
        Debug.Log($"Google Play Games sign-in succeeded for " +
                  (string.IsNullOrWhiteSpace(playerName) ? "the current player." : $"'{playerName}'."));
        PlayGamesLeaderboardManager.SyncSavedBestScores();
        RequestFirebaseServerCode();
    }

    private void RequestFirebaseServerCode()
    {
        if (serverCodeRequestInProgress) return;
        serverCodeRequestInProgress = true;
        serverCodeStartedAt = Time.realtimeSinceStartup;
        GameHealthMonitor.StartTrace("play_games_server_code");
        GameAnalytics.SignInStep("server_code", "started", 0f, currentAttemptAutomatic);
        PlayGamesPlatform.Instance.RequestServerSideAccess(
            false,
            HandleServerAuthCode);
    }

    private void HandleServerAuthCode(string serverAuthCode)
    {
        // A server authorization code is single-use. Only block duplicate calls
        // while one request is active so a later sign-in (including a different
        // Play Games account after account deletion) can request a fresh code.
        serverCodeRequestInProgress = false;
        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - serverCodeStartedAt);

        if (string.IsNullOrWhiteSpace(serverAuthCode))
        {
            GameHealthMonitor.StopTrace("play_games_server_code", false);
            GameAnalytics.SignInStep("server_code", "empty", elapsed, currentAttemptAutomatic);
            Debug.LogWarning("Google Play Games returned no server authorization code.");
            GameAnalytics.CloudSaveStatus("play_games_server_code_failed");
            return;
        }

        GameHealthMonitor.StopTrace("play_games_server_code", true);
        GameAnalytics.SignInStep("server_code", "received", elapsed, currentAttemptAutomatic);
        GameAnalytics.CloudSaveStatus("play_games_server_code_received");
        CloudSaveManager.SignInWithPlayGamesAuthCode(serverAuthCode);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
