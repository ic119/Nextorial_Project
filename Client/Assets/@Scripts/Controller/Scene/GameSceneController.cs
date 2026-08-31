using UnityEngine;

public class GameSceneController : MonoBehaviour
{
    #region Variable
    [Header("Spawn Settings")]
    [SerializeField] private Transform characterSpawnPoint;
    [SerializeField] private Vector3 defaultSpawnPosition = Vector3.zero;

    private GameObject spawnedCharacter;
    #endregion

    #region LifeCycle
    private void Start()
    {
        EnsureGroundPlane();
        SpawnPlayerCharacter();
    }
    #endregion

    #region Method
    /// <summary>
    /// 아직 GameScene에 아무 지형도 없어서, 캐릭터가 허공에 뜨지 않도록 최소한의 바닥을 만든다.
    /// 실제 환경 에셋으로 교체되기 전까지의 임시 플레이스홀더.
    /// </summary>
    private void EnsureGroundPlane()
    {
        if (GameObject.Find("GroundPlaceholder") != null)
        {
            return;
        }

        var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundGo.name = "GroundPlaceholder";
        groundGo.transform.position = Vector3.zero;
        groundGo.transform.localScale = new Vector3(5f, 1f, 5f);

        var renderer = groundGo.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.35f, 0.2f);
            renderer.material = mat;
        }
    }

    /// <summary>
    /// 저장된 유저 데이터(hairIndex/eyeIndex/mouthIndex)로 커스터마이징된 BasicCharacter를
    /// GameScene 월드에 실제로 스폰한다. 로비 캐릭터 생성 팝업의 프리뷰에서만 보이던 캐릭터를
    /// 처음으로 실제 게임 월드에 등장시키는 지점이다.
    /// </summary>
    private void SpawnPlayerCharacter()
    {
        UserSaveData userData = SaveDataController.Instance != null ? SaveDataController.Instance.CurrentData?.user : null;

        if (userData == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>("저장된 유저 데이터가 없어 캐릭터를 스폰할 수 없습니다.");
            return;
        }

        if (AddressableAssetController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>("AddressableAssetController.Instance가 없어 캐릭터를 스폰할 수 없습니다.");
            return;
        }

        string key = AddressableKey.BasicCharacter.ToString();

        AddressableAssetController.Instance.LoadPrefabAddress<GameObject>(key, prefab =>
        {
            if (prefab == null)
            {
                DebugLogController.GenerateErrorMessage<GameSceneController>($"캐릭터 프리팹 로드 실패 Key : {key}");
                return;
            }

            if (spawnedCharacter != null)
            {
                // 이미 스폰된 뒤 콜백이 중복 도착한 경우 등, 재스폰을 방지한다.
                return;
            }

            Vector3 spawnPosition = characterSpawnPoint != null ? characterSpawnPoint.position : defaultSpawnPosition;
            Quaternion spawnRotation = characterSpawnPoint != null ? characterSpawnPoint.rotation : Quaternion.identity;

            spawnedCharacter = AddressableAssetController.Instance.InstantiatePrefab(prefab);
            spawnedCharacter.name = "PlayerCharacter";
            spawnedCharacter.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            var customModel = spawnedCharacter.GetComponent<CharacterCustomModel>();
            customModel?.ApplyCustomization(userData.hairIndex, userData.eyeIndex, userData.mouthIndex);

            // 아직 플레이어 이동/입력 로직이 없는 단계라, Root Motion이 켜진 채로 두면 애니메이션(Idle 등)에
            // 섞인 미세한 루트 모션만으로도 캐릭터가 스폰 위치를 벗어나 카메라 프러스텀 밖으로 나가버릴 수 있다.
            // Animator/Avatar 자체는 건드리지 않고, 실제 이동 시스템이 생기기 전까지만 런타임에서 비활성화한다.
            var animator = spawnedCharacter.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            // SkinnedMeshRenderer는 기본적으로 바인드 포즈 기준으로 계산된 바운드를 캐싱해두기 때문에,
            // 스폰 위치가 그 바운드 밖(카메라 프러스텀 판정 기준)이면 실제로는 보여야 할 메쉬가
            // 컬링되어 안 보이는 경우가 있다. 매 프레임 바운드를 재계산하도록 강제해 이를 방지한다.
            var skinnedRenderers = spawnedCharacter.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                skinnedRenderers[i].updateWhenOffscreen = true;
            }

            DebugLogController.GenerateLogMessage<GameSceneController>(
                $"플레이어 캐릭터 스폰 완료: {userData.userID} (헤어:{userData.hairIndex}, 눈:{userData.eyeIndex}, 입:{userData.mouthIndex})");
        });
    }
    #endregion
}
