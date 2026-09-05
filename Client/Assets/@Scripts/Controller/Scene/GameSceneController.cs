using System.Collections.Generic;
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

    [Header("Monster Spawn Settings")]
    [Tooltip("몬스터를 스폰할 위치들. 비어있으면 characterSpawnPoint/defaultSpawnPosition 기준 monsterSpawnOffset 위치에 1마리만 스폰한다.")]
    [SerializeField] private Transform[] monsterSpawnPoints;
    [SerializeField] private Vector3 monsterSpawnOffset = new Vector3(3f, 0f, 0f);

    /// <summary>
    /// 유저 캐릭터와 드래곤이 GameScene에 스폰될 때 공통으로 바라볼 초기 방향(Vector3.right).
    /// PlayerController/DragonController의 오른쪽 이동 회전값(90도)과 동일하다.
    /// </summary>
    private static readonly Quaternion InitialFacingRotation = Quaternion.LookRotation(Vector3.right);



    private GameObject spawnedCharacter;
    private GameObject spawnedDragon;
    private DragonController spawnedDragonController;
    private readonly List<MonsterController> spawnedMonsterControllers = new List<MonsterController>();



    private UI_GameSceneView gameSceneView;

    [Header("Player Skill Data")]
    [Tooltip("A/S/D/F 스킬(이름/쿸타임/데미지) 데이터. Addressable로 로드되며 Inspector에서 직접 드래그할 필요는 없다.")]
private SkillDataModelSO skillDataModel;

    [Header("Dragon Skill Data")]
    [Tooltip("Q/W/E/R 스킬(이름/쿨타임/데미지) 데이터. Addressable로 로드되며 Inspector에서 직접 드래그할 필요는 없다.")]
    private DragonSkillDataModelSO dragonSkillDataModel;


    private KeyboardInputController spawnedKeyboardInput;
    private PlayerController spawnedPlayerController;


    private PlayerCharacterModel spawnedCharacterModel;
    private UserSaveData cachedUserData;
    #endregion

    #region LifeCycle
    private void Start()
    {
        SpawnPlayerCharacter();
        SpawnPlayerDragon();
        SpawnMonsters();
        LoadGameSceneUI();
        LoadSkillData();
        LoadDragonSkillData();
    }

    private void OnDestroy()
    {
if (spawnedKeyboardInput != null)
        {
            spawnedKeyboardInput.OnSkillKeyPressed -= HandleSkillKeyPressed;
            spawnedKeyboardInput.OnDragonSkillKeyPressed -= HandleDragonSkillKeyPressed;
        }
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
            characterModel?.ApplyCombatStat(userData.userStats);

            spawnedCharacterModel = characterModel;
            cachedUserData = userData;
            TryBindGameSceneView();
            TryWireDragonFollowTarget();
            TryWireMonsterTargets();

            // KeyboardInputController(SingletonObject)를 PlayerController보다 먼저 붙여, PlayerController.Awake()가
            // KeyboardInputController.Instance를 처음 참조할 때 이미 이 캐릭터에 붙은 인스턴스를 쓰도록 한다.
            // 순서가 바끈면(PlayerController 먼저) SingletonObject가 별도의 익명 GameObject를 먼저 만들어버리고,
            // 이후 여기서 AddComponent한 두 번째 인스턴스가 SingletonObject.Awake에서 자기 자신(spawnedCharacter)을 파괴해버린다.
            spawnedCharacter.AddComponent<KeyboardInputController>();
            spawnedKeyboardInput = spawnedCharacter.GetComponent<KeyboardInputController>();
spawnedKeyboardInput.OnSkillKeyPressed += HandleSkillKeyPressed;
            spawnedKeyboardInput.OnDragonSkillKeyPressed += HandleDragonSkillKeyPressed;

            SetupPhysics(spawnedCharacter);
            spawnedPlayerController = spawnedCharacter.AddComponent<PlayerController>();

            if (cameraZoomController != null)
            {
                cameraZoomController.SetTarget(spawnedPlayerController);
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
    /// monsterSpawnPoints가 지정되어 있으면 각 위치에 NormalMonster를 하나씩 스폰하고, 지정된 것이 없으면
    /// characterSpawnPoint/defaultSpawnPosition 기준 monsterSpawnOffset 위치에 1마리만 스폰한다.
    /// 몬스터는 PlayerCharacter/PlayerDragon과 달리 스폰 이후 개별 참조를 유지할 필요가 없다(체력/사망 처리는 MonsterModel이 자체적으로 처리한다).
    /// </summary>
    private void SpawnMonsters()
    {
        if (AddressableAssetController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>("AddressableAssetController.Instance가 없어 몬스터를 스폰할 수 없습니다.");
            return;
        }

        string key = AddressableKey.NormalMonster.ToString();
        Vector3[] spawnPositions = GetMonsterSpawnPositions();

        foreach (Vector3 spawnPosition in spawnPositions)
        {
            AddressableAssetController.Instance.LoadPrefabAddress<GameObject>(key, prefab =>
            {
                if (prefab == null)
                {
                    DebugLogController.GenerateErrorMessage<GameSceneController>($"몬스터 프리팹 로드 실패 Key : {key}");
                    return;
                }

                GameObject monster = AddressableAssetController.Instance.InstantiatePrefab(prefab);
                monster.name = "NormalMonster";
                monster.transform.SetPositionAndRotation(spawnPosition, InitialFacingRotation);

                MonsterController monsterController = monster.GetComponent<MonsterController>();
                if (monsterController != null)
                {
                    spawnedMonsterControllers.Add(monsterController);
                    TryWireMonsterTargets();
                }

                var animator = monster.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                }

                var skinnedRenderers = monster.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < skinnedRenderers.Length; i++)
                {
                    skinnedRenderers[i].updateWhenOffscreen = true;
                }

                DebugLogController.GenerateLogMessage<GameSceneController>($"몬스터 스폰 완료: {monster.name} at {spawnPosition}");
            });
        }
    }

    /// <summary>
    /// monsterSpawnPoints가 비어있으면 characterSpawnPoint/defaultSpawnPosition에 monsterSpawnOffset만큼 띄운 위치 1개만 반환하고,
    /// 지정되어 있으면 각 Transform의 위치를 그대로 반환한다.
    /// </summary>
    private Vector3[] GetMonsterSpawnPositions()
    {
        if (monsterSpawnPoints != null && monsterSpawnPoints.Length > 0)
        {
            Vector3[] positions = new Vector3[monsterSpawnPoints.Length];
            for (int i = 0; i < monsterSpawnPoints.Length; i++)
            {
                positions[i] = monsterSpawnPoints[i] != null ? monsterSpawnPoints[i].position : defaultSpawnPosition;
            }
            return positions;
        }

        Vector3 basePosition = characterSpawnPoint != null ? characterSpawnPoint.position : defaultSpawnPosition;
        return new[] { basePosition + monsterSpawnOffset };
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
            TryApplySkillIcons();
            TryApplyDragonSkillIcons();
        });
    }

/// <summary>
    /// 플레이어 스킬(A/S/D/F) 데이터(스킬명/쿸타임/데미지)를 담은 SkillDataModelSO를 Addressable로 로드한다.
    /// 캐릭터 스폰과는 독립적인 비동기 로드이며, 로드가 끝나기 전에 스킬 키가 눌리면 해당 입력은 무시된다.
    /// </summary>
    private void LoadSkillData()
    {
        if (AddressableAssetController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>("AddressableAssetController.Instance가 없어 스킬 데이터를 로드할 수 없습니다.");
            return;
        }

        string key = AddressableKey.SkillDataModelSO.ToString();

        AddressableAssetController.Instance.LoadPrefabAddress<SkillDataModelSO>(key, so =>
        {
            if (so == null)
            {
                DebugLogController.GenerateErrorMessage<GameSceneController>($"스킬 데이터 로드 실패 Key : {key}");
                return;
            }

            skillDataModel = so;
            TryApplySkillIcons();
        });
    }


    /// <summary>
    /// 드래곤 스킬(Q/W/E/R) 데이터(스킬명/쿨타임/데미지)를 담은 DragonSkillDataModelSO를 Addressable로 로드한다.
    /// 드래곤 스폰과는 독립적인 비동기 로드이며, 로드가 끝나기 전에 드래곤 스킬 키가 눌리면 해당 입력은 무시된다.
    /// </summary>
    private void LoadDragonSkillData()
    {
        if (AddressableAssetController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>("AddressableAssetController.Instance가 없어 드래곤 스킬 데이터를 로드할 수 없습니다.");
            return;
        }

        string key = AddressableKey.DragonSkillDataModelSO.ToString();

        AddressableAssetController.Instance.LoadPrefabAddress<DragonSkillDataModelSO>(key, so =>
        {
            if (so == null)
            {
                DebugLogController.GenerateErrorMessage<GameSceneController>($"드래곤 스킬 데이터 로드 실패 Key : {key}");
                return;
            }

            dragonSkillDataModel = so;
            TryApplyDragonSkillIcons();
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
    /// UI와 스킬 데이터가 둘 다 준비된 경우에만 슬롯(A/S/D/F) 아이콘을 연결한다.
    /// 어느 쪽이 먼저 끝나도 동작하도록 양쪽 로드 완료 콜백에서 이 메서드를 호출한다.
    /// </summary>
    private void TryApplySkillIcons()
    {
        if (gameSceneView == null || skillDataModel == null)
        {
            return;
        }

        gameSceneView.ApplySkillIcons(skillDataModel);
    }


    /// <summary>
    /// UI와 드래곤 스킬 데이터가 둘 다 준비된 경우에만 슬롯(Q/W/E/R) 아이콘을 연결한다.
    /// 어느 쪽이 먼저 끝나도 동작하도록 양쪽 로드 완료 콜백에서 이 메서드를 호출한다.
    /// </summary>
    private void TryApplyDragonSkillIcons()
    {
        if (gameSceneView == null || dragonSkillDataModel == null)
        {
            return;
        }

        gameSceneView.ApplyDragonSkillIcons(dragonSkillDataModel);
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
    /// 캐릭터 스폰과 몬스터 스폰(둘 다 독립적인 비동기 Addressable 로드)의 완료 순서가 보장되지 않으므로,
    /// 유저 캐릭터가 준비되어 있으면 그 시점까지 스폰된 모든 몬스터에게 추적 대상을 연결해준다.
    /// 몬스터가 나중에 추가로 스폰되는 경우를 대비해 캐릭터 스폰 완료 시점과 몬스터 스폰 완료 시점
    /// 양쪽에서 이 메서드를 호출한다.
    /// </summary>
    private void TryWireMonsterTargets()
    {
        if (spawnedCharacter == null)
        {
            return;
        }

        foreach (MonsterController monsterController in spawnedMonsterControllers)
        {
            monsterController?.SetTarget(spawnedCharacter.transform);
        }
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


/// <summary>
    /// KeyboardInputController에서 A/S/D/F 스킬 키가 눌렸을 때 호출된다. UI_GameSceneView의
    /// 쿨타임 기능을 호출해 해당 슬롯을 쿨타임 상태로 전환하고, 성공하면 PlayerController.PlaySkillAnimation으로 애니메이션 재생과 데미지 판정을 함께 요청한다.
    /// </summary>
    private void HandleSkillKeyPressed(UI_GameSceneView.PlayerSkillSlot slot)
    {
        if (gameSceneView == null)
        {
            return;
        }

        SkillData skill = skillDataModel != null ? skillDataModel.GetSkill(slot) : null;
        if (skill == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>($"슬롯 {slot}에 대응하는 스킬 데이터를 찾을 수 없습니다.");
            return;
        }

        bool started = gameSceneView.TryStartPlayerSkillCooldown(slot, skill.cooldown);
        if (!started)
        {
            return;
        }

        spawnedPlayerController?.PlaySkillAnimation(slot, skill.damage);

        DebugLogController.GenerateLogMessage<GameSceneController>(
            $"스킬 사용: {skill.skillName} (슬롯:{slot}, 데미지:{skill.damage}, 쿸타임:{skill.cooldown}초)");
    }


    /// <summary>
    /// KeyboardInputController에서 Q/W/E/R 드래곤 스킬 키가 눌렸을 때 호출된다. UI_GameSceneView의
    /// 쿨타임 기능을 호출해 해당 슬롯을 쿨타임 상태로 전환하고, 성공하면 DragonController의 스킬 애니메이션을 재생한다.
    /// </summary>
    private void HandleDragonSkillKeyPressed(DragonSkillSlot slot)
    {
        if (gameSceneView == null)
        {
            return;
        }

        DragonSkillData skill = dragonSkillDataModel != null ? dragonSkillDataModel.GetSkill(slot) : null;
        if (skill == null)
        {
            DebugLogController.GenerateErrorMessage<GameSceneController>($"드래곤 슬롯 {slot}에 대응하는 스킬 데이터를 찾을 수 없습니다.");
            return;
        }

        bool started = gameSceneView.TryStartDragonSkillCooldown(slot, skill.cooldown);
        if (!started)
        {
            return;
        }

        spawnedDragonController?.PlaySkillAnimation(slot);

        DebugLogController.GenerateLogMessage<GameSceneController>(
            $"드래곤 스킬 사용: {skill.skillName} (슬롯:{slot}, 데미지:{skill.damage}, 쿨타임:{skill.cooldown}초)");
    }

}
