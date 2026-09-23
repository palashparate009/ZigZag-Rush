using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class RuntimeUIPrefabBuilder
{
    // Prefab authoring belongs in the editor; player builds only instantiate the saved assets.
    private const string SourcePrefabPath = "Assets/Resources/UI/ZigZagRuntimeUI.prefab";
    private const string OutputFolder = "Assets/Resources/UI/RuntimeParts";
    private static readonly string[] RequiredPrefabs =
    {
        "LoginPanel.prefab",
        "NamePanel.prefab",
        "LobbyPlayerName.prefab",
        "QuitButton.prefab",
        "QuitPanel.prefab",
        "SpecialTileFtuePanel.prefab",
        "GarageEnhancements.prefab"
    };

    static RuntimeUIPrefabBuilder()
    {
        EditorApplication.delayCall += EnsurePrefabs;
    }

    [MenuItem("ZigZag/UI/Rebuild Runtime UI Prefabs")]
    public static void RebuildPrefabs()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null)
        {
            Debug.LogError($"Runtime UI source prefab was not found at {SourcePrefabPath}.");
            return;
        }

        EnsureOutputFolder();
        RuntimeGameUI ui = source.GetComponent<RuntimeGameUI>();
        SerializedObject serializedUi = new SerializedObject(ui);
        TMP_Text textTemplate = GetReference<TMP_Text>(serializedUi, "menuBestText");
        Button playTemplate = GetReference<Button>(serializedUi, "playButton");
        Button secondaryTemplate = GetReference<Button>(serializedUi, "garageButton");
        Button privacyTemplate = GetReference<Button>(serializedUi, "privacyButton");
        Button chooseCarTemplate = GetReference<Button>(serializedUi, "chooseSkinButton");
        GameObject menuPanel = GetReference<GameObject>(serializedUi, "menuPanel");
        Color panelColour = menuPanel != null && menuPanel.TryGetComponent(out Image menuImage)
            ? menuImage.color
            : new Color(0.025f, 0.035f, 0.075f, 0.98f);

        BuildLoginPanel(textTemplate, playTemplate, secondaryTemplate, panelColour);
        BuildNamePanel(textTemplate, playTemplate, panelColour);
        BuildLobbyPlayerName(textTemplate);
        BuildQuitButton(privacyTemplate);
        BuildQuitPanel(textTemplate, playTemplate, secondaryTemplate);
        BuildSpecialTileFtuePanel(textTemplate);
        BuildGarageEnhancements(textTemplate, chooseCarTemplate);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Runtime UI prefabs rebuilt in {OutputFolder}.");
    }

    private static void EnsurePrefabs()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsurePrefabs;
            return;
        }

        RepairOpenPrefabScriptReferences();
        EnsureChallengePrefabs();
        foreach (string prefab in RequiredPrefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{OutputFolder}/{prefab}") != null) continue;
            RebuildPrefabs();
            return;
        }
    }

    // A prefab stage can retain an empty m_Script after scripts are split into
    // separate files, even though the asset on disk has already been repaired.
    // Repair that live component without rebuilding or replacing its children.
    [MenuItem("ZigZag/UI/Repair Open Prefab Script References")]
    public static void RepairOpenPrefabScriptReferences()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null || stage.prefabContentsRoot == null ||
            !stage.assetPath.StartsWith(OutputFolder + "/", System.StringComparison.Ordinal)) return;

        int repaired = 0;
        foreach (MonoBehaviour component in stage.prefabContentsRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            var type = component.GetType();
            if (type != typeof(LoginPanelView) && type != typeof(NamePanelView) &&
                type != typeof(QuitPanelView) && type != typeof(SpecialTileFtueView) &&
                type != typeof(GarageEnhancementsView)) continue;

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>($"Assets/Scripts/{type.Name}.cs");
            if (script == null || script.GetClass() != type) continue;
            using (var serialized = new SerializedObject(component))
            {
                SerializedProperty scriptReference = serialized.FindProperty("m_Script");
                if (scriptReference == null || scriptReference.objectReferenceValue == script) continue;
                Undo.RecordObject(component, "Repair UI prefab script reference");
                scriptReference.objectReferenceValue = script;
                serialized.ApplyModifiedProperties();
                // Assigning m_Script can recreate the native component; do not
                // access the old wrapper after applying the serialized change.
                repaired++;
            }
        }

        if (repaired > 0)
        {
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Debug.Log($"Repaired {repaired} script reference(s) in the open UI prefab. Your edits are preserved; save the prefab normally.");
        }
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            throw new DirectoryNotFoundException("Assets/Resources/UI is missing.");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Resources/UI", "RuntimeParts");
    }

    [MenuItem("ZigZag/UI/Create Missing Challenge Prefabs")]
    public static void EnsureChallengePrefabs()
    {
        const string cardPath = OutputFolder + "/ChallengeCard.prefab";
        const string listPath = OutputFolder + "/ChallengeList.prefab";
        bool needsCard = !File.Exists(cardPath);
        bool needsList = !File.Exists(listPath);
        if (!needsCard && !needsList) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureOutputFolder();
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null) return;
        var serialized = new SerializedObject(source.GetComponent<RuntimeGameUI>());
        if (needsCard) BuildChallengeCard(
            GetReference<TMP_Text>(serialized, "menuBestText"),
            GetReference<Button>(serialized, "playButton"));
        if (needsList) BuildChallengeList();
        Debug.Log("Challenge card and scrolling list prefabs are ready.");
    }

    private static void BuildChallengeCard(TMP_Text template, Button buttonTemplate)
    {
        GameObject root = CreatePanel("Challenge Card", new Color(0.08f, 0.10f, 0.18f, 0.98f));
        root.AddComponent<LayoutElement>().preferredHeight = 205f;
        SetRect((RectTransform)root.transform, new Vector2(0.5f, 1f), new Vector2(920f, 205f));
        ChallengeCardView view = root.AddComponent<ChallengeCardView>();
        view.titleText = CreateText(root.transform, template, "Title", "CHALLENGE", 28f, Vector2.zero, Vector2.zero);
        SetCardRegion((RectTransform)view.titleText.transform, 0.04f, 0.71f, 0.69f, 0.97f);
        view.titleText.alignment = TextAlignmentOptions.MidlineLeft;
        view.titleText.enableAutoSizing = true;
        view.titleText.fontSizeMin = 18f;
        view.titleText.fontSizeMax = 28f;
        view.descriptionText = CreateText(root.transform, template, "Description", "Challenge objective", 22f, Vector2.zero, Vector2.zero);
        SetCardRegion((RectTransform)view.descriptionText.transform, 0.04f, 0.46f, 0.69f, 0.71f);
        view.descriptionText.alignment = TextAlignmentOptions.MidlineLeft;
        view.descriptionText.enableAutoSizing = true;
        view.descriptionText.fontSizeMin = 16f;
        view.descriptionText.fontSizeMax = 22f;
        view.progressText = CreateText(root.transform, template, "Progress", "0 / 100 - 100 LEFT", 19f, Vector2.zero, Vector2.zero);
        SetCardRegion((RectTransform)view.progressText.transform, 0.04f, 0.04f, 0.69f, 0.27f);
        view.progressText.alignment = TextAlignmentOptions.MidlineLeft;
        view.progressText.enableAutoSizing = true;
        view.progressText.fontSizeMin = 14f;
        view.progressText.fontSizeMax = 19f;

        GameObject bar = CreatePanel("Progress Slider", new Color(0.18f, 0.21f, 0.30f, 1f), false);
        bar.transform.SetParent(root.transform, false);
        SetCardRegion((RectTransform)bar.transform, 0.04f, 0.31f, 0.68f, 0.40f);
        GameObject fill = CreatePanel("Fill", new Color(0.05f, 0.88f, 1f, 1f), false);
        fill.transform.SetParent(bar.transform, false);
        SetStretch((RectTransform)fill.transform);
        view.progressSlider = bar.AddComponent<Slider>();
        view.progressSlider.fillRect = (RectTransform)fill.transform;
        view.progressSlider.minValue = 0;
        view.progressSlider.maxValue = 1;
        view.progressSlider.interactable = false;
        view.progressSlider.navigation = new Navigation { mode = Navigation.Mode.None };
        view.claimButton = CreateButton(root.transform, buttonTemplate, "Claim Gems", "CLAIM\n25 GEMS",
            Vector2.zero, Vector2.zero, new Color(1f, 0.18f, 0.57f, 1f));
        SetCardRegion((RectTransform)view.claimButton.transform, 0.73f, 0.20f, 0.97f, 0.80f);
        view.claimText = view.claimButton.GetComponentInChildren<TMP_Text>(true);
        view.claimText.fontSizeMin = 14f;
        view.claimText.fontSizeMax = 26f;
        GameObject tick = new GameObject("Claimed Tick", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(view.claimButton.transform, false);
        SetRect((RectTransform)tick.transform, new Vector2(0.5f, 0.5f), new Vector2(48f, 48f));
        view.claimedTick = tick.GetComponent<Image>();
        view.claimedTick.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FreeButtonSet/Textures/icons/64x64/checkmark.png");
        view.claimedTick.preserveAspect = true;
        view.claimedTick.raycastTarget = false;
        tick.SetActive(false);
        SavePrefab(root, "ChallengeCard.prefab", true);
    }

    private static void BuildChallengeList()
    {
        GameObject root = new GameObject("Challenge List", typeof(RectTransform), typeof(ScrollRect), typeof(ChallengeListView));
        SetCardRegion((RectTransform)root.transform, 0.055f, 0.25f, 0.945f, 0.71f);
        ChallengeListView view = root.GetComponent<ChallengeListView>();
        GameObject viewport = CreatePanel("Viewport", new Color(0f, 0f, 0f, 0.01f));
        viewport.transform.SetParent(root.transform, false);
        SetStretch((RectTransform)viewport.transform);
        viewport.AddComponent<RectMask2D>();
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        view.content = (RectTransform)content.transform;
        view.content.anchorMin = new Vector2(0f, 1f);
        view.content.anchorMax = Vector2.one;
        view.content.pivot = new Vector2(0.5f, 1f);
        view.content.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        view.scrollRect = root.GetComponent<ScrollRect>();
        view.scrollRect.content = view.content;
        view.scrollRect.viewport = (RectTransform)viewport.transform;
        view.scrollRect.horizontal = false;
        view.scrollRect.vertical = true;
        view.scrollRect.movementType = ScrollRect.MovementType.Clamped;
        view.scrollRect.scrollSensitivity = 40f;
        SavePrefab(root, "ChallengeList.prefab", true);
    }

    private static void SetCardRegion(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T GetReference<T>(SerializedObject owner, string propertyName) where T : Object
    {
        T value = owner.FindProperty(propertyName)?.objectReferenceValue as T;
        if (value == null) throw new MissingReferenceException($"RuntimeGameUI.{propertyName} is not assigned.");
        return value;
    }

    private static void BuildLoginPanel(TMP_Text textTemplate, Button playTemplate, Button secondaryTemplate, Color panelColour)
    {
        GameObject root = CreatePanel("Account Sign In", panelColour);
        LoginPanelView view = root.AddComponent<LoginPanelView>();
        TMP_Text title = CreateText(root.transform, textTemplate, "Welcome", "WELCOME TO\nZIGZAG RUSH", 68f,
            new Vector2(0.5f, 0.73f), new Vector2(900f, 260f));
        title.fontStyle = FontStyles.Bold;
        CreateText(root.transform, textTemplate, "Sign In Explanation",
            "Sign in with Google Play Games to use your Google account, achievements, and Firebase cloud save.\n\nOr continue on this device as a guest.",
            31f, new Vector2(0.5f, 0.56f), new Vector2(860f, 360f));
        view.playGamesButton = CreateButton(root.transform, playTemplate, "Google Play Games Sign In",
            "SIGN IN WITH GOOGLE PLAY GAMES", new Vector2(0.5f, 0.39f), new Vector2(800f, 115f),
            new Color(1f, 0.18f, 0.57f, 1f));
        view.guestButton = CreateButton(root.transform, secondaryTemplate, "Continue As Guest", "CONTINUE AS GUEST",
            new Vector2(0.5f, 0.29f), new Vector2(640f, 100f), new Color(0.08f, 0.1f, 0.18f, 0.96f));
        view.statusText = CreateText(root.transform, textTemplate, "Login Status", string.Empty, 27f,
            new Vector2(0.5f, 0.20f), new Vector2(850f, 120f));
        SavePrefab(root, "LoginPanel.prefab", false);
    }

    private static void BuildNamePanel(TMP_Text textTemplate, Button playTemplate, Color panelColour)
    {
        GameObject root = CreatePanel("Choose Player Name", panelColour);
        NamePanelView view = root.AddComponent<NamePanelView>();
        TMP_Text title = CreateText(root.transform, textTemplate, "Choose Name Title", "CHOOSE YOUR PLAYER NAME", 60f,
            new Vector2(0.5f, 0.70f), new Vector2(900f, 180f));
        title.fontStyle = FontStyles.Bold;
        CreateText(root.transform, textTemplate, "Choose Name Help",
            "This name will appear in your lobby and will be saved with your progress.", 31f,
            new Vector2(0.5f, 0.58f), new Vector2(850f, 180f));

        GameObject inputObject = new GameObject("Player Name Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(root.transform, false);
        SetRect((RectTransform)inputObject.transform, new Vector2(0.5f, 0.46f), new Vector2(760f, 115f));
        inputObject.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.18f, 0.96f);
        view.playerNameInput = inputObject.GetComponent<TMP_InputField>();
        view.playerNameInput.characterLimit = 16;
        view.playerNameInput.lineType = TMP_InputField.LineType.SingleLine;
        view.playerNameInput.contentType = TMP_InputField.ContentType.Standard;
        TMP_Text inputText = CreateText(inputObject.transform, textTemplate, "Text", string.Empty, 40f,
            new Vector2(0.5f, 0.5f), new Vector2(690f, 90f));
        inputText.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text placeholder = CreateText(inputObject.transform, textTemplate, "Placeholder", "3-16 CHARACTERS", 34f,
            new Vector2(0.5f, 0.5f), new Vector2(690f, 90f));
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.color = new Color(1f, 1f, 1f, 0.42f);
        view.playerNameInput.textViewport = (RectTransform)inputText.transform;
        view.playerNameInput.textComponent = inputText;
        view.playerNameInput.placeholder = placeholder;
        view.confirmButton = CreateButton(root.transform, playTemplate, "Confirm Player Name", "CONTINUE",
            new Vector2(0.5f, 0.34f), new Vector2(600f, 105f), new Color(1f, 0.18f, 0.57f, 1f));
        view.statusText = CreateText(root.transform, textTemplate, "Player Name Status", string.Empty, 27f,
            new Vector2(0.5f, 0.25f), new Vector2(850f, 110f));
        SavePrefab(root, "NamePanel.prefab", false);
    }

    private static void BuildLobbyPlayerName(TMP_Text textTemplate)
    {
        TMP_Text text = Object.Instantiate(textTemplate);
        text.gameObject.name = "Lobby Player Name";
        text.text = string.Empty;
        text.fontSize = 27f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = 27f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.72f, 0.12f, 1f);
        text.raycastTarget = false;
        SetRect((RectTransform)text.transform, new Vector2(0.5f, 0.94f), new Vector2(380f, 60f));
        SavePrefab(text.gameObject, "LobbyPlayerName.prefab", true);
    }

    private static void BuildQuitButton(Button template)
    {
        Button button = CreateButton(null, template, "Quit Game", "QUIT", new Vector2(0.13f, 0.065f),
            new Vector2(230f, 62f), new Color(0.08f, 0.1f, 0.18f, 0.96f));
        SavePrefab(button.gameObject, "QuitButton.prefab", true);
    }

    private static void BuildQuitPanel(TMP_Text textTemplate, Button playTemplate, Button secondaryTemplate)
    {
        GameObject root = CreatePanel("Quit Confirmation", new Color(0f, 0f, 0f, 0.82f), true);
        root.AddComponent<CanvasGroup>();
        QuitPanelView view = root.AddComponent<QuitPanelView>();
        TMP_Text title = CreateText(root.transform, textTemplate, "Quit Title", "QUIT GAME?", 58f,
            new Vector2(0.5f, 0.58f), new Vector2(820f, 130f));
        title.fontStyle = FontStyles.Bold;
        CreateText(root.transform, textTemplate, "Quit Message", "ARE YOU SURE YOU WANT TO EXIT?", 30f,
            new Vector2(0.5f, 0.49f), new Vector2(850f, 100f));
        view.confirmButton = CreateButton(root.transform, playTemplate, "Confirm Quit", "QUIT",
            new Vector2(0.35f, 0.39f), new Vector2(300f, 92f), new Color(1f, 0.18f, 0.57f, 1f));
        view.cancelButton = CreateButton(root.transform, secondaryTemplate, "Cancel Quit", "CANCEL",
            new Vector2(0.65f, 0.39f), new Vector2(300f, 92f), new Color(0.08f, 0.1f, 0.18f, 0.96f));
        SavePrefab(root, "QuitPanel.prefab", false);
    }

    private static void BuildSpecialTileFtuePanel(TMP_Text textTemplate)
    {
        GameObject root = CreatePanel("Special Block Tooltip", new Color(0f, 0f, 0f, 0.48f), true);
        Button dismiss = root.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        SpecialTileFtueView view = root.AddComponent<SpecialTileFtueView>();
        view.canvasGroup = group;
        view.dismissButton = dismiss;

        GameObject cardObject = new GameObject("Tooltip Card", typeof(RectTransform), typeof(Image));
        cardObject.transform.SetParent(root.transform, false);
        view.cardRect = (RectTransform)cardObject.transform;
        SetRect(view.cardRect, new Vector2(0.5f, 0.73f), new Vector2(900f, 330f));
        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.035f, 0.045f, 0.09f, 0.98f);
        cardImage.raycastTarget = false;
        TMP_Text badge = CreateText(cardObject.transform, textTemplate, "FTUE Badge", "UP AHEAD", 22f,
            new Vector2(0.5f, 0.84f), new Vector2(780f, 48f));
        badge.color = new Color(1f, 0.72f, 0.12f, 1f);
        view.titleText = CreateText(cardObject.transform, textTemplate, "FTUE Title", string.Empty, 42f,
            new Vector2(0.5f, 0.62f), new Vector2(840f, 80f));
        view.titleText.fontStyle = FontStyles.Bold;
        view.bodyText = CreateText(cardObject.transform, textTemplate, "FTUE Explanation", string.Empty, 27f,
            new Vector2(0.5f, 0.33f), new Vector2(820f, 110f));
        TMP_Text hint = CreateText(cardObject.transform, textTemplate, "FTUE Tap Hint", "TAP ANYWHERE TO CONTINUE", 21f,
            new Vector2(0.5f, 0.10f), new Vector2(800f, 45f));
        hint.color = new Color(1f, 0.72f, 0.12f, 1f);
        SavePrefab(root, "SpecialTileFtuePanel.prefab", false);
    }

    private static void BuildGarageEnhancements(TMP_Text textTemplate, Button chooseCarTemplate)
    {
        GameObject root = new GameObject("Garage Enhancements", typeof(RectTransform), typeof(GarageEnhancementsView));
        SetStretch((RectTransform)root.transform);
        GarageEnhancementsView view = root.GetComponent<GarageEnhancementsView>();

        GameObject preview = new GameObject("Garage Car Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        preview.transform.SetParent(root.transform, false);
        SetRect((RectTransform)preview.transform, new Vector2(0.5f, 0.755f), new Vector2(760f, 250f));
        view.previewImage = preview.GetComponent<RawImage>();
        view.previewImage.raycastTarget = false;
        view.previewImage.color = Color.white;

        view.watchAdButton = CreateButton(root.transform, chooseCarTemplate, "Watch Ad To Unlock Car", "WATCH AD TO UNLOCK",
            new Vector2(0.5f, 0.345f), new Vector2(620f, 68f), new Color(1f, 0.72f, 0.12f, 1f));
        view.watchAdButtonText = view.watchAdButton.GetComponentInChildren<TMP_Text>();
        view.speedSlider = CreateStatRow(root.transform, textTemplate, "Speed", 0.555f,
            new Color(0.05f, 0.88f, 1f), out TMP_Text speedValue);
        view.speedValueText = speedValue;
        view.handlingSlider = CreateStatRow(root.transform, textTemplate, "Handling", 0.505f,
            new Color(1f, 0.72f, 0.12f, 1f), out TMP_Text handlingValue);
        view.handlingValueText = handlingValue;
        view.ownershipText = CreateText(root.transform, textTemplate, "Car Ownership", string.Empty, 24f,
            new Vector2(0.5f, 0.458f), new Vector2(760f, 48f));
        view.ownershipText.color = Color.white;
        SavePrefab(root, "GarageEnhancements.prefab", true);
    }

    private static Slider CreateStatRow(Transform parent, TMP_Text textTemplate, string label, float anchorY,
        Color fillColour, out TMP_Text valueText)
    {
        TMP_Text labelText = CreateText(parent, textTemplate, label + " Label", label.ToUpperInvariant(), 24f,
            new Vector2(0.18f, anchorY), new Vector2(230f, 46f));
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        GameObject sliderObject = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        SetRect((RectTransform)sliderObject.transform, new Vector2(0.53f, anchorY), new Vector2(420f, 28f));
        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        SetStretch((RectTransform)backgroundObject.transform);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.16f, 0.18f, 0.26f, 1f);
        background.raycastTarget = false;
        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillArea = (RectTransform)fillAreaObject.transform;
        SetStretch(fillArea);
        fillArea.offsetMin = new Vector2(4f, 4f);
        fillArea.offsetMax = new Vector2(-4f, -4f);
        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        RectTransform fillRect = (RectTransform)fillObject.transform;
        SetStretch(fillRect);
        Image fill = fillObject.GetComponent<Image>();
        fill.color = fillColour;
        fill.raycastTarget = false;
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 80f;
        slider.maxValue = 125f;
        slider.wholeNumbers = true;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillRect;
        slider.interactable = false;
        valueText = CreateText(parent, textTemplate, label + " Value", "100", 24f,
            new Vector2(0.84f, anchorY), new Vector2(150f, 46f));
        valueText.alignment = TextAlignmentOptions.MidlineRight;
        return slider;
    }

    private static GameObject CreatePanel(string name, Color colour, bool raycastTarget = true)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        SetStretch((RectTransform)panel.transform);
        Image image = panel.GetComponent<Image>();
        image.color = colour;
        image.raycastTarget = raycastTarget;
        return panel;
    }

    private static TMP_Text CreateText(Transform parent, TMP_Text template, string objectName, string value, float size,
        Vector2 anchor, Vector2 dimensions)
    {
        TMP_Text text = Object.Instantiate(template, parent, false);
        text.gameObject.name = objectName;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = Color.white;
        text.raycastTarget = false;
        SetRect((RectTransform)text.transform, anchor, dimensions);
        return text;
    }

    private static Button CreateButton(Transform parent, Button template, string objectName, string label,
        Vector2 anchor, Vector2 dimensions, Color colour)
    {
        Button button = Object.Instantiate(template, parent, false);
        button.gameObject.name = objectName;
        button.onClick.RemoveAllListeners();
        SetRect((RectTransform)button.transform, anchor, dimensions);
        SetButtonColour(button, colour);
        TMP_Text labelText = button.GetComponentInChildren<TMP_Text>();
        if (labelText != null)
        {
            labelText.text = label;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMax = 32f;
            labelText.fontSizeMin = 20f;
        }
        return button;
    }

    private static void SetButtonColour(Button button, Color colour)
    {
        if (button.TryGetComponent(out Image image)) image.color = colour;
        ColorBlock colours = button.colors;
        colours.normalColor = colour;
        colours.highlightedColor = Color.Lerp(colour, Color.white, 0.2f);
        colours.selectedColor = colours.highlightedColor;
        colours.pressedColor = Color.Lerp(colour, Color.black, 0.2f);
        colours.disabledColor = new Color(colour.r, colour.g, colour.b, colour.a * 0.45f);
        button.colors = colours;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 dimensions)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = Vector2.zero;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SavePrefab(GameObject root, string fileName, bool active)
    {
        root.SetActive(active);
        PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/{fileName}");
        Object.DestroyImmediate(root);
    }
}
