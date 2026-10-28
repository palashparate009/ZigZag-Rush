using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[Serializable]
public sealed class GhostFrame
{
    public float time;
    public Vector3 position;
    public Quaternion rotation;
}

[Serializable]
public sealed class GhostRunData
{
    public string date;
    public int routeVersion;
    public int score;
    public int carIndex;
    public List<GhostFrame> frames = new List<GhostFrame>();
}

public sealed class PersonalBestGhost : MonoBehaviour
{
    private const string DailyGhostKey = "DailyPersonalBestGhost";
    private readonly List<GhostFrame> recording = new List<GhostFrame>(2048);
    private CarController player;
    private GhostRunData playback;
    private Transform ghostRoot;
    private float activeRunElapsed;
    private float nextSampleAt;
    private int playbackIndex;
    private bool dailyRun;
    private static Shader ghostShader;
    private static GhostRunData latestSessionGhost;

    public void Initialize(CarController car)
    {
        player = car;
    }

    public void StartRun(bool daily)
    {
        dailyRun = daily && ConfigManager.Current.ghost.enabled;
        recording.Clear();
        playback = null;
        playbackIndex = 0;
        activeRunElapsed = 0f;
        nextSampleAt = 0f;
        DestroyGhost();

        // A position ghost is only fair on the daily seeded road, where the road
        // layout is identical for every attempt that day.
        if (!dailyRun || player == null) return;
        playback = LoadLatestGhost();
        if (playback == null) return;

        Debug.Log($"Loaded latest daily ghost: score {playback.score}, {playback.frames.Count} frames.");
        CreateGhostVisual();
    }

    public void CompleteRun(int score)
    {
        if (!dailyRun || recording.Count < 2 || score <= 0)
            return;

        GhostRunData saved = LoadLatestGhost();
        int savedScore = saved?.score ?? 0;
        int savedFrameCount = saved?.frames?.Count ?? 0;

        // A revived run can reach a new score after its first crash. Replace the
        // session cache and PlayerPrefs snapshot with the longest current result,
        // so the very next attempt never reuses the pre-revive ghost.
        if (score > savedScore || (score == savedScore && recording.Count > savedFrameCount))
        {
            GhostRunData data = new GhostRunData
            {
                date = TodayKey,
                routeVersion = PlatformSpawner.DailyRouteVersion,
                score = score,
                carIndex = GameProgress.SelectedCarIndex,
                frames = new List<GhostFrame>(recording)
            };
            latestSessionGhost = data;
            PlayerPrefs.SetString(DailyGhostKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Debug.Log($"Saved updated daily ghost: score {score}, {recording.Count} frames.");
            GameAnalytics.GameplayReward("daily_personal_best_ghost_saved", score);
        }
    }

    private void FixedUpdate()
    {
        if (!dailyRun || player == null || GameManager.instance == null ||
            GameManager.instance.State != GameState.Playing) return;
        // Count gameplay time only. Game-over UI, rewarded ads, and the revive
        // countdown must not create a large frozen gap in the next ghost replay.
        activeRunElapsed += Time.fixedDeltaTime;
        float elapsed = activeRunElapsed;
        GhostConfig config = ConfigManager.Current.ghost;
        if (elapsed >= nextSampleAt && recording.Count < config.maximumRecordedFrames)
        {
            recording.Add(new GhostFrame
            {
                time = elapsed,
                position = player.transform.position,
                rotation = player.transform.rotation
            });
            nextSampleAt = elapsed + config.sampleInterval;
        }

        UpdatePlayback(elapsed);
    }

    private void UpdatePlayback(float elapsed)
    {
        if (playback?.frames == null || ghostRoot == null) return;
        List<GhostFrame> frames = playback.frames;
        while (playbackIndex < frames.Count - 2 && frames[playbackIndex + 1].time <= elapsed)
            playbackIndex++;

        if (elapsed >= frames[frames.Count - 1].time)
        {
            // A ghost ends when its recorded daily run ends. Give it the same
            // quick "collected" feeling as a gem instead of leaving a frozen
            // car at its final (often fallen) position.
            playback = null;
            PlayCollectedDisappearAnimation();
            return;
        }

        if (!ghostRoot.gameObject.activeSelf) ghostRoot.gameObject.SetActive(true);

        GhostFrame from = frames[playbackIndex];
        GhostFrame to = frames[Mathf.Min(playbackIndex + 1, frames.Count - 1)];
        float duration = Mathf.Max(0.0001f, to.time - from.time);
        float t = Mathf.Clamp01((elapsed - from.time) / duration);
        ghostRoot.SetPositionAndRotation(
            Vector3.Lerp(from.position, to.position, t) + Vector3.up * ConfigManager.Current.ghost.verticalOffset,
            Quaternion.Slerp(from.rotation, to.rotation, t));
    }

    private void CreateGhostVisual()
    {
        GameObject root = new GameObject("Daily Personal Best Ghost");
        ghostRoot = root.transform;
        GameObject visual = player.CreateGhostVisual(ghostRoot);
        if (visual == null)
        {
            DestroyGhost();
            return;
        }

        ghostRoot.localScale *= ConfigManager.Current.ghost.scaleMultiplier;

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++) ConfigureGhostMaterial(materials[i]);
            renderer.materials = materials;
        }
    }

    private static void ConfigureGhostMaterial(Material material)
    {
        if (material == null) return;

        GhostConfig config = ConfigManager.Current.ghost;

        Texture mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        Vector2 textureScale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
        Vector2 textureOffset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
        Color originalColor = material.HasProperty("_BaseColor")
            ? material.GetColor("_BaseColor")
            : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

        if (ghostShader == null) ghostShader = Resources.Load<Shader>("Shaders/GhostCar");
        if (ghostShader == null) ghostShader = Shader.Find("ZigZag/GhostCar");
        if (ghostShader == null)
        {
            Debug.LogError("Missing ZigZag/GhostCar shader. The personal-best ghost cannot be rendered.");
            if (!config.preserveOriginalColors) originalColor = config.tintColor;
            originalColor.a = config.opacity;
            material.color = originalColor;
            return;
        }

        material.shader = ghostShader;
        if (mainTexture != null)
        {
            material.SetTexture("_MainTex", mainTexture);
            material.SetTextureScale("_MainTex", textureScale);
            material.SetTextureOffset("_MainTex", textureOffset);
        }
        Color ghostColor = config.preserveOriginalColors ? originalColor : config.tintColor;
        ghostColor.a = 1f;
        material.SetColor("_Color", ghostColor);
        material.SetColor("_RimColor", config.rimColor);
        material.SetFloat("_Opacity", config.opacity);
        material.SetFloat("_RimStrength", config.rimStrength);
        material.SetFloat("_RimPower", config.rimPower);
        material.SetFloat("_FaceAlpha", config.faceAlphaFactor);
        material.SetFloat("_RimAlpha", config.rimAlphaFactor);
        material.renderQueue = 3000;
    }

    private void DestroyGhost()
    {
        if (ghostRoot != null)
        {
            ghostRoot.DOKill(false);
            Destroy(ghostRoot.gameObject);
        }
        ghostRoot = null;
    }

    private void PlayCollectedDisappearAnimation()
    {
        if (ghostRoot == null) return;

        Transform root = ghostRoot;
        root.DOKill(false);

        const float duration = 0.18f;
        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(root)
            .SetLink(root.gameObject);

        // Rise and shrink quickly, like a collected item leaving the track.
        sequence.Join(root.DOMove(root.position + Vector3.up * 0.42f, duration).SetEase(Ease.OutQuad));
        sequence.Join(root.DOScale(root.localScale * 0.12f, duration).SetEase(Ease.InBack));

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.materials)
            {
                if (material == null) continue;

                // GhostCar uses _Opacity. Keep a color-alpha fallback in case
                // a future car material does not use the ghost shader.
                if (material.HasProperty("_Opacity"))
                {
                    Material ghostMaterial = material;
                    sequence.Join(DOTween.To(
                        () => ghostMaterial.GetFloat("_Opacity"),
                        value => ghostMaterial.SetFloat("_Opacity", value),
                        0f,
                        duration));
                }
                else if (material.HasProperty("_Color"))
                {
                    Material ghostMaterial = material;
                    Color transparent = ghostMaterial.GetColor("_Color");
                    transparent.a = 0f;
                    sequence.Join(DOTween.To(
                        () => ghostMaterial.GetColor("_Color"),
                        value => ghostMaterial.SetColor("_Color", value),
                        transparent,
                        duration));
                }
            }
        }

        sequence.OnComplete(() =>
        {
            if (root != null) Destroy(root.gameObject);
            if (ghostRoot == root) ghostRoot = null;
        });
    }

    // A revive rebuilds a safety runway rather than continuing the original
    // seeded route. Stop recording and hide the ghost so neither a saved nor
    // displayed ghost can appear off-road after that route change.
    public void StopForRouteChange()
    {
        if (!dailyRun) return;
        dailyRun = false;
        recording.Clear();
        playback = null;
        DestroyGhost();
    }

    private static GhostRunData LoadLatestGhost()
    {
        GhostRunData persisted = null;
        string json = PlayerPrefs.GetString(DailyGhostKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                persisted = JsonUtility.FromJson<GhostRunData>(json);
                if (!IsUsableToday(persisted)) persisted = null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Daily ghost data was ignored: {exception.Message}");
            }
        }

        if (!IsUsableToday(latestSessionGhost)) latestSessionGhost = null;
        if (IsBetterGhost(persisted, latestSessionGhost)) latestSessionGhost = persisted;
        return latestSessionGhost;
    }

    private static bool IsUsableToday(GhostRunData data) =>
        data != null &&
        data.date == TodayKey &&
        data.routeVersion == PlatformSpawner.DailyRouteVersion &&
        data.frames != null &&
        data.frames.Count >= 2;

    private static bool IsBetterGhost(GhostRunData candidate, GhostRunData current)
    {
        if (!IsUsableToday(candidate)) return false;
        if (!IsUsableToday(current)) return true;
        if (candidate.score != current.score) return candidate.score > current.score;
        return candidate.frames.Count > current.frames.Count;
    }

    private static string TodayKey => DateTime.UtcNow.ToString("yyyyMMdd");

    private void OnDestroy()
    {
        DestroyGhost();
    }
}
