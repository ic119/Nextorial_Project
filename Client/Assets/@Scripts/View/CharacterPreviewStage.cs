using System;
using UnityEngine;

/// <summary>
/// 3D 캐릭터 실시간 프리뷰를 위한 스테이지, 카메라, 라이팅 및 커스터마이징 관리 클래스
/// </summary>
public class CharacterPreviewStage : MonoBehaviour
{
    #region Variable
    [Header("Preview Camera & Lighting")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Light keyLight;
    [SerializeField] private Light fillLight;
    [SerializeField] private Transform characterRoot;
    [SerializeField] private Transform stagePedestal;

    [Header("Character Model & Customization")]
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private GameObject characterModelInstance;
    [SerializeField] private CharacterCustomModel customModel;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSensitivity = 0.5f;
    [SerializeField] private float autoRotationSpeed = 10f;
    [SerializeField] private bool autoRotate = false;

    private RenderTexture previewRenderTexture;
    private float currentYaw = 180f;
    private float targetYaw = 180f;

    public RenderTexture PreviewTexture => previewRenderTexture;
    public CharacterCustomModel CustomModel => customModel;

    public int HairCount => customModel != null ? customModel.HairCount : 13;
    public int EyeCount => customModel != null ? customModel.EyeCount : 12;
    public int MouthCount => customModel != null ? customModel.MouthCount : 12;

    public int CurrentHairIndex => customModel != null ? customModel.CurrentHairIndex : 0;
    public int CurrentEyeIndex => customModel != null ? customModel.CurrentEyeIndex : 0;
    public int CurrentMouthIndex => customModel != null ? customModel.CurrentMouthIndex : 0;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        InitializeStage();
    }

    private void Update()
    {
        if (autoRotate)
        {
            targetYaw += autoRotationSpeed * Time.deltaTime;
        }

        currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * 15f);

        if (characterRoot != null)
        {
            characterRoot.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
    }

    private void OnDestroy()
    {
        CleanupRenderTexture();
    }
    #endregion

    #region Method
    /// <summary>
    /// 프리뷰 스테이지 및 렌더 텍스처 초기화
    /// </summary>
    public RenderTexture SetupPreview(int width = 1024, int height = 1024)
    {
        InitializeStage();

        if (previewRenderTexture != null)
        {
            CleanupRenderTexture();
        }

        previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "CharacterPreview_RenderTexture",
            antiAliasing = 4,
            useMipMap = false,
            autoGenerateMips = false
        };
        previewRenderTexture.Create();

        if (previewCamera != null)
        {
            previewCamera.targetTexture = previewRenderTexture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 0f);
        }

        ResetRotation();
        return previewRenderTexture;
    }

    public void InitializeStage()
    {
        if (previewCamera == null)
        {
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.05f, 2.3f);
            camGo.transform.localRotation = Quaternion.Euler(3f, 180f, 0f);
            previewCamera = camGo.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 0f);
            previewCamera.fieldOfView = 34f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 20f;
        }

        if (characterRoot == null)
        {
            var rootGo = new GameObject("CharacterRoot");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = Vector3.zero;
            characterRoot = rootGo.transform;
        }

        EnsureCharacterModel();
        EnsureLights();
        EnsurePedestal();
    }

    private void EnsureCharacterModel()
    {
        if (characterModelInstance == null)
        {
            if (characterPrefab == null)
            {
#if UNITY_EDITOR
                characterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AddressableAssets/Prefabs/Character/BasicCharacter.prefab");
#endif
            }

            if (characterPrefab != null)
            {
                characterModelInstance = Instantiate(characterPrefab, characterRoot);
                characterModelInstance.name = "BasicCharacter_Preview";
                characterModelInstance.transform.localPosition = Vector3.zero;
                characterModelInstance.transform.localRotation = Quaternion.identity;
                customModel = characterModelInstance.GetComponent<CharacterCustomModel>();
            }
        }
        else if (customModel == null)
        {
            customModel = characterModelInstance.GetComponent<CharacterCustomModel>();
        }
    }

    private void EnsureLights()
    {
        if (keyLight == null)
        {
            var keyGo = new GameObject("PreviewKeyLight");
            keyGo.transform.SetParent(transform, false);
            keyGo.transform.localPosition = new Vector3(1.5f, 3f, 2f);
            keyGo.transform.localRotation = Quaternion.Euler(45f, -140f, 0f);
            keyLight = keyGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.3f;
            keyLight.color = new Color(1f, 0.96f, 0.92f);
        }

        if (fillLight == null)
        {
            var fillGo = new GameObject("PreviewFillLight");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(-2f, 1.5f, -1f);
            fillGo.transform.localRotation = Quaternion.Euler(20f, 40f, 0f);
            fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.7f;
            fillLight.color = new Color(0.6f, 0.8f, 1f);
        }
    }

    private void EnsurePedestal()
    {
        if (stagePedestal == null)
        {
            var pedGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedGo.name = "StagePedestal";
            pedGo.transform.SetParent(transform, false);
            pedGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            pedGo.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);

            var col = pedGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = pedGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.12f, 0.16f, 0.22f);
                mat.SetFloat("_Smoothness", 0.8f);
                mr.material = mat;
            }
            stagePedestal = pedGo.transform;
        }
    }

    public void SetHair(int index)
    {
        if (customModel == null && characterModelInstance != null)
        {
            customModel = characterModelInstance.GetComponent<CharacterCustomModel>();
        }
        customModel?.SetHair(index);
    }

    public void SetEye(int index)
    {
        if (customModel == null && characterModelInstance != null)
        {
            customModel = characterModelInstance.GetComponent<CharacterCustomModel>();
        }
        customModel?.SetEye(index);
    }

    public void SetMouth(int index)
    {
        if (customModel == null && characterModelInstance != null)
        {
            customModel = characterModelInstance.GetComponent<CharacterCustomModel>();
        }
        customModel?.SetMouth(index);
    }

    public void ApplyCustomization(int hairIndex, int eyeIndex, int mouthIndex)
    {
        if (customModel == null && characterModelInstance != null)
        {
            customModel = characterModelInstance.GetComponent<CharacterCustomModel>();
        }
        customModel?.ApplyCustomization(hairIndex, eyeIndex, mouthIndex);
    }

    /// <summary>
    /// 수동 회전 제어 (드래그 시 호출)
    /// </summary>
    public void AddRotation(float deltaX)
    {
        targetYaw -= deltaX * rotationSensitivity;
    }

    public void RotateLeft(float angle = 45f)
    {
        targetYaw += angle;
    }

    public void RotateRight(float angle = 45f)
    {
        targetYaw -= angle;
    }

    public void ResetRotation()
    {
        targetYaw = 180f;
        currentYaw = 180f;
        if (characterRoot != null)
        {
            characterRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    public void SetAutoRotate(bool enabled)
    {
        autoRotate = enabled;
    }

    private void CleanupRenderTexture()
    {
        if (previewCamera != null && previewCamera.targetTexture == previewRenderTexture)
        {
            previewCamera.targetTexture = null;
        }

        if (previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            Destroy(previewRenderTexture);
            previewRenderTexture = null;
        }
    }
    #endregion
}
