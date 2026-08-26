using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 3D 캐릭터 실시간 프리뷰를 위한 스테이지, 카메라, 라이팅 및 3D 모델 관리 클래스
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

    [Header("Character Models")]
    [SerializeField] private GameObject maleCharacterModel;
    [SerializeField] private GameObject femaleCharacterModel;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSensitivity = 0.5f;
    [SerializeField] private float autoRotationSpeed = 10f;
    [SerializeField] private bool autoRotate = false;

    private RenderTexture previewRenderTexture;
    private Gender currentGender = Gender.Male;
    private float currentYaw = 180f;
    private float targetYaw = 180f;

    public RenderTexture PreviewTexture => previewRenderTexture;
    public Gender CurrentGender => currentGender;
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

        SetGender(currentGender);
        ResetRotation();

        return previewRenderTexture;
    }

    private void InitializeStage()
    {
        if (previewCamera == null)
        {
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 2.7f);
            camGo.transform.localRotation = Quaternion.Euler(5f, 180f, 0f);
            previewCamera = camGo.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 0f);
            previewCamera.fieldOfView = 36f;
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

        // 3D 캐릭터 모델이 없으면 기본 3D 캐릭터 생성
        EnsureCharacterModels();

        // 조명 설정
        EnsureLights();

        // 스테이지 발판 생성
        EnsurePedestal();
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
            keyLight.intensity = 1.2f;
            keyLight.color = new Color(1f, 0.96f, 0.9f);
        }

        if (fillLight == null)
        {
            var fillGo = new GameObject("PreviewFillLight");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(-2f, 1.5f, -1f);
            fillGo.transform.localRotation = Quaternion.Euler(20f, 40f, 0f);
            fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.6f;
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

    private void EnsureCharacterModels()
    {
        if (maleCharacterModel == null)
        {
            maleCharacterModel = CreateStylizedCharacter(Gender.Male);
            maleCharacterModel.transform.SetParent(characterRoot, false);
        }

        if (femaleCharacterModel == null)
        {
            femaleCharacterModel = CreateStylizedCharacter(Gender.Female);
            femaleCharacterModel.transform.SetParent(characterRoot, false);
        }
    }

    private GameObject CreateStylizedCharacter(Gender gender)
    {
        var charGo = new GameObject(gender == Gender.Male ? "MaleCharacter_3D" : "FemaleCharacter_3D");
        charGo.transform.localPosition = Vector3.zero;

        Color bodyColor = gender == Gender.Male ? new Color(0.2f, 0.45f, 0.85f) : new Color(0.9f, 0.35f, 0.55f);
        Color accentColor = gender == Gender.Male ? new Color(0.1f, 0.25f, 0.5f) : new Color(0.7f, 0.15f, 0.35f);
        Color skinColor = new Color(1f, 0.85f, 0.75f);
        Color hairColor = gender == Gender.Male ? new Color(0.15f, 0.15f, 0.18f) : new Color(0.6f, 0.3f, 0.15f);

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material skinMat = new Material(litShader) { color = skinColor };
        Material bodyMat = new Material(litShader) { color = bodyColor };
        Material accentMat = new Material(litShader) { color = accentColor };
        Material hairMat = new Material(litShader) { color = hairColor };

        // 머리 (Head)
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(charGo.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        head.transform.localScale = new Vector3(0.35f, 0.38f, 0.35f);
        head.GetComponent<MeshRenderer>().material = skinMat;
        RemoveCollider(head);

        // 머리카락 (Hair)
        var hair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hair.name = "Hair";
        hair.transform.SetParent(head.transform, false);
        hair.transform.localPosition = new Vector3(0f, 0.15f, -0.05f);
        hair.transform.localScale = gender == Gender.Male ? new Vector3(1.08f, 0.9f, 1.1f) : new Vector3(1.15f, 1.3f, 1.25f);
        hair.GetComponent<MeshRenderer>().material = hairMat;
        RemoveCollider(hair);

        // 몸통 (Torso)
        var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Torso";
        torso.transform.SetParent(charGo.transform, false);
        torso.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        torso.transform.localScale = gender == Gender.Male ? new Vector3(0.48f, 0.45f, 0.3f) : new Vector3(0.4f, 0.42f, 0.28f);
        torso.GetComponent<MeshRenderer>().material = bodyMat;
        RemoveCollider(torso);

        // 벨트 / 악센트 (Belt)
        var belt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        belt.name = "Belt";
        belt.transform.SetParent(charGo.transform, false);
        belt.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        belt.transform.localScale = gender == Gender.Male ? new Vector3(0.46f, 0.04f, 0.32f) : new Vector3(0.38f, 0.04f, 0.28f);
        belt.GetComponent<MeshRenderer>().material = accentMat;
        RemoveCollider(belt);

        // 팔 (Left / Right Arms)
        CreateLimb(charGo.transform, "LeftArm", new Vector3(-0.32f, 1.05f, 0f), new Vector3(0.12f, 0.42f, 0.12f), skinMat);
        CreateLimb(charGo.transform, "RightArm", new Vector3(0.32f, 1.05f, 0f), new Vector3(0.12f, 0.42f, 0.12f), skinMat);

        // 다리 (Left / Right Legs)
        float legOffset = gender == Gender.Male ? 0.14f : 0.11f;
        CreateLimb(charGo.transform, "LeftLeg", new Vector3(-legOffset, 0.45f, 0f), new Vector3(0.15f, 0.48f, 0.15f), accentMat);
        CreateLimb(charGo.transform, "RightLeg", new Vector3(legOffset, 0.45f, 0f), new Vector3(0.15f, 0.48f, 0.15f), accentMat);

        return charGo;
    }

    private void CreateLimb(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var limb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        limb.name = name;
        limb.transform.SetParent(parent, false);
        limb.transform.localPosition = pos;
        limb.transform.localScale = scale;
        limb.GetComponent<MeshRenderer>().material = mat;
        RemoveCollider(limb);
    }

    private void RemoveCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }
    }

    /// <summary>
    /// 성별 변경에 따른 3D 모델 실시간 교체
    /// </summary>
    public void SetGender(Gender gender)
    {
        currentGender = gender;
        if (maleCharacterModel != null)
        {
            maleCharacterModel.SetActive(gender == Gender.Male);
        }
        if (femaleCharacterModel != null)
        {
            femaleCharacterModel.SetActive(gender == Gender.Female);
        }
    }

    /// <summary>
    /// 수동 회전 제어 (드래그 시 호출)
    /// </summary>
    public void AddRotation(float deltaX)
    {
        targetYaw -= deltaX * rotationSensitivity;
    }

    /// <summary>
    /// 좌측 회전 버튼
    /// </summary>
    public void RotateLeft(float angle = 45f)
    {
        targetYaw += angle;
    }

    /// <summary>
    /// 우측 회전 버튼
    /// </summary>
    public void RotateRight(float angle = 45f)
    {
        targetYaw -= angle;
    }

    /// <summary>
    /// 정면 회전 리셋
    /// </summary>
    public void ResetRotation()
    {
        targetYaw = 180f;
        currentYaw = 180f;
        if (characterRoot != null)
        {
            characterRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    /// <summary>
    /// 자동 회전 토글
    /// </summary>
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
