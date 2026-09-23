using UnityEngine;
using UnityEngine.UI;

public sealed class CarGaragePreview : MonoBehaviour
{
    private const int PreviewLayer = 31;
    private RawImage targetImage;
    private RenderTexture renderTexture;
    private GameObject previewRig;
    private Transform modelHolder;
    private GameObject activeModel;

    public void Initialize(RawImage image)
    {
        targetImage = image;
        renderTexture = new RenderTexture(512, 384, 16, RenderTextureFormat.ARGB32)
        {
            name = "Garage Car Preview",
            antiAliasing = 2
        };
        renderTexture.Create();
        targetImage.texture = renderTexture;

        previewRig = new GameObject("Garage Preview Rig");
        previewRig.hideFlags = HideFlags.HideAndDontSave;
        previewRig.transform.position = new Vector3(5000f, -5000f, 5000f);
        modelHolder = new GameObject("Car Model").transform;
        modelHolder.SetParent(previewRig.transform, false);

        Camera previewCamera = new GameObject("Preview Camera").AddComponent<Camera>();
        previewCamera.transform.SetParent(previewRig.transform, false);
        previewCamera.transform.localPosition = new Vector3(3.2f, 2.25f, -4.4f);
        previewCamera.transform.LookAt(modelHolder.position + Vector3.up * 0.45f);
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 1.08f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.025f, 0.035f, 0.075f, 1f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.targetTexture = renderTexture;
        previewCamera.nearClipPlane = 0.1f;
        previewCamera.farClipPlane = 20f;

        Light keyLight = new GameObject("Preview Light").AddComponent<Light>();
        keyLight.transform.SetParent(previewRig.transform, false);
        keyLight.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.25f;
        keyLight.cullingMask = 1 << PreviewLayer;
    }

    public void Show(CarDefinition definition)
    {
        if (activeModel != null)
        {
            activeModel.SetActive(false);
            Destroy(activeModel);
        }
        activeModel = null;
        if (definition?.prefab == null || modelHolder == null) return;

        modelHolder.localRotation = Quaternion.Euler(0f, -28f, 0f);
        activeModel = Instantiate(definition.prefab, modelHolder);
        activeModel.name = definition.displayName + " Preview";
        CarVisualUtility.RemoveGameplayComponents(activeModel);
        CarVisualUtility.SetLayerRecursively(activeModel, PreviewLayer);
        Bounds target = new Bounds(new Vector3(0f, 0.45f, 0f), new Vector3(1.25f, 0.9f, 2.35f));
        CarVisualUtility.NormalizeToBounds(activeModel.transform, modelHolder, target);
    }

    private void Update()
    {
        if (modelHolder != null && targetImage != null && targetImage.gameObject.activeInHierarchy)
            modelHolder.Rotate(0f, 24f * Time.unscaledDeltaTime, 0f, Space.Self);
    }

    private void OnDestroy()
    {
        if (previewRig != null) Destroy(previewRig);
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
