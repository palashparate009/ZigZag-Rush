using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraColourChanger : MonoBehaviour
{
    private enum BackgroundMode { Menu, Playing, GameOver }

    private readonly struct BackgroundMood
    {
        public readonly Color Top;
        public readonly Color Middle;
        public readonly Color Bottom;
        public readonly Color Accent;

        public BackgroundMood(Color top, Color middle, Color bottom, Color accent)
        {
            Top = top;
            Middle = middle;
            Bottom = bottom;
            Accent = accent;
        }

        public static BackgroundMood Lerp(BackgroundMood a, BackgroundMood b, float amount)
        {
            return new BackgroundMood(
                Color.Lerp(a.Top, b.Top, amount),
                Color.Lerp(a.Middle, b.Middle, amount),
                Color.Lerp(a.Bottom, b.Bottom, amount),
                Color.Lerp(a.Accent, b.Accent, amount));
        }
    }

    private static readonly int FromTopColorId = Shader.PropertyToID("_FromTopColor");
    private static readonly int FromMiddleColorId = Shader.PropertyToID("_FromMiddleColor");
    private static readonly int FromBottomColorId = Shader.PropertyToID("_FromBottomColor");
    private static readonly int FromAccentColorId = Shader.PropertyToID("_FromAccentColor");
    private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
    private static readonly int MiddleColorId = Shader.PropertyToID("_MiddleColor");
    private static readonly int BottomColorId = Shader.PropertyToID("_BottomColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int PulseColorId = Shader.PropertyToID("_PulseColor");
    private static readonly int MotionSpeedId = Shader.PropertyToID("_MotionSpeed");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int TransitionProgressId = Shader.PropertyToID("_TransitionProgress");
    private static readonly int TransitionStyleId = Shader.PropertyToID("_TransitionStyle");
    private static readonly int TransitionSoftnessId = Shader.PropertyToID("_TransitionSoftness");
    private static readonly int TransitionCrossFadeId = Shader.PropertyToID("_TransitionCrossFade");
    private static readonly int BackgroundTimeId = Shader.PropertyToID("_BackgroundTime");

    private Camera targetCamera;
    private Skybox targetSkybox;
    private Material backgroundMaterial;
    private Material originalSkyboxMaterial;
    private CameraClearFlags originalClearFlags;
    private BackgroundConfig settings;
    private BackgroundMood[][] themePalettes;
    private BackgroundMood fromMood;
    private BackgroundMood targetMood;
    private Color pulseColor = Color.white;
    private int selectedTheme;
    private int lastMoodIndex;
    private int lastTransitionStyle = -1;
    private float menuTimer;
    private float gameplayTimer;
    private float animationTime;
    private float transitionProgress = 1f;
    private float pulse;
    private float lastDifficulty;
    private int lastCombo;
    private BackgroundMode mode;
    private bool backgroundReady;
    private bool capturedOriginalState;
    private bool createdSkybox;

    private void Awake()
    {
        EnsureBackground();
        EnsureConfiguration();
        SetTheme(GameProgress.SelectedTheme);
        SetMenuMode();
    }

    private void OnEnable()
    {
        ConfigManager.ConfigChanged += HandleConfigChanged;
        EnsureBackground();
        ActivateBackground();
    }

    private void Update()
    {
        if (!backgroundReady) return;
        float delta = Time.unscaledDeltaTime;
        animationTime += delta;

        if (mode == BackgroundMode.Menu)
        {
            menuTimer += delta;
            if (menuTimer >= Mathf.Max(1f, settings.menuMoodInterval))
            {
                menuTimer = 0f;
                StartNextPaletteTransition();
            }
        }
        else if (mode == BackgroundMode.Playing)
        {
            gameplayTimer += delta;
            if (gameplayTimer >= Mathf.Max(1f, settings.gameplayColorInterval))
            {
                gameplayTimer = 0f;
                StartNextPaletteTransition();
            }
        }

        if (transitionProgress < 1f)
            transitionProgress = Mathf.MoveTowards(transitionProgress, 1f,
                delta / Mathf.Max(0.05f, settings.transitionDuration));
        pulse = Mathf.MoveTowards(pulse, 0f, delta * Mathf.Max(0.01f, settings.pulseFadeSpeed));
        ApplyMaterial();
    }

    public void SetTheme(int theme)
    {
        EnsureBackground();
        EnsureConfiguration();
        selectedTheme = Mathf.Clamp(theme, 0, themePalettes.Length - 1);
        lastMoodIndex = 0;
        fromMood = themePalettes[selectedTheme][0];
        targetMood = fromMood;
        pulseColor = targetMood.Accent;
        transitionProgress = 1f;
        ApplyMaterial();
    }

    public void SetMenuMode()
    {
        mode = BackgroundMode.Menu;
        menuTimer = 0f;
        gameplayTimer = 0f;
    }

    public void SetGameplayState(float difficulty01, int combo)
    {
        bool startingRun = mode != BackgroundMode.Playing;
        mode = BackgroundMode.Playing;
        lastDifficulty = Mathf.Clamp01(difficulty01);
        lastCombo = combo;
        if (startingRun)
        {
            gameplayTimer = 0f;
            StartNextPaletteTransition();
        }

        if (combo > 1)
        {
            float comboStrength = Mathf.Clamp01(combo / (float)Mathf.Max(1, settings.comboForMaximumPulse));
            pulseColor = Color.Lerp(targetMood.Accent, Color.white, comboStrength * 0.3f);
            pulse = Mathf.Max(pulse, 0.1f + comboStrength * 0.35f);
        }
    }

    public void PulseSpecial(SpecialTileType type)
    {
        switch (type)
        {
            case SpecialTileType.SpeedBoost: pulseColor = Parse(settings.speedBoostAccent); break;
            case SpecialTileType.SlowMotion: pulseColor = Parse(settings.slowMotionAccent); break;
            case SpecialTileType.Shield: pulseColor = Parse(settings.shieldAccent); break;
            case SpecialTileType.GemMagnet: pulseColor = Parse(settings.gemMagnetAccent); break;
            default: pulseColor = Parse(settings.mysteryAccent); break;
        }
        pulse = 1f;
    }

    public void SetGameOverMood()
    {
        mode = BackgroundMode.GameOver;
        menuTimer = 0f;
        gameplayTimer = 0f;
        BackgroundMood visibleMood = CurrentBlendedMood();
        BeginTransition(new BackgroundMood(
            visibleMood.Top * 0.55f,
            Color.Lerp(visibleMood.Middle * 0.5f, new Color(0.28f, 0.02f, 0.05f), 0.35f),
            visibleMood.Bottom * 0.42f,
            Parse(settings.gameOverAccent)));
        pulseColor = Parse(settings.gameOverAccent);
        pulse = 0.7f;
    }

    private void StartNextPaletteTransition()
    {
        BackgroundMood[] palette = themePalettes[selectedTheme];
        if (palette.Length == 1)
        {
            BeginTransition(palette[0]);
            return;
        }

        int nextIndex;
        int preferred = Mathf.Clamp(Mathf.RoundToInt(lastDifficulty * (palette.Length - 1)), 0, palette.Length - 1);
        if (preferred != lastMoodIndex && Random.value < 0.4f)
        {
            nextIndex = preferred;
        }
        else
        {
            nextIndex = Random.Range(0, palette.Length - 1);
            if (nextIndex >= lastMoodIndex) nextIndex++;
        }

        lastMoodIndex = nextIndex;
        BeginTransition(palette[nextIndex]);
    }

    private void BeginTransition(BackgroundMood nextMood)
    {
        fromMood = CurrentBlendedMood();
        targetMood = nextMood;
        transitionProgress = 0f;
        lastTransitionStyle = ChooseTransitionStyle();
        pulseColor = targetMood.Accent;
        ApplyMaterial();
    }

    private int ChooseTransitionStyle()
    {
        BackgroundTransitionOption[] options = settings.transitionAnimations;
        int selected = -1;
        int fallbackEnabled = -1;
        int eligibleCount = 0;
        if (options != null)
        {
            for (int i = 0; i < options.Length; i++)
            {
                BackgroundTransitionOption option = options[i];
                if (option == null || !option.enabled || option.style < 0 || option.style > 19) continue;
                if (fallbackEnabled < 0) fallbackEnabled = option.style;
                if (option.style == lastTransitionStyle) continue;
                eligibleCount++;
                if (Random.Range(0, eligibleCount) == 0) selected = option.style;
            }
        }

        if (selected >= 0) return selected;
        return fallbackEnabled >= 0 ? fallbackEnabled : 0;
    }

    private BackgroundMood CurrentBlendedMood()
    {
        float eased = transitionProgress * transitionProgress * transitionProgress *
            (transitionProgress * (transitionProgress * 6f - 15f) + 10f);
        return BackgroundMood.Lerp(fromMood, targetMood, eased);
    }

    private void EnsureConfiguration()
    {
        if (settings != null && themePalettes != null && themePalettes.Length > 0) return;
        settings = ConfigManager.Current.background ?? new BackgroundConfig();
        BackgroundThemeConfig[] configuredThemes = settings.themes;
        if (configuredThemes == null || configuredThemes.Length == 0)
            configuredThemes = new BackgroundConfig().themes;

        themePalettes = new BackgroundMood[configuredThemes.Length][];
        for (int i = 0; i < configuredThemes.Length; i++)
            themePalettes[i] = BuildPalette(configuredThemes[i]);
    }

    private static BackgroundMood[] BuildPalette(BackgroundThemeConfig theme)
    {
        if (theme == null) return new[] { Mood("17113f", "5a287d", "210d38", "ffb33c") };
        int count = Mathf.Min(Count(theme.topColors), Count(theme.middleColors),
            Count(theme.bottomColors), Count(theme.accentColors));
        if (count <= 0) return new[] { Mood("17113f", "5a287d", "210d38", "ffb33c") };

        BackgroundMood[] palette = new BackgroundMood[count];
        for (int i = 0; i < count; i++)
            palette[i] = Mood(theme.topColors[i], theme.middleColors[i], theme.bottomColors[i], theme.accentColors[i]);
        return palette;
    }

    private void EnsureBackground()
    {
        if (backgroundReady) return;
        targetCamera = GetComponent<Camera>();
        Shader shader = Resources.Load<Shader>("Shaders/ZigZagAnimatedBackground");
        if (shader == null) shader = Shader.Find("ZigZag/Animated Background");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("Animated background shader is unavailable; using the camera's solid background colour.", this);
            return;
        }

        targetSkybox = GetComponent<Skybox>();
        if (targetSkybox == null)
        {
            targetSkybox = gameObject.AddComponent<Skybox>();
            createdSkybox = true;
        }

        if (!capturedOriginalState)
        {
            originalClearFlags = targetCamera.clearFlags;
            originalSkyboxMaterial = targetSkybox.material;
            capturedOriginalState = true;
        }

        backgroundMaterial = new Material(shader)
        {
            name = "ZigZag Animated Background (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        backgroundReady = true;
    }

    private void ActivateBackground()
    {
        if (!backgroundReady || targetCamera == null || targetSkybox == null) return;
        targetCamera.clearFlags = CameraClearFlags.Skybox;
        targetSkybox.material = backgroundMaterial;
    }

    private void ApplyMaterial()
    {
        if (!backgroundReady || backgroundMaterial == null || settings == null) return;
        ActivateBackground();
        backgroundMaterial.SetColor(FromTopColorId, fromMood.Top);
        backgroundMaterial.SetColor(FromMiddleColorId, fromMood.Middle);
        backgroundMaterial.SetColor(FromBottomColorId, fromMood.Bottom);
        backgroundMaterial.SetColor(FromAccentColorId, fromMood.Accent);
        backgroundMaterial.SetColor(TopColorId, targetMood.Top);
        backgroundMaterial.SetColor(MiddleColorId, targetMood.Middle);
        backgroundMaterial.SetColor(BottomColorId, targetMood.Bottom);
        backgroundMaterial.SetColor(AccentColorId, targetMood.Accent);
        backgroundMaterial.SetColor(PulseColorId, pulseColor);
        backgroundMaterial.SetFloat(MotionSpeedId, Mathf.Clamp(settings.motionSpeed, 0f, 2f));
        backgroundMaterial.SetFloat(IntensityId, Mathf.Clamp01(settings.effectIntensity));
        backgroundMaterial.SetFloat(PulseId, pulse);
        backgroundMaterial.SetFloat(TransitionProgressId, transitionProgress);
        backgroundMaterial.SetFloat(TransitionStyleId, lastTransitionStyle < 0 ? 0 : lastTransitionStyle);
        backgroundMaterial.SetFloat(TransitionSoftnessId, Mathf.Clamp(settings.transitionSoftness, 0.01f, 0.25f));
        backgroundMaterial.SetFloat(TransitionCrossFadeId, Mathf.Clamp(settings.transitionCrossFade, 0f, 0.5f));
        backgroundMaterial.SetFloat(BackgroundTimeId, animationTime);
        targetCamera.backgroundColor = CurrentBlendedMood().Bottom;
    }

    private void HandleConfigChanged()
    {
        BackgroundMode previousMode = mode;
        int previousTheme = selectedTheme;
        settings = null;
        themePalettes = null;
        EnsureConfiguration();
        SetTheme(previousTheme);
        switch (previousMode)
        {
            case BackgroundMode.Playing: SetGameplayState(lastDifficulty, lastCombo); break;
            case BackgroundMode.GameOver: SetGameOverMood(); break;
            default: SetMenuMode(); break;
        }
    }

    private void OnDisable()
    {
        ConfigManager.ConfigChanged -= HandleConfigChanged;
        if (!capturedOriginalState || targetCamera == null || targetSkybox == null) return;
        targetCamera.clearFlags = originalClearFlags;
        targetSkybox.material = originalSkyboxMaterial;
    }

    private void OnDestroy()
    {
        if (backgroundMaterial != null)
        {
            if (Application.isPlaying) Destroy(backgroundMaterial);
            else DestroyImmediate(backgroundMaterial);
        }
        if (createdSkybox && targetSkybox != null && !Application.isPlaying)
            DestroyImmediate(targetSkybox);
    }

    private static int Count(string[] values) => values == null ? 0 : values.Length;

    private static BackgroundMood Mood(string top, string middle, string bottom, string accent)
    {
        return new BackgroundMood(Parse(top), Parse(middle), Parse(bottom), Parse(accent));
    }

    private static Color Parse(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.black;
        string html = hex[0] == '#' ? hex : "#" + hex;
        return ColorUtility.TryParseHtmlString(html, out Color colour) ? colour : Color.black;
    }
}
