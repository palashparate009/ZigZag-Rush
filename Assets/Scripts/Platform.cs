using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

public enum SpecialTileType
{
    Normal,
    SpeedBoost,
    SlowMotion,
    Shield,
    GemMagnet,
    Crumbling,
    Moving,
    Mystery
}

[RequireComponent(typeof(Rigidbody))]
public class Platform : MonoBehaviour
{
    public GameObject diamond;

    private PlatformSpawner owner;
    private Rigidbody body;
    private Renderer tileRenderer;
    private MaterialPropertyBlock propertyBlock;
    private GameObject diamondInstance;
    private SpecialTileType tileType;
    private Vector3 basePosition;
    private bool activated;
    private bool falling;
    private float movementOffset;
    private bool ftueTooltipShown;
    private MeshRenderer tileIconRenderer;
    private TextMeshPro tileLabel;
    private Transform specialTileMarkerRoot;
    private Transform tileIconAnchor;
    private Transform tileLabelAnchor;
    private int routeChoiceMarker;
    private Sequence crumbleVisualSequence;
    private Color configuredTileColor = Color.white;

    private const int IconTextureSize = 32;
    private static readonly Material[] TileIconMaterials = new Material[8];
    private static Mesh tileIconMesh;
    private static GameObject specialTileMarkerPrefab;

    private void Awake()
    {
        CacheRuntimeReferences();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void Start()
    {
        // The hand-placed starting platform is not managed by the pool.
        if (owner == null) ConfigureVisual(GameProgress.SelectedTheme);
    }

    private void FixedUpdate()
    {
        if (tileType != SpecialTileType.Moving || falling) return;
        GameplayConfig config = ConfigManager.Current.gameplay;
        float routeClock = owner != null ? owner.RouteClock : Time.fixedTime;
        float y = Mathf.Sin((routeClock + movementOffset) * config.movingTileSpeed) *
                  config.movingTileAmplitude;
        body.MovePosition(basePosition + Vector3.up * y);
    }

    public void Initialize(PlatformSpawner platformOwner, Vector3 position, SpecialTileType type, int theme, bool spawnDiamond,
        int choiceMarker = 0, float deterministicMovementOffset = -1f)
    {
        // Pool entries can be instantiated inactive. In that case Unity defers
        // Awake until after the spawner calls Initialize, so refresh these
        // references here before touching the rigidbody or property block.
        CacheRuntimeReferences();
        owner = platformOwner;
        tileType = type;
        basePosition = position;
        movementOffset = deterministicMovementOffset >= 0f
            ? deterministicMovementOffset
            : Random.Range(0f, 10f);
        activated = false;
        falling = false;
        ftueTooltipShown = false;
        routeChoiceMarker = choiceMarker;
        CancelInvoke();
        StopAllCoroutines();
        ResetCrumbleVisual();

        transform.SetPositionAndRotation(position, Quaternion.identity);
        body.isKinematic = true;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        ConfigureVisual(theme);
        ConfigureTileIcon(type);
        ConfigureTileLabel(type);
        ResetCrumbleVisual();
        enabled = type != SpecialTileType.Normal;

        if (spawnDiamond && diamond != null)
        {
            if (diamondInstance == null)
                diamondInstance = Instantiate(diamond, transform);
            // A collected gem temporarily detaches to play its pickup tween.
            // Reparent it here before returning this pooled tile to the road.
            diamondInstance.transform.SetParent(transform, false);
            diamondInstance.transform.SetLocalPositionAndRotation(Vector3.up, diamond.transform.rotation);
            if (diamondInstance.GetComponent<GemMagnetPickup>() == null)
                diamondInstance.AddComponent<GemMagnetPickup>();
            diamondInstance.SetActive(true);
        }
        else if (diamondInstance != null) diamondInstance.SetActive(false);
    }

    private void CacheRuntimeReferences()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        // The prefab is required to have a Rigidbody, but keep pooled tiles
        // recoverable if an old prefab instance is missing one.
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        if (tileRenderer == null) tileRenderer = GetComponent<Renderer>();
        propertyBlock ??= new MaterialPropertyBlock();
    }

    public void ApplyTheme(int theme)
    {
        ConfigureVisual(theme);
    }

    private void Update()
    {
        if (ftueTooltipShown || falling || tileType == SpecialTileType.Normal || GameManager.instance == null) return;
        if (GameProgress.HasSeenSpecialTileFtue(tileType))
        {
            ftueTooltipShown = true;
            return;
        }
        if (GameManager.instance.TryShowSpecialTileTooltip(tileType, transform.position))
            ftueTooltipShown = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (activated) return;
        activated = true;

        ActivateTile();
    }

    private void ActivateTile()
    {
        if (!gameObject.activeInHierarchy || falling) return;
        if (routeChoiceMarker != 0)
        {
            GameAnalytics.RouteBranchChosen(routeChoiceMarker > 0);
            routeChoiceMarker = 0;
        }
        if (tileType != SpecialTileType.Normal && GameManager.instance != null)
            GameManager.instance.ApplySpecialTile(tileType, transform.position);
        if (tileType == SpecialTileType.Crumbling)
        {
            float crumbleDelay = Mathf.Max(0.1f, ConfigManager.Current.gameplay.crumblingTileDelay);
            PlayCrumbleAnimation(crumbleDelay);
            CancelInvoke(nameof(Fall));
            Invoke(nameof(Fall), crumbleDelay);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player") || falling) return;

        // A crumbling tile begins its 1.5-second warning on contact. Leaving
        // it must not replace that timer with the old near-instant exit delay;
        // otherwise the tween is killed before the player can see it.
        if (tileType == SpecialTileType.Crumbling) return;

        Invoke(nameof(Fall), ConfigManager.Current.gameplay.normalTileFallDelay);
    }

    private void Fall()
    {
        if (falling) return;

        // The crumble tween may still be offsetting this Transform when the
        // player leaves the block. Restore the kinematic body first, then let
        // physics take over from the real tile position.
        if (tileType == SpecialTileType.Crumbling)
        {
            crumbleVisualSequence?.Kill();
            crumbleVisualSequence = null;
            transform.position = basePosition;
            body.position = basePosition;
        }

        falling = true;
        body.isKinematic = false;
        StartCoroutine(ReturnAfterDelay());
    }

    private void PlayCrumbleAnimation(float duration)
    {
        if (tileRenderer == null) return;

        crumbleVisualSequence?.Kill();
        float safeDuration = Mathf.Max(0.08f, duration);
        Color crumbledColour = Color.Lerp(configuredTileColor, Color.black, 0.58f);

        // Shake the complete block while it is kinematic. Fall() restores its
        // exact original position immediately before physics is enabled.
        crumbleVisualSequence = DOTween.Sequence().SetTarget(this);
        crumbleVisualSequence.Join(transform.DOShakePosition(
            safeDuration,
            new Vector3(0.075f, 0.015f, 0.075f),
            18,
            90f,
            false,
            true));
        crumbleVisualSequence.Join(DOTween.To(
            () => configuredTileColor,
            SetTileColour,
            crumbledColour,
            safeDuration).SetEase(Ease.InQuad));

        if (tileIconRenderer != null)
        {
            Transform iconTransform = tileIconRenderer.transform;
            crumbleVisualSequence.Join(iconTransform.DOPunchScale(Vector3.one * 0.28f, safeDuration, 4, 0.65f));
            crumbleVisualSequence.Join(iconTransform.DOShakePosition(safeDuration, 0.045f, 12, 90f, false, true));
        }
    }

    private void ResetCrumbleVisual()
    {
        crumbleVisualSequence?.Kill();
        crumbleVisualSequence = null;
        if (tileRenderer != null) SetTileColour(configuredTileColor);
        if (tileIconRenderer != null)
        {
            tileIconRenderer.transform.localPosition = Vector3.zero;
            tileIconRenderer.transform.localScale = Vector3.one;
        }
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(ConfigManager.Current.gameplay.platformRecycleDelay);
        if (owner != null) owner.Release(this);
        else Destroy(gameObject);
    }

    private void ConfigureVisual(int theme)
    {
        if (tileRenderer == null) return;
        configuredTileColor = GetConfiguredColour(tileType, theme);
        SetTileColour(configuredTileColor);
    }

    private void SetTileColour(Color colour)
    {
        if (tileRenderer == null) return;
        tileRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", colour);
        propertyBlock.SetColor("_BaseColor", colour);
        tileRenderer.SetPropertyBlock(propertyBlock);
    }

    public static Color GetConfiguredColour(SpecialTileType type, int theme = 0)
    {
        SpecialTileVisualConfig visuals = ConfigManager.Current.gameplay.specialTileVisuals;
        if (visuals == null) visuals = new SpecialTileVisualConfig();
        return visuals.ResolveTileColor((int)type, theme);
    }

    private void ConfigureTileIcon(SpecialTileType type)
    {
        if (type == SpecialTileType.Normal)
        {
            if (tileIconRenderer != null) tileIconRenderer.gameObject.SetActive(false);
            return;
        }

        if (tileIconRenderer == null)
        {
            EnsureSpecialTileMarker();
            GameObject marker = new GameObject("Special Tile Icon");
            marker.layer = gameObject.layer;
            marker.transform.SetParent(tileIconAnchor, false);

            MeshFilter filter = marker.AddComponent<MeshFilter>();
            filter.sharedMesh = GetTileIconMesh();
            tileIconRenderer = marker.AddComponent<MeshRenderer>();
            tileIconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tileIconRenderer.receiveShadows = false;
            tileIconRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            tileIconRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            tileIconRenderer.sortingOrder = 1;
        }

        EnsureSpecialTileMarker();
        tileIconRenderer.transform.SetParent(tileIconAnchor, false);
        tileIconRenderer.sharedMaterial = GetTileIconMaterial(type);
        tileIconRenderer.transform.localPosition = Vector3.zero;
        tileIconRenderer.transform.localScale = Vector3.one;
        tileIconRenderer.gameObject.SetActive(true);
    }

    private void ConfigureTileLabel(SpecialTileType type)
    {
        if (type == SpecialTileType.Normal)
        {
            if (tileLabel != null) tileLabel.gameObject.SetActive(false);
            return;
        }

        if (tileLabel == null)
        {
            EnsureSpecialTileMarker();
            tileLabel = tileLabelAnchor.GetComponentInChildren<TextMeshPro>(true);
        }

        // Old scene/platform instances may not yet contain the shared marker
        // label. Keep a fallback for those, but the prefab TMP label is the
        // normal path and owns all font settings in the Inspector.
        if (tileLabel == null)
        {
            GameObject label = new GameObject("Special Tile Label");
            label.layer = gameObject.layer;
            label.transform.SetParent(tileLabelAnchor, false);
            label.transform.localPosition = Vector3.zero;
            label.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            label.transform.localScale = Vector3.one * 0.28f;

            tileLabel = label.AddComponent<TextMeshPro>();
            tileLabel.font = TMP_Settings.defaultFontAsset ??
                Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            tileLabel.alignment = TextAlignmentOptions.Center;
            tileLabel.textWrappingMode = TextWrappingModes.NoWrap;
            tileLabel.overflowMode = TextOverflowModes.Overflow;
            tileLabel.fontSize = 4.2f;
            tileLabel.color = Color.white;
            tileLabel.outlineWidth = 0.18f;
            tileLabel.outlineColor = new Color(0.02f, 0.03f, 0.08f, 1f);
            tileLabel.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tileLabel.renderer.receiveShadows = false;
        }

        EnsureSpecialTileMarker();
        tileLabel.transform.SetParent(tileLabelAnchor, false);
        tileLabel.transform.localPosition = Vector3.zero;
        tileLabel.text = GetTileLabel(type);
        tileLabel.gameObject.SetActive(true);
    }

    private void EnsureSpecialTileMarker()
    {
        if (specialTileMarkerRoot != null) return;

        specialTileMarkerPrefab ??= Resources.Load<GameObject>("SpecialTileMarker");
        GameObject marker = specialTileMarkerPrefab != null
            ? Instantiate(specialTileMarkerPrefab, transform, false)
            : new GameObject("Special Tile Marker");
        if (marker.transform.parent != transform) marker.transform.SetParent(transform, false);
        marker.layer = gameObject.layer;
        specialTileMarkerRoot = marker.transform;
        tileIconAnchor = specialTileMarkerRoot.Find("Icon Anchor");
        tileLabelAnchor = specialTileMarkerRoot.Find("Label Anchor");

        if (tileIconAnchor == null)
            tileIconAnchor = CreateMarkerAnchor("Icon Anchor", Vector3.zero);
        if (tileLabelAnchor == null)
            tileLabelAnchor = CreateMarkerAnchor("Label Anchor", new Vector3(-0.35f, 0.515f, 0.35f));
    }

    private Transform CreateMarkerAnchor(string anchorName, Vector3 localPosition)
    {
        GameObject anchor = new GameObject(anchorName);
        anchor.layer = gameObject.layer;
        anchor.transform.SetParent(specialTileMarkerRoot, false);
        anchor.transform.localPosition = localPosition;
        return anchor.transform;
    }

    private static string GetTileLabel(SpecialTileType type)
    {
        SpecialTileVisualConfig visuals = ConfigManager.Current.gameplay.specialTileVisuals;
        return visuals != null ? visuals.ResolveTileLabel((int)type) : string.Empty;
    }

    private static Mesh GetTileIconMesh()
    {
        if (tileIconMesh != null) return tileIconMesh;

        const float extent = 0.36f;
        const float top = 0.506f;
        tileIconMesh = new Mesh { name = "Shared Special Tile Icon Mesh" };
        tileIconMesh.vertices = new[]
        {
            new Vector3(-extent, top, -extent),
            new Vector3(-extent, top, extent),
            new Vector3(extent, top, extent),
            new Vector3(extent, top, -extent)
        };
        tileIconMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        tileIconMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        tileIconMesh.RecalculateNormals();
        tileIconMesh.RecalculateBounds();
        return tileIconMesh;
    }

    private static Material GetTileIconMaterial(SpecialTileType type)
    {
        int index = (int)type;
        if (TileIconMaterials[index] != null) return TileIconMaterials[index];

        Shader shader = Shader.Find("ZigZag/SpecialTileIcon");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        Material material = new Material(shader)
        {
            name = $"Special Tile Icon - {type}",
            mainTexture = CreateIconTexture(type),
            color = Color.white
        };
        TileIconMaterials[index] = material;
        return material;
    }

    private static Texture2D CreateIconTexture(SpecialTileType type)
    {
        Color32[] pixels = new Color32[IconTextureSize * IconTextureSize];
        Color32 outline = new Color32(5, 8, 18, 255);
        Color32 fill = new Color32(255, 255, 255, 255);

        DrawIcon(type, pixels, outline, 2);
        DrawIcon(type, pixels, fill, 0);

        Texture2D texture = new Texture2D(IconTextureSize, IconTextureSize, TextureFormat.RGBA32, false, true)
        {
            name = $"Special Tile Icon - {type}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static void DrawIcon(SpecialTileType type, Color32[] pixels, Color32 colour, int radius)
    {
        switch (type)
        {
            case SpecialTileType.SpeedBoost:
                DrawPolyline(pixels, colour, radius, new[]
                {
                    new Vector2Int(20, 3), new Vector2Int(9, 16), new Vector2Int(16, 16),
                    new Vector2Int(11, 29), new Vector2Int(25, 12), new Vector2Int(18, 12),
                    new Vector2Int(20, 3)
                });
                break;

            case SpecialTileType.SlowMotion:
                DrawCircle(pixels, 16, 16, 11, colour, radius);
                DrawLine(pixels, 16, 16, 16, 8, colour, radius);
                DrawLine(pixels, 16, 16, 23, 19, colour, radius);
                break;

            case SpecialTileType.Shield:
                DrawPolyline(pixels, colour, radius, new[]
                {
                    new Vector2Int(16, 3), new Vector2Int(26, 7), new Vector2Int(24, 19),
                    new Vector2Int(16, 29), new Vector2Int(8, 19), new Vector2Int(6, 7),
                    new Vector2Int(16, 3)
                });
                DrawLine(pixels, 16, 7, 16, 24, colour, radius);
                break;

            case SpecialTileType.GemMagnet:
                DrawLine(pixels, 7, 6, 7, 18, colour, radius);
                DrawLine(pixels, 25, 6, 25, 18, colour, radius);
                for (int angle = 180; angle <= 360; angle += 10)
                {
                    float radians = angle * Mathf.Deg2Rad;
                    int x = Mathf.RoundToInt(16f + Mathf.Cos(radians) * 9f);
                    int y = Mathf.RoundToInt(18f - Mathf.Sin(radians) * 9f);
                    PutDisc(pixels, x, y, radius, colour);
                }
                DrawLine(pixels, 4, 6, 10, 6, colour, radius);
                DrawLine(pixels, 22, 6, 28, 6, colour, radius);
                break;

            case SpecialTileType.Crumbling:
                DrawPolyline(pixels, colour, radius, new[]
                {
                    new Vector2Int(18, 2), new Vector2Int(13, 10), new Vector2Int(18, 15),
                    new Vector2Int(11, 22), new Vector2Int(14, 30)
                });
                DrawLine(pixels, 13, 10, 5, 8, colour, radius);
                DrawLine(pixels, 18, 15, 27, 12, colour, radius);
                DrawLine(pixels, 11, 22, 4, 26, colour, radius);
                break;

            case SpecialTileType.Moving:
                DrawLine(pixels, 4, 16, 28, 16, colour, radius);
                DrawLine(pixels, 4, 16, 10, 10, colour, radius);
                DrawLine(pixels, 4, 16, 10, 22, colour, radius);
                DrawLine(pixels, 28, 16, 22, 10, colour, radius);
                DrawLine(pixels, 28, 16, 22, 22, colour, radius);
                break;

            case SpecialTileType.Mystery:
                DrawPolyline(pixels, colour, radius, new[]
                {
                    new Vector2Int(9, 9), new Vector2Int(12, 5), new Vector2Int(18, 4),
                    new Vector2Int(24, 8), new Vector2Int(23, 14), new Vector2Int(16, 18),
                    new Vector2Int(16, 22)
                });
                PutDisc(pixels, 16, 28, radius + 1, colour);
                break;
        }
    }

    private static void DrawPolyline(Color32[] pixels, Color32 colour, int radius, Vector2Int[] points)
    {
        for (int i = 1; i < points.Length; i++)
            DrawLine(pixels, points[i - 1].x, points[i - 1].y, points[i].x, points[i].y, colour, radius);
    }

    private static void DrawCircle(Color32[] pixels, int centerX, int centerY, int circleRadius, Color32 colour, int radius)
    {
        Vector2Int previous = new Vector2Int(centerX + circleRadius, centerY);
        for (int angle = 10; angle <= 360; angle += 10)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2Int current = new Vector2Int(
                Mathf.RoundToInt(centerX + Mathf.Cos(radians) * circleRadius),
                Mathf.RoundToInt(centerY + Mathf.Sin(radians) * circleRadius));
            DrawLine(pixels, previous.x, previous.y, current.x, current.y, colour, radius);
            previous = current;
        }
    }

    private static void DrawLine(Color32[] pixels, int x0, int y0, int x1, int y1, Color32 colour, int radius)
    {
        int deltaX = Mathf.Abs(x1 - x0);
        int stepX = x0 < x1 ? 1 : -1;
        int deltaY = -Mathf.Abs(y1 - y0);
        int stepY = y0 < y1 ? 1 : -1;
        int error = deltaX + deltaY;

        while (true)
        {
            PutDisc(pixels, x0, y0, radius, colour);
            if (x0 == x1 && y0 == y1) break;
            int twiceError = error * 2;
            if (twiceError >= deltaY)
            {
                error += deltaY;
                x0 += stepX;
            }
            if (twiceError <= deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
    }

    private static void PutDisc(Color32[] pixels, int centerX, int centerY, int radius, Color32 colour)
    {
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            if (y < 0 || y >= IconTextureSize) continue;
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || x >= IconTextureSize) continue;
                int offsetX = x - centerX;
                int offsetY = y - centerY;
                if (offsetX * offsetX + offsetY * offsetY > radius * radius + radius) continue;
                pixels[y * IconTextureSize + x] = colour;
            }
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
        StopAllCoroutines();
        if (diamondInstance != null) diamondInstance.SetActive(false);
    }
}

/// <summary>
/// Gives spawned gems a safe, visible fly-to-car sequence when a magnet block
/// collects them. It is added to the runtime gem instance by Platform.
/// </summary>
public sealed class GemMagnetPickup : MonoBehaviour
{
    private Collider[] gemColliders;
    private AnimationScript idleAnimation;
    private Vector3 originalLocalScale;
    private Quaternion originalLocalRotation;
    private Tween pullTween;
    private bool magnetized;

    private void Awake()
    {
        originalLocalScale = transform.localScale;
        originalLocalRotation = transform.localRotation;
        gemColliders = GetComponentsInChildren<Collider>(true);
        idleAnimation = GetComponent<AnimationScript>();
    }

    private void OnEnable()
    {
        magnetized = false;
        pullTween?.Kill();
        pullTween = null;
        transform.localScale = originalLocalScale;
        transform.localRotation = originalLocalRotation;
        if (idleAnimation != null) idleAnimation.enabled = true;
        SetGemCollidersEnabled(true);
    }

    private void OnDisable()
    {
        pullTween?.Kill();
        pullTween = null;
    }

    public bool PullTo(Transform target)
    {
        if (target == null) return false;
        return BeginCollection(target, 0.18f + Vector3.Distance(transform.position, target.position) * 0.035f, 0.14f, 180f);
    }

    public bool CollectAtCar(Transform target)
    {
        RuntimeGameUI ui = FindFirstObjectByType<RuntimeGameUI>();
        Camera camera = Camera.main;
        if (ui != null && camera != null && ui.TryGetGemHudScreenPosition(out Vector2 hudPosition))
            return BeginHudCollection(camera, hudPosition);

        // Safe fallback for a scene where the HUD is not currently available.
        return BeginCollection(target, 0.28f, 0.30f, 180f);
    }

    private bool BeginHudCollection(Camera camera, Vector2 hudPosition)
    {
        if (magnetized || camera == null || !gameObject.activeInHierarchy) return false;

        Vector3 startScreenPosition = camera.WorldToScreenPoint(transform.position);
        if (startScreenPosition.z <= 0f) return false;

        magnetized = true;
        SetGemCollidersEnabled(false);
        if (idleAnimation != null) idleAnimation.enabled = false;

        transform.SetParent(null, true);
        Vector3 targetScreenPosition = new Vector3(hudPosition.x, hudPosition.y, startScreenPosition.z);
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;

        // Use screen-space coordinates so the gem visibly lands on the HUD,
        // regardless of the camera following the moving car underneath it.
        pullTween = DOTween.To(
                () => 0f,
                progress =>
                {
                    float eased = DOVirtual.EasedValue(0f, 1f, progress, Ease.InOutCubic);
                    Vector3 screenPosition = Vector3.LerpUnclamped(startScreenPosition, targetScreenPosition, eased);
                    screenPosition.y += Mathf.Sin(progress * Mathf.PI) * 48f;
                    transform.position = camera.ScreenToWorldPoint(screenPosition);
                    transform.rotation = startRotation * Quaternion.Euler(0f, 180f * progress, 0f);

                    float pop = 1f + Mathf.Sin(progress * Mathf.PI) * 0.12f;
                    float shrink = Mathf.Lerp(1f, 0.06f, Mathf.InverseLerp(0.58f, 1f, progress));
                    transform.localScale = startScale * pop * shrink;
                },
                1f,
                0.42f)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(CompleteCollection);

        return true;
    }

    private bool BeginCollection(Transform target, float duration, float arcHeight, float spinDegrees)
    {
        if (magnetized || target == null || !gameObject.activeInHierarchy) return false;

        magnetized = true;
        SetGemCollidersEnabled(false); // Prevent normal trigger collection mid-flight.
        if (idleAnimation != null) idleAnimation.enabled = false;

        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;
        duration = Mathf.Clamp(duration, 0.16f, 0.55f);

        // The platform drops shortly after the car leaves it. Detaching keeps
        // the pickup tween smooth instead of making the gem fall with the tile.
        transform.SetParent(null, true);

        // The target is read on every tween update, so the gem follows the car
        // even while it is continuing along the road.
        pullTween = DOTween.To(
                () => 0f,
                progress =>
                {
                    if (target == null) return;
                    float eased = DOVirtual.EasedValue(0f, 1f, progress, Ease.InOutCubic);
                    Vector3 targetPosition = target.position + Vector3.up * 0.25f;
                    float arc = Mathf.Sin(progress * Mathf.PI) * arcHeight;
                    transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased) + Vector3.up * arc;
                    transform.rotation = startRotation * Quaternion.Euler(0f, spinDegrees * progress, 0f);
                    float grow = 1f + Mathf.Sin(progress * Mathf.PI) * 0.14f;
                    float shrink = Mathf.Lerp(1f, 0.08f, Mathf.InverseLerp(0.55f, 1f, progress));
                    float popScale = grow * shrink;
                    transform.localScale = startScale * popScale;
                },
                1f,
                duration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(CompleteCollection);

        return true;
    }

    private void CompleteCollection()
    {
        pullTween = null;
        if (!gameObject.activeInHierarchy) return;

        // Restore the pooled instance before disabling it so its next spawn is
        // always a normal-size, collectible gem.
        transform.localScale = originalLocalScale;
        gameObject.SetActive(false);
        if (GameManager.instance != null) GameManager.instance.DiamondScore();
    }

    private void SetGemCollidersEnabled(bool enabled)
    {
        if (gemColliders == null) return;
        for (int i = 0; i < gemColliders.Length; i++)
            if (gemColliders[i] != null) gemColliders[i].enabled = enabled;
    }
}
