# ZigZag Rush online services setup

The code-side integration is included in version `1.1.0` (`versionCode 5`). Complete the items below before building the release AAB.

## Google Play Games leaderboards

In Play Console, open **Grow users > Play Games Services > Setup and management > Leaderboards**.

1. Create a leaderboard named `ZigZag Rush High Score` using **larger is better**, integer formatting, and a sensible anti-tamper score limit.
2. Create a second leaderboard named `Daily Score Rush` using the same score settings. This receives scores from the seeded daily mode.
3. Copy both Android leaderboard resource IDs into:
   - `Assets/Resources/Config/Sections/ServiceConfig.asset`
   - `mainLeaderboardId`
   - `dailyLeaderboardId`
4. Publish the Play Games Services configuration and ensure the closed-test accounts are PGS testers until the configuration is public.

The main leaderboard exposes Play Games' daily, weekly, and all-time timeframes. The game provides a timeframe picker from the main menu and after game over. Scores that fail to upload are retained locally and retried after the next successful sign-in.

## Firebase Crashlytics and Performance

The Android dependencies and runtime bridge are included. Unity errors/exceptions are recorded as Crashlytics non-fatals. Custom Performance traces cover startup and the Play Games/Firebase sign-in flow; long frame stalls are also logged.

1. In Firebase Console, open **Crashlytics** and **Performance**, then enable/get started with both products.
2. Install the closed-test build and exercise launch, Play Games sign-in, one run, game over, and retry.
3. Allow time for the first reports to appear, then verify `app_start_to_game_ready`, sign-in traces, non-fatals, and Analytics `sign_in_step` events.
4. Preserve the Unity IL2CPP symbols generated for every release. Upload native symbols when Firebase requests them so native crashes are symbolicated.

## Firebase Remote Config

Create one Remote Config parameter:

- Key: `game_config_json`
- Type: String
- Default value: the complete contents of `Assets/Resources/Config/game_config.json`

Publish the template. For safety, edit the complete JSON rather than a fragment and validate it before publishing. Useful live-test fields include:

- `gameplay.difficultyScoreCap`
- `gameplay.specialTileChanceMin` / `specialTileChanceMax`
- `gameplay.routeBranchesEnabled`
- `gameplay.routeBranchChanceMin` / `routeBranchChanceMax`
- `gameplay.riskRouteGemChance`
- `economy.dailyGiftGems` / `rewardedStoreGems`
- challenge targets and rewards under `challenges`
- `ads.interstitialEveryRuns` (`0` disables run-count interstitials)

Use Firebase conditions and a small test audience before broad rollout. The client fetches and activates this value on Android and keeps the packaged configuration when fetch or parsing fails.

## Release smoke test

Before sending `versionCode 5` to closed testing:

- sign in with both an existing tester and a newly added tester;
- finish a normal run and confirm all three leaderboard timeframes open;
- finish two daily runs and confirm the personal-best ghost appears on the second run;
- encounter a branch and verify both routes merge;
- verify mastery XP, trails/paint, and cloud restore;
- verify a personalized game-over objective appears;
- verify banners remain hidden during driving and pause;
- verify Remote Config, Crashlytics, Performance, and sign-in Analytics receive data.
