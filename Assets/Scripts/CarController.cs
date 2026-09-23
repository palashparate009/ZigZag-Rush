using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    public GameObject particleEffect;
    [SerializeField] public float moveSpeed = 8f;
    [SerializeField] private float perfectTurnWindow = 0.34f;

    private Rigidbody body;
    private bool movingLeft = true;
    private Vector3 moveDirection;
    private Quaternion desiredRotation;
    private bool rotationPending;
    private Vector3 lastSafePosition;
    private Vector3 lastSafePlatformPosition;
    private Quaternion lastSafeRotation;
    private TrailRenderer trail;
    private Collider lastGroundCollider;
    private Platform lastGroundPlatform;
    private Renderer[] originalVisualRenderers;
    private Bounds referenceVisualBounds;
    private GameObject activeCarModel;
    private Transform visualTurnPivot;
    private float carSpeedMultiplier = 1f;
    private float carPerfectWindowMultiplier = 1f;
    private readonly RaycastHit[] groundHits = new RaycastHit[1];
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private int ignoreInputThroughFrame = -1;
    public Vector3 LastSafePlatformPosition => lastSafePlatformPosition;
    public Vector3 MovementDirection => moveDirection;

    private static readonly Color[] SkinColours =
    {
        new Color(0.95f, 0.12f, 0.18f), new Color(0.05f, 0.75f, 1f),
        new Color(0.3f, 1f, 0.25f), new Color(1f, 0.2f, 0.8f),
        new Color(1f, 0.72f, 0.05f), new Color(0.55f, 0.2f, 1f)
    };

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        initialPosition = body.position;
        initialRotation = body.rotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        desiredRotation = body.rotation;
        SetMovementDirection(body.rotation);
        lastSafePosition = body.position;
        lastSafePlatformPosition = body.position - Vector3.up * 0.5f;
        lastSafeRotation = body.rotation;
        originalVisualRenderers = GetComponentsInChildren<Renderer>(true);
        if (!CarVisualUtility.TryGetLocalRendererBounds(transform, originalVisualRenderers, out referenceVisualBounds))
            referenceVisualBounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.3f, 1.1f, 2.4f));
        EnsureVisualTurnPivot();
        CreateTrail();
        ApplyCar(GameProgress.SelectedCarIndex);
    }

    public void ResetForNewRun()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = initialPosition;
        body.rotation = initialRotation;
        transform.SetPositionAndRotation(initialPosition, initialRotation);

        desiredRotation = initialRotation;
        rotationPending = false;
        SetMovementDirection(initialRotation);
        lastSafePosition = initialPosition;
        lastSafePlatformPosition = initialPosition - Vector3.up * 0.5f;
        lastSafeRotation = initialRotation;
        lastGroundCollider = null;
        lastGroundPlatform = null;
        ignoreInputThroughFrame = Time.frameCount;
        ResetVisualTurn();
        if (trail != null) trail.Clear();
    }

    private void Update()
    {
        if (GameManager.instance == null || GameManager.instance.State != GameState.Playing) return;
        if (transform.position.y <= -2f)
        {
            GameManager.instance.GameOver();
            return;
        }
        if (Time.frameCount <= ignoreInputThroughFrame) return;
        if (PressedThisFrame() && !PointerIsOverUi()) ChangeDirection();
    }

    private void FixedUpdate()
    {
        if (GameManager.instance == null || GameManager.instance.State != GameState.Playing) return;
        GameplayConfig config = ConfigManager.Current.gameplay;
        bool beginnerSpeedEnabled = IsNewPlayer(config);
        float configuredBaseSpeed = beginnerSpeedEnabled
            ? config.newPlayerMoveSpeed
            : config.baseMoveSpeed;
        if (configuredBaseSpeed <= 0f) configuredBaseSpeed = moveSpeed;
        float difficultySpeed = Mathf.Lerp(
            1f, config.maximumDifficultySpeedMultiplier, GameManager.instance.Difficulty01);
        float speed = configuredBaseSpeed * carSpeedMultiplier * difficultySpeed * GameManager.instance.SpeedMultiplier;
        if (rotationPending)
        {
            Quaternion visualRotationBeforeTurn = visualTurnPivot != null ? visualTurnPivot.rotation : body.rotation;
            body.MoveRotation(desiredRotation);
            SmoothVisualTurn(visualRotationBeforeTurn, desiredRotation);
            rotationPending = false;
        }
        body.MovePosition(body.position + moveDirection * speed * Time.fixedDeltaTime);

        int hits = Physics.RaycastNonAlloc(body.position + Vector3.up * 0.15f, Vector3.down, groundHits, 1.8f);
        if (hits > 0 && IsPlatformCollider(groundHits[0].collider))
        {
            lastSafePosition = body.position;
            lastSafePlatformPosition = groundHits[0].collider.bounds.center;
            lastSafeRotation = body.rotation;
        }
    }

    private void ChangeDirection()
    {
        bool perfect = IsPerfectTurn();
        movingLeft = !movingLeft;
        moveDirection = movingLeft ? Vector3.forward : Vector3.right;
        desiredRotation = Quaternion.Euler(0f, movingLeft ? 0f : 90f, 0f);
        rotationPending = true;
        GameManager.instance.RegisterTurn(perfect);
    }

    private bool IsPerfectTurn()
    {
        if (!Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 2f)) return false;
        if (!IsPlatformCollider(hit.collider)) return false;
        Vector3 offset = transform.position - hit.collider.bounds.center;
        offset.y = 0f;
        GameplayConfig config = ConfigManager.Current.gameplay;
        float configuredWindow = IsNewPlayer(config)
            ? config.newPlayerPerfectTurnWindow
            : config.perfectTurnWindow;
        if (configuredWindow <= 0f) configuredWindow = perfectTurnWindow;
        return Mathf.Abs(Vector3.Dot(offset, moveDirection)) <= configuredWindow * carPerfectWindowMultiplier;
    }

    private static bool IsNewPlayer(GameplayConfig _) =>
        GameManager.instance != null && GameManager.instance.UsesNewPlayerSpeedThisRun;

    public void Revive(float safetyInset = 0f)
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        Vector3 safeTileCentre = new Vector3(lastSafePlatformPosition.x, lastSafePosition.y, lastSafePlatformPosition.z);
        Vector3 revivePosition = Vector3.Lerp(lastSafePosition, safeTileCentre, Mathf.Clamp01(safetyInset));
        body.position = revivePosition + Vector3.up * 0.25f;
        body.rotation = lastSafeRotation;
        desiredRotation = lastSafeRotation;
        rotationPending = false;
        SetMovementDirection(lastSafeRotation);
        transform.SetPositionAndRotation(body.position, body.rotation);
        ResetVisualTurn();
    }

    public void ApplyCar(int carIndex)
    {
        CarCatalog catalog = CarCatalog.Load();
        CarDefinition definition = catalog?.Get(carIndex);
        if (definition?.prefab == null)
        {
            ApplyLegacySkin(carIndex);
            return;
        }

        if (activeCarModel != null)
        {
            activeCarModel.SetActive(false);
            Destroy(activeCarModel);
        }
        foreach (Renderer original in originalVisualRenderers)
            if (original != null) original.enabled = false;

        EnsureVisualTurnPivot();
        ResetVisualTurn();
        activeCarModel = Instantiate(definition.prefab, visualTurnPivot);
        activeCarModel.name = definition.displayName + " Visual";
        CarVisualUtility.RemoveGameplayComponents(activeCarModel);
        CarVisualUtility.NormalizeToBounds(activeCarModel.transform, transform, referenceVisualBounds);
        carSpeedMultiplier = definition.speedMultiplier;
        carPerfectWindowMultiplier = definition.perfectWindowMultiplier;

        Color trailColour = RepresentativeColour(activeCarModel);
        if (trail != null)
        {
            trail.startColor = trailColour;
            trail.endColor = new Color(trailColour.r, trailColour.g, trailColour.b, 0f);
        }
        ApplyMasteryCosmetics(carIndex, trailColour);
    }

    public void ApplySkin(int skinIndex)
    {
        ApplyCar(skinIndex);
    }

    public GameObject CreateGhostVisual(Transform parent)
    {
        if (parent == null) return null;

        // The player root can be scaled in the scene. Matching that scale and
        // cloning the already-normalized active visual guarantees that the ghost
        // has exactly the same dimensions, rotation correction and model offset.
        parent.localScale = transform.lossyScale;
        GameObject visual;
        if (activeCarModel != null)
        {
            visual = Instantiate(activeCarModel, parent, false);
            visual.name = activeCarModel.name.Replace(" Visual", " Ghost Visual");
            visual.transform.localPosition = activeCarModel.transform.localPosition;
            visual.transform.localRotation = activeCarModel.transform.localRotation;
            visual.transform.localScale = activeCarModel.transform.localScale;
        }
        else
        {
            CarDefinition definition = CarCatalog.Load()?.Get(GameProgress.SelectedCarIndex);
            if (definition?.prefab == null) return null;
            visual = Instantiate(definition.prefab, parent);
            visual.name = definition.displayName + " Ghost Visual";
            CarVisualUtility.NormalizeToBounds(visual.transform, parent, referenceVisualBounds);
        }

        CarVisualUtility.RemoveGameplayComponents(visual);
        return visual;
    }

    private void ApplyLegacySkin(int skinIndex)
    {
        carSpeedMultiplier = 1f;
        carPerfectWindowMultiplier = 1f;
        Color colour = SkinColours[Mathf.Clamp(skinIndex, 0, SkinColours.Length - 1)];
        foreach (Renderer targetRenderer in originalVisualRenderers)
        {
            if (targetRenderer == null) continue;
            targetRenderer.enabled = true;
            Material[] materials = targetRenderer.materials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].name.ToLowerInvariant().Contains("paint")) continue;
                materials[i].color = colour;
                changed = true;
            }
            if (changed) targetRenderer.materials = materials;
        }
    }

    private static Color RepresentativeColour(GameObject model)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.sharedMaterial == null) continue;
            if (renderer.sharedMaterial.HasProperty("_BaseColor")) return renderer.sharedMaterial.GetColor("_BaseColor");
            if (renderer.sharedMaterial.HasProperty("_Color")) return renderer.sharedMaterial.color;
        }
        return Color.white;
    }

    private void ApplyMasteryCosmetics(int carIndex, Color baseColour)
    {
        int level = GameProgress.GetCarMasteryLevel(carIndex);
        if (trail != null)
        {
            trail.time = level >= 4 ? 0.72f : level >= 2 ? 0.5f : 0.38f;
            trail.startWidth = level >= 4 ? 0.28f : level >= 2 ? 0.23f : 0.2f;
            Color masteryTrail = level >= 5 ? new Color(1f, 0.35f, 0.9f) : new Color(0.1f, 0.95f, 1f);
            trail.startColor = level >= 2 ? masteryTrail : baseColour;
            trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f);
        }

        if (level < 3 || activeCarModel == null) return;
        Color masteryPaint = level >= 5 ? new Color(0.95f, 0.3f, 1f) : new Color(1f, 0.68f, 0.08f);
        foreach (Renderer renderer in activeCarModel.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.materials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                string materialName = material.name.ToLowerInvariant();
                if (!materialName.Contains("paint") && !materialName.Contains("body")) continue;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", masteryPaint);
                if (material.HasProperty("_Color")) material.color = masteryPaint;
                changed = true;
            }
            if (changed) renderer.materials = materials;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Diamond")) return;
        GameManager.instance.DiamondScore();
        if (particleEffect != null) Instantiate(particleEffect, other.transform.position, particleEffect.transform.rotation);
        other.gameObject.SetActive(false);
    }

    private void CreateTrail()
    {
        GameObject trailObject = new GameObject("Car Trail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, 0.12f, -0.9f);
        trail = trailObject.AddComponent<TrailRenderer>();
        trail.time = 0.38f;
        trail.startWidth = 0.2f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.08f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    private static bool PressedThisFrame()
    {
        return Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    private static bool PointerIsOverUi()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsPlatformCollider(Collider candidate)
    {
        if (candidate == null) return false;
        if (candidate == lastGroundCollider) return lastGroundPlatform != null;
        lastGroundCollider = candidate;
        lastGroundPlatform = candidate.GetComponent<Platform>();
        return lastGroundPlatform != null;
    }

    private void SetMovementDirection(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        movingLeft = Mathf.Abs(forward.z) >= Mathf.Abs(forward.x);
        moveDirection = movingLeft ? Vector3.forward : Vector3.right;
    }

    private void EnsureVisualTurnPivot()
    {
        if (visualTurnPivot != null) return;
        GameObject pivotObject = new GameObject("Car Visual Turn Pivot");
        visualTurnPivot = pivotObject.transform;
        visualTurnPivot.SetParent(transform, false);
    }

    private void SmoothVisualTurn(Quaternion startWorldRotation, Quaternion targetWorldRotation)
    {
        if (visualTurnPivot == null || activeCarModel == null) return;

        GameplayConfig config = ConfigManager.Current.gameplay;
        visualTurnPivot.DOKill(false);
        if (!config.smoothVisualTurns || config.visualTurnDuration <= 0f)
        {
            visualTurnPivot.localRotation = Quaternion.identity;
            return;
        }

        visualTurnPivot.rotation = startWorldRotation;
        float duration = Mathf.Clamp(config.visualTurnDuration, 0.05f, 0.35f);
        Tweener turnTween = visualTurnPivot
            .DORotateQuaternion(targetWorldRotation, duration)
            .SetEase(ParseTurnEase(config.visualTurnEase));
        turnTween.ForceInit();
    }

    private void ResetVisualTurn()
    {
        if (visualTurnPivot == null) return;
        visualTurnPivot.DOKill(false);
        visualTurnPivot.localRotation = Quaternion.identity;
    }

    private static Ease ParseTurnEase(string configuredEase)
    {
        return System.Enum.TryParse(configuredEase, true, out Ease ease) ? ease : Ease.OutCubic;
    }

    private void OnDestroy()
    {
        if (visualTurnPivot != null) visualTurnPivot.DOKill(false);
    }
}
