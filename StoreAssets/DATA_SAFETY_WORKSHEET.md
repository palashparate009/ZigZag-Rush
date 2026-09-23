# Google Play Data safety worksheet

Complete this against the final LevelPlay dashboard configuration and every enabled mediated network. Do not submit the form from this draft alone.

## Game-owned data

The current game code stores score, gems, unlocks, selected cosmetics, challenge progress, tutorial status, settings, and service configuration in Unity PlayerPrefs. Google Play Games can authenticate a Firebase account, and Firebase Cloud Firestore stores synchronized player progress.

The main-menu Delete Account flow deletes the player's Firestore document, Firebase Authentication user, and local PlayerPrefs progress. Verify whether Google Play Games requires a separate deletion or revocation instruction in the hosted policy.

## Likely advertising SDK declarations

Confirm in each provider's current documentation whether the final build collects or shares:

- Device or other identifiers, including advertising ID
- Approximate location inferred from IP
- App interactions and advertising data
- Diagnostics, crash data, and performance data
- Fraud-prevention and security signals

Likely purposes include advertising or marketing, analytics, app functionality, fraud prevention, security, and compliance. Collection, sharing, optionality, retention, and encryption answers must match the actual SDK and consent configuration.

## Console checks

- Declare that the app contains ads.
- Add the hosted privacy-policy URL in the store listing and inside the app.
- Complete the Data safety form after all networks and adapters are final.
- Complete Target audience and content accurately; apply Families policies if children are targeted.
- Complete the content-rating questionnaire based on the real game and ad content.
- Verify the Advertising ID declaration if any enabled SDK uses it.
- Publish clear data-deletion instructions for the in-game Delete Account button and any separate Google Play Games revocation process.
- Export and archive the final LevelPlay network list and each provider disclosure with the release record.
