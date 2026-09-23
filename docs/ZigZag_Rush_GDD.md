# ZigZag Rush — Game Design Document

**Document status:** Living design document  
**Last updated:** 17 September 2026  
**Current project version:** 1.1.0 (Android version code 5)  
**Platform:** Android, portrait  
**Genre:** One-touch endless arcade / score-chasing driving game  
**Package:** `com.zigzagstudio.zigzagrush`

> This document distinguishes shipped systems from future ideas. Sections marked **Implemented** describe the current project. Sections marked **Proposed** are recommendations and are not yet in the game.

## 1. High concept

ZigZag Rush is a fast, one-touch driving game in which the player steers an automatically moving car across an endlessly generated zigzag road. Every tap changes direction. The player survives increasingly fast turns, aims for precisely timed PERFECT turns, builds a score multiplier, collects gems, reacts to special road blocks, completes challenges, and unlocks cars and visual themes.

### Player promise

“Easy to understand in one run, difficult to master over many runs.”

The game should deliver:

- immediate one-touch control;
- short runs with a strong “one more try” feeling;
- visible skill growth through PERFECT timing and combos;
- readable surprises through colored and icon-marked special blocks;
- long-term collection through cars, themes, achievements, and challenges;
- fair monetization that never interrupts active driving.

### Design pillars

1. **One-touch clarity** — the player should always understand what a tap does.
2. **Precision creates mastery** — good timing produces PERFECT turns, combos, and higher scores.
3. **Readable variety** — special blocks vary a run without becoming unpredictable traps.
4. **Fast recovery** — game over, revive, and retry should return the player to action quickly.
5. **Fair progression** — gems, ads, purchases, and car stats must support play rather than replace skill.

## 2. Target audience and session

**Implemented product fit**

- Players who enjoy simple arcade controls, high-score competition, collectible cars, and short mobile sessions.
- Suitable for quick sessions, but daily/weekly challenges provide reasons for longer repeat play.
- Portrait orientation supports comfortable one-handed play.

**Intended session shape**

- Learn the control in under one minute.
- Play several short runs in one session.
- Leave with progress toward a challenge, car, theme, or personal best.
- Return for a daily gift, daily seeded run, or challenge reset.

## 3. Core game loop — Implemented

1. Enter as a Google Play Games player or continue as a guest.
2. Choose a car and theme, or play immediately.
3. Start an endless or daily seeded run.
4. Tap to alternate between the world-forward and world-right directions.
5. Survive the generated road, time PERFECT turns, collect gems, and use special blocks.
6. Fall from the road, unless a shield or rewarded revive saves the run.
7. Review score, best score, collected gems, and best combo.
8. Optionally double the run’s gems with a rewarded ad.
9. Claim completed challenges or achievements and spend gems on cars/themes.
10. Retry from the original start or return to the menu.

## 4. Controls and rules — Implemented

### Controls

- The car moves automatically.
- A tap/click switches the travel direction between forward and right.
- Taps over UI are ignored by gameplay input.
- The pause button freezes gameplay; Android back navigation is handled by the UI.
- The device is kept awake while the game is running.

### Failure condition

- Falling below the road ends the run.
- An active shield prevents one fall and is consumed.
- A rewarded revive rebuilds a five-tile safe runway near the last safe platform, places the car with a `0.8` safety inset, and resumes after a countdown.
- Retry performs a clean in-scene reset: score, gems, combo, car, camera, road pool, and initial runway return to the beginning without reloading the scene.

### Movement and difficulty

- Base car movement speed: `8` units/second.
- Run speed scales from `1.0×` to `1.75×` as score approaches `250`.
- Each car applies its own speed multiplier and PERFECT-window multiplier.
- Procedural roads allow up to five consecutive straight blocks early, four in mid difficulty, and three later.
- The spawner keeps 32 road blocks ahead and starts with a pool of 42 blocks.

## 5. Scoring and skill system — Implemented

### Normal turn

- Adds `1` point.
- Resets the PERFECT combo to zero.

### PERFECT turn

- A turn is PERFECT when made within the car’s timing/position window.
- Base PERFECT window: `0.34`, modified by the selected car’s handling multiplier.
- Each PERFECT increments the combo.
- Score multiplier formula:

  `multiplier = clamp(1 + combo / 5, 1, 5)`

- PERFECT feedback includes on-screen text, a small camera shake, and a smooth low-amplitude Android haptic (`12 ms`, amplitude `35`).
- A new personal best is announced once per run with dedicated visual feedback, sound (`0.8` volume), and stronger haptic (`42 ms`, amplitude `100`).

### Gems

- A normal gem adds `1` run gem.
- Each gem also adds `2` score points.
- Run gems are transferred to the permanent wallet safely, including across revives.

### Logged score milestones

`10`, `25`, `50`, `100`, `200`, and `500`.

## 6. Procedural road and special blocks — Implemented

All road types use one pooled platform prefab. Runtime configuration changes the color, surface icon, and behavior, so separate prefabs are not required for every special block.

- Special blocks begin appearing at score `12`.
- Hazardous crumble and moving blocks join the selection at score `35`.
- Special-block chance scales from `6%` to `16%` with difficulty.
- Normal-block gem chance scales from `12%` to `20%`.
- The first encounter with each special type displays a teaching tooltip and pauses play.
- Every special block has a surface icon as well as a color, improving recognition and color accessibility.
- Block colors and activation-text color are editable through the Unity color picker in `GameplayConfig`.

| Block | Icon | Current behavior | Current color role |
|---|---|---|---|
| Normal | None | Standard road; may contain a gem | Theme-specific |
| Speed Boost | Lightning | `1.35×` temporary speed for 3 seconds | Orange |
| Slow Motion | Clock | `0.72×` temporary speed for 3 seconds | Cyan |
| Shield | Shield | Protects against one fall | Green |
| Gem Magnet | Magnet | Collects nearby gems within radius `9` | Purple |
| Crumbling | Crack | Falls shortly after contact/exit | Dark red |
| Moving | Arrows | Oscillates vertically by about `0.22` units | Yellow |
| Mystery | Question mark | Awards a random `3–8` gems | Pink |

Special-block activation messages use black text for contrast against their colored feedback.

## 7. Game modes — Implemented

### Endless run

- Unseeded procedural road.
- Primary personal-best and score-chasing mode.

### Daily seeded run

- The road seed is derived from the UTC year and day of year.
- Players receive the same procedural sequence for that day, subject to matching build/configuration.
- The daily SCORE RUSH target is also seeded and ranges from `45` to `100`.

## 8. Cars, garage, and themes — Implemented

### Car collection

- The current catalog contains **53 cars/variants**.
- Four entries begin unlocked.
- Locked cars can use gem prices and are currently eligible for rewarded-ad unlocks.
- Car prices currently span roughly `100–825` gems, with two police/taxi entries at `350` and the starter entries free.
- Driving profiles include balanced, power, pursuit, precision, cruiser, sprinter/rush, and control styles.
- Current catalog stat range is approximately:
  - speed multiplier: `0.94–1.08`;
  - PERFECT-window multiplier: `0.90–1.22`.
- The garage/store shows the selected model, speed, handling, driving style, price, ownership state, and select/buy actions.

### Legacy color skins

Crimson, Ocean, Lime, Neon Pink, Gold, and Violet at `0`, `50`, `120`, `250`, `500`, and `900` gems.

### Themes

Sunset, Neon, and Space at `0`, `200`, and `500` gems.

## 9. Challenges and achievements — Implemented

The Challenges screen has three tabs with five entries each. Rewards must be claimed and are protected from duplicate claims within their period.

### Daily challenges

| Challenge | Requirement | Reward |
|---|---:|---:|
| Score Rush | Daily seeded target `45–100` | 100 gems |
| Daily Driver | Finish 3 runs | 20 gems |
| Gem Hunter | Collect 15 gems | 20 gems |
| Combo Dash | Reach combo 6 | 25 gems |
| Score Sprint | Earn 200 total score | 30 gems |

### Weekly challenges

| Challenge | Requirement | Reward |
|---|---:|---:|
| Road Regular | Finish 15 runs | 75 gems |
| Score Storm | Earn 1,000 total score | 75 gems |
| Gem Vault | Collect 75 gems | 75 gems |
| Combo Champion | Reach combo 12 | 100 gems |
| Road Legend | Reach score 150 | 100 gems |

### Missions

Missions currently use the daily period/reset key.

| Mission | Requirement | Reward |
|---|---:|---:|
| Score Master | Reach score 60 | 25 gems |
| Combo Master | Reach combo 8 | 25 gems |
| Gem Collector | Collect 15 gems | 25 gems |
| Road Test | Finish 5 runs | 30 gems |
| Point Bank | Earn 300 total score | 40 gems |

### Local achievement rewards

| Achievement | Requirement | Reward |
|---|---:|---:|
| Road Rookie | Reach score 50 | 25 gems |
| Combo Master | Reach combo 10 | 35 gems |
| Road Legend | Reach score 200 | 75 gems |

These are currently in-game rewards. Google Play Games achievement definitions/synchronization are a proposed extension.

## 10. Economy — Implemented

### Currency

Gems are the only spendable currency.

### Gem sources

- Collected during runs.
- Mystery blocks (`3–8`).
- Daily gift (`10`).
- Rewarded store ad (`10`).
- Challenge and achievement rewards.
- Optional post-run gem doubling.
- Real-money gem packs.

### Gem sinks

- Cars/variants.
- Legacy skins.
- Visual themes.

### In-app purchases

| Product | Gems | Current fallback price |
|---|---:|---:|
| `gems_50` | 50 | ₹30 |
| `gems_100` | 100 | ₹70 |
| `gems_150` | 150 | ₹130 |
| `gems_200` | 200 | ₹180 |

Google Play’s localized price string is preferred when available. The runtime font fallback includes the Indian rupee glyph.

## 11. Advertising — Implemented

- Mediation: Unity LevelPlay, with AdMob configuration.
- Rewarded placements:
  - revive;
  - double run gems;
  - unlock a car;
  - receive store gems.
- Interstitial: after every four completed runs in the current app session.
- Adaptive banner: menu, game over, store/garage, gem store, and challenges.
- Banners are hidden during active gameplay and pause.
- Banner layout respects safe area and reserves UI space.
- Privacy controls default to non-personalized ads and expose consent hooks/settings.

## 12. UX and screen flow — Implemented

```text
Launch
  ├─ Google Play Games sign-in ─┐
  └─ Continue as guest ─────────┴─> Player name (when needed) -> Main menu

Main menu
  ├─ Play -> Tutorial (first run) -> HUD -> Pause / Game over
  │                                      ├─ Revive -> HUD
  │                                      ├─ Retry -> New run from start
  │                                      └─ Home -> Main menu
  ├─ Challenges -> Daily run / claim rewards
  ├─ Store / Garage -> Buy, ad-unlock, or select cars and themes
  ├─ Gem store -> Daily gift, rewarded gems, or IAP
  ├─ Privacy
  ├─ Account sign-in/delete
  └─ Quit confirmation
```

### Responsive presentation

- Portrait canvas with safe-area handling.
- Full-screen animated background covers menu and modal screens.
- Home HUD positions best score, player name, gem balance, and the gem-store plus button independently to prevent clipping on narrow phones.
- Pooled, animated toast messages provide status, warnings, PERFECT feedback, rewards, and special-block activations.

## 13. Visual, audio, and haptic direction — Implemented

### Visual identity

- Dark arcade backdrop with high-saturation neon buttons and readable white display lettering.
- Low-poly cars and roads viewed with an angled follow camera.
- Theme-colored normal road blocks and fixed semantic colors/icons for special blocks.
- Camera shake communicates PERFECT timing and falling, with a stronger death response.
- Smooth `0.16` second OutCubic visual car turns.

### Dynamic backgrounds

- Three background palettes.
- Twenty transition styles including fades, directional wipes, radial/elliptical irises, waves, spiral, diamond, bands, checker, dissolve, aurora, and ripple effects.
- Gameplay background changes approximately every 10 seconds; menu changes approximately every 9 seconds.

### Audio and haptics

- Separate menu and gameplay music levels (`0.55` / `0.65`).
- Gem-pickup audio and dedicated new-best audio, alongside separate looping menu/gameplay music.
- Music and haptics are individually toggleable and saved.
- PERFECT haptics are deliberately subtle; new-best feedback is more noticeable.

## 14. Accounts, cloud, privacy, and data — Implemented

- Google Play Games sign-in with guest fallback.
- Firebase Authentication exchanges the Play Games server auth code.
- Cloud Firestore stores player documents in the `players` collection.
- Cloud schema version: `6`; delayed save batching: `2` seconds.
- Local progress uses PlayerPrefs and merges with cloud progress after sign-in.
- Synced progress includes identity/profile and meaningful game progression such as best score, gems, selections, unlocks, settings, challenges, and rewards.
- Account deletion removes the Firestore player document, deletes the Firebase user, and changes the device to guest/cloud-disabled mode.
- Privacy policy and privacy settings are available in-game.

## 15. Analytics — Implemented

Firebase Analytics currently records:

- screen views and button clicks;
- run start, failure, abandonment, milestones, duration, turns, PERFECT count, gems, combo, and daily-mode status;
- special-block use;
- tutorial completion;
- pause/resume and settings changes;
- cosmetic purchase/select results;
- rewarded/interstitial ad requests and results;
- challenge/reward grants;
- IAP start, completion, value/currency, and failure;
- store availability and cloud-save status.

This event coverage is a good foundation for measuring the proposals below.

## 16. Technical overview — Implemented

- Unity project with one enabled scene: `Assets/Scenes/Game.unity`.
- Core orchestration: `GameManager` state machine (`Menu`, `Playing`, `Reviving`, `Paused`, `GameOver`).
- Driving/input: `CarController`.
- Road generation/pooling: `PlatformSpawner` and `Platform`.
- Persistent local progression: `GameProgress` and `ChallengeProgress`.
- Runtime interface: `RuntimeGameUI` plus reusable UI prefabs.
- Cars: `CarCatalog` ScriptableObject data.
- Economy/IAP: `GemStoreManager` and Unity IAP.
- Online save/account: `CloudSaveManager`, Firebase Authentication, and Firestore.
- Ads: `MediatedAdsManager` and LevelPlay.
- Tuning: typed configuration plus Resources assets/JSON.
- Target frame rate: `60`; maximum delta time: `0.1`.
- Android minimum SDK: 25.

## 17. Current design/production risks

These are observations, not confirmed player-facing defects.

1. **Too many cars can feel interchangeable.** There are 53 variants but a narrow stat range and repeated handling profiles. Collection breadth is strong; identity is weaker.
2. **Rewarded car unlocks may bypass the economy.** If every locked car can be unlocked by one ad, gem prices and long-term saving lose meaning.
3. **Revived scores need a published leaderboard rule.** Revive is now capped to once per run, but assisted scores are still submitted to the normal board.
4. **Mission identity overlaps daily challenges.** Missions currently reset on the daily period and repeat similar goals, so three tabs may feel like duplicated checklists.
5. **Risk/reward branches need device playtesting.** The split-road system is implemented, but reward rate and readability should be tuned using branch-choice analytics.
6. **Local achievements are not yet Play Games achievements.** Play leaderboards are integrated, while achievement definitions/synchronization remain a later extension.
7. **Consumable fulfillment is client-side.** Before scaling purchases, add robust receipt/backend validation and idempotent fulfillment review.
8. **Remote values need disciplined rollout.** Firebase Remote Config is connected; malformed or overly aggressive production values can still damage a session.
9. **Observability needs release validation.** Crashlytics, Performance, and richer sign-in events are integrated but must be verified from a signed closed-test build.

## 18. Engagement and live-ops expansion — Implemented in 1.1.0

- Crashlytics non-fatals, Firebase Performance custom traces, frame-stall reporting, and detailed Play Games/Firebase sign-in analytics.
- Google Play Games score submission with offline retry, daily/weekly/all-time timeframe selection, a separate seeded-daily board ID, and entry points on the menu and game-over screen.
- A translucent local personal-best ghost for daily seeded runs. Daily-only playback guarantees the ghost follows the same generated road.
- Occasional readable safe-versus-risky road branches. Risk tiles use hazards and high gem probability; branch choice is logged for balancing.
- Five car-mastery levels with play-earned XP, garage progress, cyan/gold/prismatic trail and paint rewards, and Firestore synchronization.
- One context-aware post-run objective, selected from near-best score, daily progress, combo, car unlock, or mastery motivation.
- Firebase Remote Config via the `game_config_json` parameter for difficulty, road branches, rewards, challenges, and interstitial cadence.
- A controlled Play Store icon, feature graphic, and screenshot experiment plan in `StoreAssets/PLAY_STORE_EXPERIMENT_PLAN.md`.

## 19. Recommended roadmap — Next iterations

The best next additions are not “more buttons.” They should strengthen competition, run variety, and reasons to return while preserving the one-touch core.

### Priority 0 — Stabilize and measure before adding content

| Proposal | Why it matters | Effort | First success signal |
|---|---|---:|---|
| Add Crashlytics and performance traces | Catch device-specific freezes, sign-in failures, and slow flows before they become reviews | S–M | Crash-free users and sign-in completion improve |
| Build a funnel dashboard | Make launch → sign-in/guest → tutorial → first run → second run visible | S | Drop-off points become measurable |
| Add a single-revive-per-run rule | Protect score integrity and avoid ad fatigue | S | Stable revive use with no fall in runs/session |
| Review purchase validation/idempotency | Protect paid currency and prevent double fulfillment | M | Zero duplicate/unsupported grants |
| Connect remote configuration | Tune difficulty, rewards, ads, and feature flags without emergency releases | M | Safe staged changes without app updates |

### Priority 1 — Highest player-value additions

#### 1. Daily, weekly, and all-time leaderboards

Submit the best score at game over and place a leaderboard shortcut on both the main menu and game-over screen. The daily seeded mode is especially suitable because all players receive a comparable route. Google Play Games automatically supports daily, weekly, and all-time leaderboard timeframes, and its quality guidance recommends surfacing leaderboards after critical transitions such as player death.

**Important:** separate normal-run and daily-seed boards, cap implausible scores, and either disallow revived scores or mark them as assisted.

#### 2. Ghost race against personal best

Record only the player’s turn events/time offsets, then render a translucent non-colliding ghost from the best run. This creates competition without multiplayer servers and gives every run a visible target.

**MVP:** local personal-best ghost only. Later: friend/top-player ghosts after anti-cheat and data-size review.

#### 3. Risk/reward road choices

Occasionally split the road into two readable routes:

- safe route: wider/longer rhythm, normal rewards;
- risky route: tighter timing, extra gems or temporary multiplier;
- routes merge after a short segment.

This adds decision-making while keeping the same one-tap control.

#### 4. Better run recap and near-goal prompts

At game over, show one useful next action, for example:

- “2 points from your best”;
- “1 more run completes Daily Driver”;
- “4 gems until the next car”;
- “New longest PERFECT chain.”

Do not show all prompts at once. Choose the most motivating one.

#### 5. Car mastery instead of more near-duplicate cars

Give each car a short mastery track using play-based goals: distance/score, PERFECT turns, and challenge completions. Rewards should be cosmetic—paint, trail, badge, wheel glow—not permanent score advantages.

This makes the existing 53-car catalog meaningful without producing dozens of additional models.

### Priority 2 — Retention and expression

#### 6. Seven-day streak with forgiveness

- Reward playing, not merely opening the app.
- Allow one missed-day recovery per week.
- Use mostly cosmetics and moderate gems.
- Avoid huge final-day currency inflation.

#### 7. Expanded Play Games achievements and Game Stats

Convert the three local achievements into a progression set of at least ten visible achievements, including early, medium, and long-term goals. Suggested categories:

- first PERFECT;
- combo 5 / 10 / 25;
- score 50 / 150 / 500;
- collect 100 / 1,000 lifetime gems;
- use every special block;
- own one car from five families;
- complete seven daily seeded runs.

Also submit recurring statistics such as runs, turns, PERFECT turns, gems, best combo, and score to support the newer Google Play Games engagement surfaces.

#### 8. Shareable result card

Generate a clean portrait card after a new best showing score, combo, selected car, and a short challenge phrase. Never include player email or internal IDs. Add native share only after the image is ready.

#### 9. Cosmetic trails and block-impact effects

Use lightweight particle/trail rewards tied to mastery and challenges. These are more visible during play than another menu-only paint variant and do not affect fairness.

#### 10. Accessibility controls

- Haptic strength: Off / Soft / Strong.
- Reduced camera shake.
- Reduced background motion.
- High-contrast road mode.
- Optional larger HUD.

The icon system already prevents color from being the only special-block signal.

### Priority 3 — Live operations after the foundation works

#### 11. Weekly modifiers

Examples: Magnet Week, Double Mystery, Fragile Road, Turbo Rush, or Shield Start. Use the existing config system and feature flags so each modifier is explicit before the run and never feels like a hidden rule change.

#### 12. Seasonal cosmetic event

Use a four-week track with gameplay goals and cosmetic rewards. Avoid adding a second premium currency initially. First prove that players return for the free event loop.

#### 13. Store-listing experiments

Test one asset at a time—icon, feature graphic, then first screenshot—and judge results using Play Console’s install/open-click metrics. The first screenshots should show real gameplay, PERFECT/combo mastery, special blocks, and car collection rather than mostly menus.

## 19. What not to add yet — Proposed restraint

- **Real-time PvP:** expensive, latency-sensitive, and unnecessary before local ghosts/leaderboards prove competition demand.
- **Complex steering controls:** would weaken the one-touch identity.
- **More currencies:** gems already cover the economy; another currency adds confusion before the current sinks are balanced.
- **Stat-heavy car upgrades:** risks pay-to-win and makes leaderboards harder to compare.
- **More forced ads:** active gameplay is correctly ad-free. Protect that promise.
- **Large story mode:** high content cost with little evidence that it solves the present retention questions.

## 20. Recommended delivery order

### Milestone A — Trustworthy beta

- Crash/performance monitoring.
- Sign-in funnel and error reason analytics.
- One revive per run.
- Purchase fulfillment hardening.
- Remote tuning connection.

### Milestone B — Competitive update

- Normal and daily leaderboards.
- Ten or more Play Games achievements.
- Improved game-over recap and near-goal prompt.
- Personal-best ghost MVP.

### Milestone C — Deeper runs

- Risk/reward split routes.
- Two or three new obstacle patterns that preserve one-touch control.
- Car mastery and cosmetic trails.
- Accessibility options.

### Milestone D — Repeatable live content

- Weekly modifiers.
- Streak with forgiveness.
- Seasonal cosmetic track.
- Store-listing and in-game A/B tests.

## 21. Metrics and experiment plan

Establish a baseline before setting absolute targets. Then define improvement targets relative to that baseline.

### Product health

- Crash-free users and sessions.
- ANR/freeze rate and slow startup/sign-in traces.
- Google Play Games sign-in completion and failure reasons.
- Cloud merge/upload success.
- Purchase completion and duplicate-grant rate.

### Onboarding

- Launch → login/guest selection.
- Login/guest → tutorial completion.
- Tutorial completion → first run completed.
- First run → second run in the same session.

### Engagement

- D1, D7, and D30 retention.
- Runs per session and sessions per active user.
- Median run duration and score distribution.
- PERFECT rate and best-combo distribution.
- Daily seeded participation and completion.
- Challenge view, completion, and claim rates.
- Car ownership breadth and car-selection diversity.

### Monetization guardrails

- Rewarded offer impressions, starts, completions, and post-ad continuation.
- Interstitial impressions per active user.
- Retention and runs/session split by interstitial exposure.
- IAP buyer conversion and revenue, alongside crash-free users and retention.

### First experiments

1. Game-over recap: basic vs one personalized near-goal prompt.
2. Interstitial cadence: current four runs vs a less frequent variant.
3. Daily challenge entry: current placement vs prominent home card.
4. First-run pacing: current acceleration vs gentler first 30 points.

Run one meaningful change per experiment. Keep a control group and evaluate retention/stability as guardrails, not revenue alone.

## 22. Definition of success for the next major update

The next update is successful when:

- sign-in and cloud save are reliable across tested devices/accounts;
- players can compare scores immediately after a run;
- a returning player has a clear daily reason to play;
- each run presents at least one meaningful choice or personal target;
- the existing cars gain identity through mastery/cosmetics;
- ads remain absent from active driving;
- engagement improves without reducing crash-free users or increasing player complaints.

## 23. External guidance used for the proposed roadmap

- [Google Play Games leaderboards](https://developer.android.com/games/pgs/leaderboards) — daily, weekly, all-time, public/social boards and score submission behavior.
- [Google Play Games Services quality checklist](https://developer.android.com/games/pgs/quality) — achievement breadth and surfacing leaderboards after important transitions.
- [Google Play Games Level Up guidelines](https://developer.android.com/games/guidelines) — recurring Game Stats that support player engagement features.
- [Firebase Remote Config](https://firebase.google.com/docs/remote-config) — remotely tune behavior, staged rollouts, personalization, and A/B testing.
- [Firebase A/B Testing with Remote Config](https://firebase.google.com/docs/ab-testing/abtest-config) — compare variants using retention, revenue, and stability metrics.
- [Firebase Crashlytics crash-free metrics](https://firebase.google.com/docs/crashlytics/crash-free-metrics) and [Firebase Performance Monitoring](https://firebase.google.com/docs/perf-mon) — release health and performance measurement.
- [Google Play store-listing experiments](https://support.google.com/googleplay/android-developer/answer/12053285) — controlled tests of listing graphics and text.

---

### Maintenance rule

Update this GDD whenever a system ships, a tuning default changes, or a proposal is accepted/rejected. Move completed roadmap items into their relevant **Implemented** section rather than leaving them labeled as proposals.
