using UnityEngine;

public class GameSceneController : MonoBehaviour
{
    #region Variable
    [Header("Camera Settings")]
    [SerializeField] private CameraZoomController cameraZoomController;

    [Header("Spawn Settings")]
    [SerializeField] private Transform characterSpawnPoint;
    [SerializeField] private Vector3 defaultSpawnPosition = Vector3.zero;

    [Header("Dragon Spawn Settings")]
    [SerializeField] private Transform dragonSpawnPoint;
    [SerializeField] private Vector3 dragonSpawnOffset = new Vector3(-2f, 0f, 0f);

    /// <summary>
    /// 유저 캐릭터와 드래곤이 GameScene에 스폰될 때 공통으로 바라볼 초기 방향(Vector3.right).
    /// PlayerController/DragonController의 오른쪽 이동 회전값(90도)과 동일하다.
    /// </summary>
    private static readonly Quaternion InitialFacingRotation = Quaternion.LookRotation(Vector3.right);



    private GameObject spawnedCharacter;
    private GameObject spawnedDragon;
    private DragonController spawnedDragonController;



    private UI_GameSceneView gameSceneView;
    private PlayerCharacterModel spawnedCharacterModel;
    private UserSaveData cachedUserData;
    #endregion

    #region LifeCycle
private void Start()
    {
        SpawnPlayerCharacter();
        SpawnPlayerDragon();
        LoadGameSceneUI();
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
            Quaternion spawnRotation = InitialFacingRotation;

            spawnedCharacter = AddressableAssetController.Instance.InstantiatePrefab(prefab);
            spawnedCharacter.name = "PlayerCharacter";
            spawnedCharacter.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            var customModel = spawnedCharacter.GetComponent<CharacterCustomModel>();
            customModel?.ApplyCustomization(userData.hairIndex, userData.eyeIndex, userData.mouthIndex);

            var characterModel = spawnedCharacter.GetComponent<PlayerCharacterModel>();
            characterModel?.ApplyHealth(userData.maxHp, userData.currentHp);
            characterModel?.ApplyExp(userData.userExp);

            spawnedCharacterModel = characterModel;
            cachedUserData = userData;
            TryBindGameSceneView();
            TryWireDragonFollowTarget();


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

            var animator = spawnedCharacter.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            var skinnedRenderers = spawnedCharacter.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                skinnedRenderers[i].updateWhenOffscreen = true;
            }

            DebugLogController.GenerateLogMessage<GameSceneController>(
                $"플레이어 캐릭터 스폰 완료: {userData.userID} (헤어:{userData.hairIndex}, 눈:{userData.eyeIndex}, 입:{userData.mouthIndex}, HP:{userData.currentHp}/{userData.maxHp})");
        });
    }

/// <summary>
    /// 유저 캐릭터와 함께 등장하는 드래곤 동료(BasicDragon)를 스폰한다.
    /// 캐릭터 생성 시 LobbySceneController가 항상 DragonSaveData를 함께 저장하므로,
    /// 세이브된 캐릭터가 있다면 드래곤도 항상 존재한다는 전제로 동작한다.
    /// SpawnPlayerCharacter와는 독립적인 비동기 Addressable 로드이며, 스폰 위치는
    /// dragonSpawnPoint(지정 시) 또는 characterSpawnPoint/defaultSpawnPosition 기준 오프셋으로
    /// 계산하므로 캐릭터 인스턴스가 먼저 준비될 필요는 없다.
    /// </summary>
private void SpawnPlayerDragon()
    {
        DragonSaveData dragonData = SaveDataController.Instance != null ? SaveDataController.Instance.CurrentData?.dragon : null;

        if (dragonData == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>("저장된 드래곤 데이터가 없어 드래곤을 스폰할 수 없습니다.");
            return;
        }

        if (AddressableAssetController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>("AddressableAssetController.Instance가 없어 드래곤을 스폰할 수 없습니다.");
            return;
        }

        string key = AddressableKey.BasicDragon.ToString();

        AddressableAssetController.Instance.LoadPrefabAddress<GameObject>(key, prefab =>
        {
            if (prefab == null)
            {
                DebugLogController.GenerateErrorMessage<GameSceneController>($"드래곤 프리팹 로드 실패 Key : {key}");
                return;
            }

            if (spawnedDragon != null)
            {
                return;
            }

            Vector3 basePosition = characterSpawnPoint != null ? characterSpawnPoint.position : defaultSpawnPosition;

            Vector3 spawnPosition = dragonSpawnPoint != null ? dragonSpawnPoint.position : basePosition + dragonSpawnOffset;
            Quaternion spawnRotation = InitialFacingRotation;

            spawnedDragon = AddressableAssetController.Instance.InstantiatePrefab(prefab);
            spawnedDragon.name = "PlayerDragon";
            spawnedDragon.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            spawnedDragonController = spawnedDragon.GetComponent<DragonController>();
            TryWireDragonFollowTarget();


            var animator = spawnedDragon.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            var skinnedRenderers = spawnedDragon.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                skinnedRenderers[i].updateWhenOffscreen = true;
            }

            DebugLogController.GenerateLogMessage<GameSceneController>(
                $"드래곤 스폰 완료: {dragonData.dragonID} (Lv.{dragonData.dragonLevel})");
        });
    }


    /// <summary>
    /// UI_GameScene(닉네임/HP/EXP 표시)을 Addressable로 로드해 인스턴스화한다.
    /// 캐릭터 스폰과 별개의 비동기 로드라 완료 순서가 보장되지 않으므로,
    /// 캐릭터/UI 중 나중에 준비되는 쪽에서 TryBindGameSceneView로 실제 바인딩을 시도한다.
    /// </summary>
private void LoadGameSceneUI()
    {
        if (AddressableAssetController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>("AddressableAssetController.Instance가 없어 UI_GameScene을 로드할 수 없습니다.");
            return;
        }

        AddressableAssetController.Instance.LoadAndBindUI<UI_GameSceneView>(AddressableKey.UI_GameScene, view =>
        {
            gameSceneView = view;
            TryBindGameSceneView();
        });
    }

    /// <summary>
    /// 캐릭터 스폰과 UI 로드가 둘 다 끝났을 때만 실제 바인딩을 수행한다.
    /// 어느 쪽이 먼저 끝나도 동작하도록 양쪽 완료 콜백에서 이 메서드를 호출한다.
    /// </summary>
    private void TryBindGameSceneView()
    {
        if (gameSceneView == null || spawnedCharacterModel == null || cachedUserData == null)
        {
            return;
        }

        gameSceneView.Bind(cachedUserData, spawnedCharacterModel);
    }

/// <summary>
    /// 캐릭터와 드래곤 스폰(둘 다 독립적인 비동기 Addressable 로드)이 모두 끝난 경우에만
    /// 드래곤의 추적 대상을 유저 캐릭터로 연결한다. 어느 쪽이 먼저 끝나도 동작하도록
    /// 양쪽 완료 콜백에서 이 메서드를 호출한다.
    /// </summary>
    private void TryWireDragonFollowTarget()
    {
        if (spawnedDragonController == null || spawnedCharacter == null)
        {
            return;
        }

        spawnedDragonController.SetFollowTarget(spawnedCharacter.transform);
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
