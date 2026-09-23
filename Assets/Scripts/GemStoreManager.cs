using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

[DefaultExecutionOrder(-50)]
public sealed class GemStoreManager : MonoBehaviour
{
    public static string[] ProductIds => Array.ConvertAll(Products, product => product.productId);
    public static int[] GemAmounts => Array.ConvertAll(Products, product => product.gems);
    public static string[] FallbackPrices => Array.ConvertAll(Products, product => product.fallbackPrice);
    private static IapProductConfig[] Products => ConfigManager.Current.economy.iapProducts ?? Array.Empty<IapProductConfig>();

    public static GemStoreManager Instance { get; private set; }
    public event Action StoreChanged;
    public event Action<string> StoreMessage;

    public bool IsReady { get; private set; }
    public bool PurchaseInProgress { get; private set; }
    public string StatusText { get; private set; } = "CONNECTING TO GOOGLE PLAY...";

    private StoreController storeController;
    private string activePurchaseProductId;

    public static GemStoreManager Create()
    {
        if (Instance != null) return Instance;
        GemStoreManager existing = FindFirstObjectByType<GemStoreManager>();
        if (existing != null) return existing;

        GameObject host = new GameObject("Gem Store Manager");
        return host.AddComponent<GemStoreManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        storeController = UnityIAPServices.StoreController();
        storeController.OnStoreConnected += OnStoreConnected;
        storeController.OnStoreDisconnected += OnStoreDisconnected;
        storeController.OnProductsFetched += OnProductsFetched;
        storeController.OnProductsFetchFailed += OnProductsFetchFailed;
        storeController.OnPurchasesFetched += OnPurchasesFetched;
        storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        storeController.OnPurchasePending += OnPurchasePending;
        storeController.OnPurchaseFailed += OnPurchaseFailed;
        storeController.OnPurchaseDeferred += OnPurchaseDeferred;
        storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;

        try
        {
            await storeController.Connect();
        }
        catch (Exception exception)
        {
            IsReady = false;
            StatusText = "STORE OFFLINE - CHECK CONNECTION";
            Debug.LogWarning($"Unity IAP connection failed: {exception.Message}");
            Notify("STORE OFFLINE - CHECK CONNECTION");
        }
    }

    private void OnDestroy()
    {
        if (storeController != null)
        {
            storeController.OnStoreConnected -= OnStoreConnected;
            storeController.OnStoreDisconnected -= OnStoreDisconnected;
            storeController.OnProductsFetched -= OnProductsFetched;
            storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
            storeController.OnPurchasesFetched -= OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
            storeController.OnPurchasePending -= OnPurchasePending;
            storeController.OnPurchaseFailed -= OnPurchaseFailed;
            storeController.OnPurchaseDeferred -= OnPurchaseDeferred;
            storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;
        }
        if (Instance == this) Instance = null;
    }

    public string GetLocalizedPrice(string productId, string fallback)
    {
        Product product = storeController != null ? storeController.GetProductById(productId) : null;
        return product != null && product.metadata != null && !string.IsNullOrWhiteSpace(product.metadata.localizedPriceString)
            ? product.metadata.localizedPriceString
            : fallback;
    }

    public bool CanPurchase(string productId)
    {
        Product product = storeController != null ? storeController.GetProductById(productId) : null;
        return IsReady && !PurchaseInProgress && product != null && product.availableToPurchase;
    }

    public void Buy(string productId)
    {
        if (!CanPurchase(productId))
        {
            Notify(IsReady ? "PURCHASE ALREADY IN PROGRESS" : "STORE IS STILL CONNECTING");
            return;
        }

        PurchaseInProgress = true;
        activePurchaseProductId = productId;
        GameAnalytics.PurchaseStarted(productId);
        StatusText = "PROCESSING PURCHASE...";
        StoreChanged?.Invoke();
        storeController.PurchaseProduct(productId);
    }

    private void OnProductsFetched(List<Product> products)
    {
        IsReady = products != null && products.Count > 0;
        GameAnalytics.StoreStatus(IsReady ? "products_available" : "products_empty", products?.Count ?? 0);
        PurchaseInProgress = false;
        StatusText = IsReady ? "SECURE PURCHASE THROUGH GOOGLE PLAY" : "PRODUCTS NOT YET AVAILABLE";
        StoreChanged?.Invoke();
        storeController.FetchPurchases();
    }

    private void OnStoreConnected()
    {
        GameAnalytics.StoreStatus("connected");
        StatusText = "LOADING STORE PRODUCTS...";
        StoreChanged?.Invoke();
        List<ProductDefinition> definitions = new List<ProductDefinition>();
        foreach (IapProductConfig product in Products)
            if (product != null && !string.IsNullOrWhiteSpace(product.productId))
                definitions.Add(new ProductDefinition(product.productId, ProductType.Consumable));
        storeController.FetchProducts(definitions);
    }

    private void OnPurchasePending(PendingOrder order)
    {
        int totalGems = 0;
        string purchasedProductId = string.Empty;
        foreach (var item in order.CartOrdered.Items())
        {
            if (string.IsNullOrEmpty(purchasedProductId)) purchasedProductId = item.Product.definition.id;
            totalGems += GemsForProduct(item.Product.definition.id);
        }

        string transactionId = order.Info != null ? order.Info.TransactionID : string.Empty;
        if (totalGems <= 0 || string.IsNullOrWhiteSpace(transactionId))
        {
            PurchaseInProgress = false;
            Debug.LogError("Unity IAP returned an invalid gem order. It was not confirmed.");
            Notify("PURCHASE COULD NOT BE VERIFIED");
            return;
        }

        string fulfilledKey = "IAP_Fulfilled_" + transactionId;
        if (PlayerPrefs.GetInt(fulfilledKey, 0) == 0)
        {
            PlayerPrefs.SetInt(fulfilledKey, 1);
            GameProgress.AddGems(totalGems);
            Product purchasedProduct = storeController.GetProductById(purchasedProductId);
            double price = purchasedProduct?.metadata != null
                ? decimal.ToDouble(purchasedProduct.metadata.localizedPrice)
                : 0d;
            string currency = purchasedProduct?.metadata?.isoCurrencyCode;
            GameAnalytics.PurchaseCompleted(purchasedProductId, totalGems, price, currency);
            Notify($"PURCHASE COMPLETE +{totalGems} GEMS");
        }

        storeController.ConfirmPurchase(order);
        PurchaseInProgress = false;
        activePurchaseProductId = null;
        StatusText = "SECURE PURCHASE THROUGH GOOGLE PLAY";
        StoreChanged?.Invoke();
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        GameAnalytics.PurchaseFailed(activePurchaseProductId, "failed_or_cancelled");
        activePurchaseProductId = null;
        PurchaseInProgress = false;
        StatusText = IsReady ? "SECURE PURCHASE THROUGH GOOGLE PLAY" : "STORE OFFLINE - CHECK CONNECTION";
        StoreChanged?.Invoke();
        Notify("PURCHASE CANCELLED OR FAILED");
    }

    private void OnPurchaseDeferred(DeferredOrder order)
    {
        GameAnalytics.PurchaseFailed(activePurchaseProductId, "deferred");
        activePurchaseProductId = null;
        PurchaseInProgress = false;
        StatusText = "PURCHASE WAITING FOR APPROVAL";
        StoreChanged?.Invoke();
        Notify("PURCHASE IS WAITING FOR APPROVAL");
    }

    private void OnPurchaseConfirmed(Order order)
    {
        PurchaseInProgress = false;
        StatusText = IsReady ? "SECURE PURCHASE THROUGH GOOGLE PLAY" : StatusText;
        StoreChanged?.Invoke();
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        GameAnalytics.StoreStatus("disconnected");
        IsReady = false;
        PurchaseInProgress = false;
        StatusText = "STORE OFFLINE - CHECK CONNECTION";
        StoreChanged?.Invoke();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        GameAnalytics.StoreStatus("product_fetch_failed");
        IsReady = false;
        PurchaseInProgress = false;
        StatusText = "PRODUCTS NOT YET AVAILABLE";
        StoreChanged?.Invoke();
        Notify("STORE PRODUCTS NOT AVAILABLE");
    }

    private void OnPurchasesFetched(Orders orders)
    {
        PurchaseInProgress = false;
        StoreChanged?.Invoke();
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        GameAnalytics.StoreStatus("purchase_history_fetch_failed");
        PurchaseInProgress = false;
        StoreChanged?.Invoke();
    }

    private void Notify(string message)
    {
        StoreMessage?.Invoke(message);
    }

    private static int GemsForProduct(string productId)
    {
        foreach (IapProductConfig product in Products)
            if (product != null && string.Equals(product.productId, productId, StringComparison.Ordinal)) return product.gems;
        return 0;
    }
}
