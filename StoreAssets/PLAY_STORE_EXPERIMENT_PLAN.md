# ZigZag Rush store listing experiment plan

Listing experiments are configured in Play Console, not in the Unity project. Use the existing production assets as the control:

- `AppIcon-512.png`
- `FeatureGraphic-1024x500.png`
- current phone screenshots

## Test order

Run one asset hypothesis at a time so the result is attributable.

1. **Icon experiment** — control vs. a simpler high-contrast car/road icon with less background detail.
2. **Feature graphic experiment** — control vs. a version that clearly shows the one-tap zigzag mechanic and `TURN • COMBO • SURVIVE` promise.
3. **Screenshot experiment** — control order vs. an order led by gameplay, PERFECT/combo feedback, risky branch choice, car mastery, and leaderboard competition.

## Recommended setup

- Audience: the main English (United States) listing first.
- Variants: control plus one challenger.
- Metric: retained first-time installers when available; otherwise first-time installers.
- Traffic: equal split.
- Duration: do not stop merely because an early result looks positive; wait for Play Console's confidence guidance and a representative weekday/weekend mix.
- Promote only a clear winner, then use that winner as the next control.

Record the hypothesis, dates, traffic, result, and decision for every test. Do not test the icon, feature graphic, and screenshot order simultaneously in one experiment.
