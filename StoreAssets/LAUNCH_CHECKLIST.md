# ZigZag Rush launch checklist

## Already prepared in the project

- Unity 6 project targeting Android API 36.
- Portrait-only orientation.
- IL2CPP with ARMv7 and ARM64.
- Package ID `com.zigzagstudio.zigzagrush`, version `1.1.0`, version code `5`.
- App Bundle release builder and readiness validator under the Unity `ZigZag` menu.
- LevelPlay mediation package and rewarded/interstitial integration.
- Rewarded placements are opt-in; interstitial cadence is remotely configurable; banners are hidden while driving and paused.
- 512 x 512 app icon and 1024 x 500 feature graphic.
- Store-listing, privacy-policy, Data safety, and asset-license drafts.
- Music is configured for mobile streaming rather than decompressing large tracks into memory.

## Account-owned blockers before the first production AAB

1. Confirm the permanent package ID `com.zigzagstudio.zigzagrush` before creating the Play Console app. It cannot be changed after publication.
2. Create a permanent Android upload keystore in Unity Player Settings, back it up securely, and record the alias/password outside the repository.
3. Create the app in Unity LevelPlay and replace all placeholders in `Assets/Ads/AdConfiguration.cs`.
4. Configure a strong mediation stack in LevelPlay. Start with bidding networks available for the target countries and add AdMob only after the Google account, app, and adapter are approved. Revenue depends heavily on audience country, retention, fill, and ad format; no network guarantees the highest payout everywhere.
5. Enable test mode and verify rewarded completion, ad-close-without-reward, no-fill, airplane mode, interstitial pacing, app background/foreground, and reinstall behavior on real devices.
6. Implement and enable a Google-certified consent management platform for regions where it is required, then set `CertifiedCmpConfigured` to `true` only after testing it. Complete the Play target-audience review, set `ChildDirected` correctly, and only then set `TargetAudienceReviewed` to `true`.
7. Complete and host `PRIVACY_POLICY_TEMPLATE.md` on a public HTTPS URL; add that URL to the game configuration and Play Console.
8. Resolve every item in `THIRD_PARTY_ASSETS.md`, especially music and sound licensing.
9. Capture real phone screenshots using the plan in `PLAY_STORE_LISTING.md`.
10. Run `ZigZag > Validate Play Store Readiness` and resolve every TODO.
11. Run `ZigZag > Build Release AAB`; upload first to Internal testing, not Production.

## Play Console and testing

- Complete App access, Ads, Content rating, Target audience, News apps, Data safety, and Advertising ID declarations.
- Supply support email, category/tags, privacy-policy URL, icon, feature graphic, and phone screenshots.
- Add testers required for the developer account type and satisfy any closed-testing requirement shown by Play Console.
- Use Play App Signing and securely retain the upload key.
- Review the pre-launch report for crashes, ANRs, layout problems, accessibility, and device compatibility.
- Test Android back behavior, notches, small/large screens, 60 Hz and high-refresh devices, low-memory devices, and Android 8 through current Android releases.
- Verify the delivered bundle contains ARM64 and supports 16 KB memory pages.
- Promote Internal to Closed/Open/Production only after crash-free physical-device and ad-policy testing.

## Release build command

Use the Unity Editor menu `ZigZag > Build Release AAB`. It intentionally refuses to build when the keystore or production privacy/ad configuration is unfinished.
