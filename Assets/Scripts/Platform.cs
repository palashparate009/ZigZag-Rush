using System.Collections;
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
    private int routeChoiceMarker;

    private const int IconTextureSize = 32;
    private static readonly Material[] TileIconMaterials = new Material[8];
    private static Mesh tileIconMesh;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        tileRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
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
        float y = Mathf.Sin((Time.fixedTime + movementOffset) * config.movingTileSpeed) *
                  config.movingTileAmplitude;
        body.MovePosition(basePosition + Vector3.up * y);
    }

    public void Initialize(PlatformSpawner platformOwner, Vector3 position, SpecialTileType type, int theme, bool spawnDiamond,
        int choiceMarker = 0)
    {
        owner = platformOwner;
        tileType = type;
        basePosition = position;
        movementOffset = Random.Range(0f, 10f);
        activated = false;
        falling = false;
        ftueTooltipShown = false;
        routeChoiceMarker = choiceMarker;
        CancelInvoke();
        StopAllCoroutines();

        transform.SetPositionAndRotation(position, Quaternion.identity);
        body.isKinematic = true;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        ConfigureVisual(theme);
        ConfigureTileIcon(type);
        enabled = type != SpecialTileType.Normal;

        if (spawnDiamond && diamond != null)
        {
            if (diamondInstance == null)
                diamondInstance = Instantiate(diamond, transform);
            diamondInstance.transform.SetLocalPositionAndRotation(Vector3.up, diamond.transform.rotation);
            diamondInstance.SetActive(true);
        }
        else if (diamondInstance != null) diamondInstance.SetActive(false);
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
            Invoke(nameof(Fall), ConfigManager.Current.gameplay.crumblingTileDelay);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") && !falling)
            Invoke(nameof(Fall), tileType == SpecialTileType.Crumbling
                ? ConfigManager.Current.gameplay.crumblingTileExitDelay
                : ConfigManager.Current.gameplay.normalTileFallDelay);
    }

    private void Fall()
    {
        if (falling) return;
        falling = true;
        body.isKinematic = false;
        StartCoroutine(ReturnAfterDelay());
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
        Color colour = GetConfiguredColour(tileType, theme);
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
            GameObject marker = new GameObject("Special Tile Icon");
            marker.layer = gameObject.layer;
            marker.transform.SetParent(transform, false);

            MeshFilter filter = marker.AddComponent<MeshFilter>();
            filter.sharedMesh = GetTileIconMesh();
            tileIconRenderer = marker.AddComponent<MeshRenderer>();
            tileIconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tileIconRenderer.receiveShadows = false;
            tileIconRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            tileIconRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            tileIconRenderer.sortingOrder = 1;
        }

        tileIconRenderer.sharedMaterial = GetTileIconMaterial(type);
        tileIconRenderer.gameObject.SetActive(true);
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
