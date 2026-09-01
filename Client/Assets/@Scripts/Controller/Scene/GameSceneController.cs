using UnityEngine;

public class GameSceneController : MonoBehaviour
{
    #region Variable
    [Header("Camera Settings")]
    [SerializeField] private CameraZoomController cameraZoomController;

    [Header("Spawn Settings")]
    [SerializeField] private Transform characterSpawnPoint;
    [SerializeField] private Vector3 defaultSpawnPosition = Vector3.zero;

    private GameObject spawnedCharacter;
    #endregion

    #region LifeCycle
    private void Start()
    {
        SpawnPlayerCharacter();
    }
    #endregion

    #region Method
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
                return;
            }

            Vector3 spawnPosition = characterSpawnPoint != null ? characterSpawnPoint.position : defaultSpawnPosition;
            Quaternion spawnRotation = characterSpawnPoint != null ? characterSpawnPoint.rotation : Quaternion.identity;

            spawnedCharacter = AddressableAssetController.Instance.InstantiatePrefab(prefab);
            spawnedCharacter.name = "PlayerCharacter";
            spawnedCharacter.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            var customModel = spawnedCharacter.GetComponent<CharacterCustomModel>();
            customModel?.ApplyCustomization(userData.hairIndex, userData.eyeIndex, userData.mouthIndex);

            SetupPhysics(spawnedCharacter);
            var playerController = spawnedCharacter.AddComponent<PlayerController>();

            if (cameraZoomController != null)
            {
                cameraZoomController.SetTarget(playerController);
            }
            else
            {
                DebugLogController.GenerateErrorMessage<GameSceneController>("cameraZoomController가 지정되지 않아 카메라 추적을 설정할 수 없습니다.");
            }

            // 이동/충돌은 Rigidbody + PlayerController가 물리적으로 처리하므로, Root Motion이 켜진 채로 두면
            // 애니메이션(Idle 등)에 섞인 미세한 루트 모션이 그 위에 겹쳐져 위치가 어긋날 수 있다.
            // Animator/Avatar 자체는 건드리지 않고 런타임에서만 비활성화한다.
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

    /// <summary>
    /// MapTile(BoxCollider) 지형과 충돌할 수 있도록 Rigidbody/CapsuleCollider를 부착한다.
    /// 캐릭터 루트가 발 위치(y=0)를 기준으로 하고 있어, 콜라이더도 그 기준으로 높이 1.8을 잡는다.
    /// Z축은 고정 스테이지 설계상 항상 같은 깊이에 있어야 하므로 물리적으로도 잠근다.
    /// </summary>
    private static void SetupPhysics(GameObject character)
    {
        var collider = character.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.9f, 0f);
        collider.height = 1.8f;
        collider.radius = 0.3f;

        var rigidbody = character.AddComponent<Rigidbody>();
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }
    #endregion
}
