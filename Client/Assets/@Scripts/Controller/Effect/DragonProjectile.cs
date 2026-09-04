using UnityEngine;

/// <summary>
/// 캐릭터 위치에 고정 스폰되는 근접형 이펙트(PooledParticleEffect)와 달리, 발사된 지점에서
/// 지정된 방향으로 일정 속도만큼 스스로 이동하다가 사거리에 도달하면 임팩트 이펙트를 남기고
/// 풀로 돌아가는 원거리 투사체(예: 드래곤 파이어볼)에 사용한다.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class DragonProjectile : MonoBehaviour, IPoolable
{
    private ParticleSystem rootParticle;

    private Vector3 startPosition;
    private Vector3 moveDirection;
    private float moveSpeed;
    private float maxDistance;
    private string impactEffectKey;
    private bool isLaunched;

    private void Awake()
    {
        rootParticle = GetComponent<ParticleSystem>();
    }

    private void Update()
    {
        if (!isLaunched)
        {
            return;
        }

        transform.position += moveDirection * (moveSpeed * Time.deltaTime);

        if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
        {
            Impact();
        }
    }

    /// <summary>풀에서 대여되어 활성화된 직후 호출된다. 이전 비행의 잔여 파티클을 지우고 처음부터 재생한다.</summary>
    public void OnGetFromPool()
    {
        rootParticle.Clear(true);
        rootParticle.Play(true);
        isLaunched = false;
    }

    /// <summary>풀로 반환되어 비활성화되기 직전에 호출된다.</summary>
    public void OnReleaseToPool()
    {
        rootParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        isLaunched = false;
    }

    /// <summary>
    /// 소환 직후(ObjectPoolController.Get 호출 바로 뒤) 호출해 진행 방향/속도/사거리와
    /// 사거리 도달 시 남길 임팩트 이펙트의 Addressable 키를 설정하고 비행을 시작시킨다.
    /// </summary>
    public void Launch(Vector3 direction, float speed, float distance, string onImpactEffectKey)
    {
        startPosition = transform.position;
        moveDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        moveSpeed = speed;
        maxDistance = distance;
        impactEffectKey = onImpactEffectKey;
        isLaunched = true;
    }

    /// <summary>사거리 끝(또는 향후 충돌 판정)에 도달했을 때 임팩트 이펙트를 남기고 스스로 풀로 반환한다.</summary>
    private void Impact()
    {
        isLaunched = false;

        if (ObjectPoolController.Instance != null && !string.IsNullOrEmpty(impactEffectKey))
        {
            ObjectPoolController.Instance.Get(impactEffectKey, transform.position, transform.rotation);
        }

        ObjectPoolController.Instance?.Release(gameObject);
    }
}
