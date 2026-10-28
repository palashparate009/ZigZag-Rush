using System;
using System.Collections.Generic;
using UnityEngine;

public class PlatformSpawner : MonoBehaviour
{
    // Increment this whenever the deterministic daily-road algorithm changes.
    // Old position ghosts are then ignored instead of being replayed on a
    // different route after an update.
    public const int DailyRouteVersion = 4;

    private static readonly SpecialTileType[] EarlyTileTypes =
    {
        SpecialTileType.SpeedBoost, SpecialTileType.SlowMotion, SpecialTileType.Shield,
        SpecialTileType.GemMagnet, SpecialTileType.Mystery
    };

    private static readonly SpecialTileType[] FullTileTypes =
    {
        SpecialTileType.SpeedBoost, SpecialTileType.SlowMotion, SpecialTileType.Shield,
        SpecialTileType.GemMagnet, SpecialTileType.Crumbling, SpecialTileType.Moving,
        SpecialTileType.Mystery
    };

    public GameObject platform;
    public Transform lastPlatform;

    [SerializeField] private int lookAheadPlatforms = 32;
    [SerializeField] private int initialPoolSize = 42;

    private readonly Queue<Platform> pool = new Queue<Platform>();
    private readonly HashSet<Platform> activePlatforms = new HashSet<Platform>();
    private Vector3 lastPosition;
    private int lastDirection = -1;
    private int straightLength;
    private int theme;
    private bool configured;
    private bool initialized;
    private bool stopped;
    private System.Random seededRandom;
    private bool dailyRoute;
    private int dailyGeneratedTileCount;
    private Vector3 generationStartPosition;
    private readonly List<Platform> initialRunwayPlatforms = new List<Platform>();
    private readonly List<Vector3> initialRunwayPositions = new List<Vector3>();
    private readonly List<Platform> skippedPlatformBuffer = new List<Platform>();
    private Transform playerTransform;
    private int tilesSinceBranch = 999;
    private int tilesSinceSpecialTile = 999;
    private int tilesSinceDiamond = 999;
    private float dailyRouteElapsed;

    public float RouteClock => dailyRoute ? dailyRouteElapsed : Time.fixedTime;

    private void Start()
    {
        EnsureInitialized();
    }

    private void FixedUpdate()
    {
        if (dailyRoute && GameManager.instance != null && GameManager.instance.State == GameState.Playing)
            dailyRouteElapsed += Time.fixedDeltaTime;
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        if (!configured) Configure(false, GameProgress.SelectedTheme);
        CaptureInitialRunway();
        generationStartPosition = lastPlatform != null ? lastPlatform.position : Vector3.zero;
        lastPosition = generationStartPosition;
        BuildPool();
        for (int i = 0; i < lookAheadPlatforms; i++) SpawnOne();
        initialized = true;
    }

    private void CaptureInitialRunway()
    {
        initialRunwayPlatforms.Clear();
        initialRunwayPositions.Clear();

        // These are the hand-placed approach tiles between the large starting
        // platform and lastPlatform. Pool objects do not exist yet at this point.
        Platform[] scenePlatforms = FindObjectsByType<Platform>(FindObjectsSortMode.None);
        foreach (Platform scenePlatform in scenePlatforms)
        {
            if (scenePlatform == null || scenePlatform.transform.IsChildOf(transform)) continue;
            initialRunwayPlatforms.Add(scenePlatform);
            initialRunwayPositions.Add(scenePlatform.transform.position);
        }
    }

    private void Update()
    {
        if (stopped || GameManager.instance == null || GameManager.instance.State != GameState.Playing) return;
        CleanupSkippedBranchTiles();
        while (activePlatforms.Count < lookAheadPlatforms) SpawnOne();
    }

    public void Configure(bool daily, int selectedTheme)
    {
        theme = selectedTheme;
        configured = true;
        stopped = false;
        dailyRoute = daily;
        dailyGeneratedTileCount = 0;
        dailyRouteElapsed = 0f;
        tilesSinceDiamond = 999;
        int dailySeed = DateTime.UtcNow.Year * 1000 + DateTime.UtcNow.DayOfYear;
        seededRandom = daily ? new System.Random(dailySeed) : null;
        if (playerTransform == null)
        {
            CarController player = FindFirstObjectByType<CarController>();
            if (player != null) playerTransform = player.transform;
        }
    }

    public void SetTheme(int selectedTheme)
    {
        theme = selectedTheme;
        if (lastPlatform != null && lastPlatform.TryGetComponent(out Platform startingTile))
            startingTile.ApplyTheme(theme);
    }

    public void StopSpawning() => stopped = true;
    public void ResumeSpawning() => stopped = false;

    public void ResetForNewRun(bool daily, int selectedTheme)
    {
        // GameManager can auto-start a Daily Run during scene startup. Ensure
        // the fixed scene runway has been captured first; otherwise a reset can
        // use the default Vector3.zero origin and begin halfway along a road.
        EnsureInitialized();
        stopped = true;

        List<Platform> oldPlatforms = new List<Platform>(activePlatforms);
        activePlatforms.Clear();
        foreach (Platform oldPlatform in oldPlatforms)
        {
            if (oldPlatform == null) continue;
            oldPlatform.gameObject.SetActive(false);
            pool.Enqueue(oldPlatform);
        }

        // The hand-placed approach tiles destroy themselves after the car leaves.
        // Disable any that still exist, then recreate all of them from the pool.
        foreach (Platform runwayPlatform in initialRunwayPlatforms)
            if (runwayPlatform != null) runwayPlatform.gameObject.SetActive(false);

        Configure(daily, selectedTheme);
        lastPosition = generationStartPosition;
        lastDirection = -1;
        straightLength = 0;
        tilesSinceBranch = 999;
        tilesSinceSpecialTile = 999;
        tilesSinceDiamond = 999;

        foreach (Vector3 runwayPosition in initialRunwayPositions)
            SpawnAt(runwayPosition, SpecialTileType.Normal, false);
        for (int i = 0; i < lookAheadPlatforms; i++) SpawnOne();
    }

    public void CreateRescuePlatform(Vector3 position)
    {
        if (platform == null) return;
        SpawnAt(position, SpecialTileType.Normal, false);
    }

    public void RebuildForRevive(Vector3 position, Vector3 movementDirection)
    {
        if (platform == null) return;
        stopped = true;

        List<Platform> oldPlatforms = new List<Platform>(activePlatforms);
        activePlatforms.Clear();
        foreach (Platform oldPlatform in oldPlatforms)
        {
            if (oldPlatform == null) continue;
            oldPlatform.gameObject.SetActive(false);
            pool.Enqueue(oldPlatform);
        }

        Vector3 runwayDirection = Mathf.Abs(movementDirection.x) >= Mathf.Abs(movementDirection.z)
            ? Vector3.right
            : Vector3.forward;
        lastPosition = position;
        lastDirection = runwayDirection == Vector3.right ? 1 : 0;
        straightLength = 1;
        tilesSinceSpecialTile = 999;

        // Replace the recycled corner with a stable tile and enough straight road
        // for the player to settle after the countdown before the next turn.
        SpawnAt(lastPosition, SpecialTileType.Normal, false);
        int safeRunwayTiles = Mathf.Max(1, ConfigManager.Current.gameplay.reviveRunwayTiles);
        for (int i = 0; i < safeRunwayTiles; i++)
        {
            lastPosition += runwayDirection * 2f;
            SpawnAt(lastPosition, SpecialTileType.Normal, false);
            straightLength++;
        }

        while (activePlatforms.Count < lookAheadPlatforms) SpawnOne();
    }

    public void Release(Platform tile)
    {
        if (tile == null || !activePlatforms.Remove(tile)) return;
        tile.gameObject.SetActive(false);
        pool.Enqueue(tile);
    }

    private void BuildPool()
    {
        if (platform == null) return;
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject item = Instantiate(platform, transform);
            item.SetActive(false);
            pool.Enqueue(item.GetComponent<Platform>());
        }
    }

    private void SpawnOne()
    {
        if (platform == null) return;
        if (TrySpawnRouteBranch()) return;
        GeneratePosition();
        SpecialTileType type = ChooseTileType();
        GameplayConfig config = ConfigManager.Current.gameplay;
        bool diamond = type == SpecialTileType.Normal &&
                       ShouldSpawnDiamond(Mathf.Lerp(config.diamondChanceMin, config.diamondChanceMax, CurrentDifficulty()));
        SpawnAt(lastPosition, type, diamond);
        tilesSinceBranch++;
        if (type == SpecialTileType.Normal) tilesSinceSpecialTile++;
        else tilesSinceSpecialTile = 0;
    }

    private void SpawnAt(Vector3 position, SpecialTileType type, bool diamond, int routeChoiceMarker = 0)
    {
        Platform tile;
        if (pool.Count > 0) tile = pool.Dequeue();
        else tile = Instantiate(platform, transform).GetComponent<Platform>();
        tile.gameObject.SetActive(false);
        // Moving tiles also need a seeded phase. Otherwise their vertical
        // position differs between attempts even when the road layout matches.
        float movementOffset = dailyRoute ? NextFloat() * 10f : -1f;
        tile.Initialize(this, position, type, theme, diamond, routeChoiceMarker, movementOffset);
        tile.gameObject.SetActive(true);
        activePlatforms.Add(tile);
        tilesSinceDiamond = diamond ? 0 : tilesSinceDiamond + 1;
        if (dailyRoute) dailyGeneratedTileCount++;
    }

    private bool TrySpawnRouteBranch()
    {
        GameplayConfig config = ConfigManager.Current.gameplay;
        // A position ghost cannot remain on the same road when two attempts can
        // choose different branches. Daily runs therefore use one fixed route.
        if (dailyRoute) return false;

        int score = GameManager.instance != null ? GameManager.instance.Score : 0;
        if (!config.routeBranchesEnabled || score < config.routeBranchesStartScore ||
            tilesSinceBranch < config.routeBranchCooldownTiles) return false;

        float chance = Mathf.Lerp(config.routeBranchChanceMin, config.routeBranchChanceMax, CurrentDifficulty());
        if (NextFloat() > chance) return false;

        int length = Mathf.Clamp(config.routeBranchLength, 2, 5);
        bool riskStartsForward = NextInt(0, 2) == 0;
        Vector3 forward = Vector3.forward * 2f;
        Vector3 right = Vector3.right * 2f;
        SpawnBranchPath(lastPosition, riskStartsForward ? forward : right,
            riskStartsForward ? right : forward, length, true, config.riskRouteGemChance);
        SpawnBranchPath(lastPosition, riskStartsForward ? right : forward,
            riskStartsForward ? forward : right, length, false, 0f);

        lastPosition += (forward + right) * length;
        SpawnAt(lastPosition, SpecialTileType.Normal, false);
        lastDirection = -1;
        straightLength = 0;
        tilesSinceBranch = 0;
        // The shared endpoint is always stable. Continue the special-tile
        // cooldown from there when ordinary procedural generation resumes.
        tilesSinceSpecialTile = 1;
        return true;
    }

    private void SpawnBranchPath(Vector3 origin, Vector3 firstAxis, Vector3 secondAxis, int length,
        bool risky, float gemChance)
    {
        Vector3 position = origin;
        for (int i = 1; i <= length; i++)
        {
            position += firstAxis;
            SpawnAt(position, risky ? RiskTileFor(i) : SpecialTileType.Normal,
                risky && ShouldSpawnDiamond(gemChance), i == 1 ? (risky ? 1 : -1) : 0);
        }

        // The common endpoint is spawned once after both paths.
        for (int i = 1; i < length; i++)
        {
            position += secondAxis;
            SpawnAt(position, risky ? RiskTileFor(length + i) : SpecialTileType.Normal,
                risky && ShouldSpawnDiamond(gemChance));
        }
    }

    private SpecialTileType RiskTileFor(int index)
    {
        // Risk routes still contain hazards, but never consecutive hazards.
        // Two stable blocks give the player time to read and react to each one.
        int spacing = Mathf.Max(2, ConfigManager.Current.gameplay.riskRouteHazardSpacing);
        if (index % spacing != 0) return SpecialTileType.Normal;
        return (index / spacing) % 2 == 0 ? SpecialTileType.Moving : SpecialTileType.Crumbling;
    }

    private void CleanupSkippedBranchTiles()
    {
        if (playerTransform == null) return;
        float playerProgress = playerTransform.position.x + playerTransform.position.z;
        skippedPlatformBuffer.Clear();
        foreach (Platform tile in activePlatforms)
        {
            if (tile == null || !tile.gameObject.activeSelf) continue;
            float tileProgress = tile.transform.position.x + tile.transform.position.z;
            if (tileProgress < playerProgress - 8f) skippedPlatformBuffer.Add(tile);
        }

        foreach (Platform tile in skippedPlatformBuffer) Release(tile);
    }

    private void GeneratePosition()
    {
        float difficulty = CurrentDifficulty();
        GameplayConfig config = ConfigManager.Current.gameplay;
        int maxStraight = difficulty < config.midRoadDifficultyThreshold
            ? config.earlyMaxStraightTiles
            : difficulty < config.lateRoadDifficultyThreshold
                ? config.midMaxStraightTiles
                : config.lateMaxStraightTiles;
        int direction = NextInt(0, 2);
        if (straightLength >= maxStraight) direction = 1 - lastDirection;

        if (direction == lastDirection) straightLength++;
        else straightLength = 1;
        lastDirection = direction;

        if (direction == 1) lastPosition.x += 2f;
        else lastPosition.z += 2f;
    }

    private SpecialTileType ChooseTileType()
    {
        int score = CurrentRouteScore();
        GameplayConfig config = ConfigManager.Current.gameplay;
        int minimumGap = config.specialTileMinGap > 0
            ? Mathf.Clamp(config.specialTileMinGap, 2, 12)
            : 4;
        if (tilesSinceSpecialTile < minimumGap) return SpecialTileType.Normal;
        if (score < config.specialTilesStartScore ||
            NextFloat() > Mathf.Lerp(config.specialTileChanceMin, config.specialTileChanceMax, CurrentDifficulty()))
            return SpecialTileType.Normal;

        SpecialTileType[] choices = score < config.fullSpecialTilesScore ? EarlyTileTypes : FullTileTypes;
        return choices[NextInt(0, choices.Length)];
    }

    private float CurrentDifficulty()
    {
        if (dailyRoute)
            return Mathf.Clamp01(dailyGeneratedTileCount /
                                 (float)Mathf.Max(1, ConfigManager.Current.gameplay.difficultyScoreCap));
        return GameManager.instance != null ? GameManager.instance.Difficulty01 : 0f;
    }

    private int CurrentRouteScore()
    {
        return dailyRoute
            ? dailyGeneratedTileCount
            : GameManager.instance != null ? GameManager.instance.Score : 0;
    }

    private bool ShouldSpawnDiamond(float chance)
    {
        GameplayConfig config = ConfigManager.Current.gameplay;
        int minimumGap = Mathf.Clamp(config.diamondMinGap, 2, 20);
        return tilesSinceDiamond >= minimumGap && NextFloat() < Mathf.Clamp01(chance);
    }

    private int NextInt(int min, int max)
    {
        return seededRandom != null ? seededRandom.Next(min, max) : UnityEngine.Random.Range(min, max);
    }

    private float NextFloat()
    {
        return seededRandom != null ? (float)seededRandom.NextDouble() : UnityEngine.Random.value;
    }
}
