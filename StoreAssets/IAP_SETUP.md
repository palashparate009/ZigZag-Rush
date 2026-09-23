# ZigZag Rush - Google Play IAP setup

The game uses Unity IAP 5.4.2 and defines four consumable products in `GemStoreManager.cs`.

Create these exact one-time product IDs in Google Play Console after the developer account is verified:

| Product ID | Player receives | India price |
| --- | ---: | ---: |
| `gems_50` | 50 gems | ₹30 |
| `gems_100` | 100 gems | ₹70 |
| `gems_150` | 150 gems | ₹130 |
| `gems_200` | 200 gems | ₹180 |

For each product:

1. Open the ZigZag Rush app in Play Console.
2. Go to **Monetize with Play > Products > In-app products**.
3. Create the product with the exact ID above, set it active, and configure the India price.
4. Upload a new Android App Bundle containing this IAP integration to an internal test track.
5. Add tester Gmail accounts to the license-test and internal-test lists, then install the game from the Play test link. A sideloaded APK cannot reliably test real Google Play purchases.

The Store screen displays Google Play's localized price when the product loads. The ₹ strings in the game are fallbacks only; Google Play Console is the billing authority.

The first daily item is not an IAP product. It awards 10 gems once per UTC day and records the claim locally. After claiming it, the same Store button becomes a repeatable rewarded-ad offer that awards 10 gems only after a completed ad.

Before a public launch, add server-side receipt validation and cloud inventory storage. The current implementation prevents repeat fulfillment of the same pending transaction on one installation, but locally stored consumable balances cannot be recovered after uninstall and are not tamper-proof.
