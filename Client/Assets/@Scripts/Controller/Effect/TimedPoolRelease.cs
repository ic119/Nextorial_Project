using UnityEngine;

/// <summary>
/// PooledParticleEffect(ParticleSystem.IsAlive 기반 자동 반환)와 달리, 내부에 루프(loop=true)
/// 파티클을 포함하고 있어 IsAlive가 스스로 false가 되지 않는 이펙트(예: 지속되는 지면 마커가
/// 포함된 메테오 스트라이크)를 위해, 고정된 시간이 지나면 무조건 풀로 반환한다.
/// </summary>
public class TimedPoolRelease : MonoBehaviour, IPoolable
{
    [Tooltip("풀에서 대여된 뒤 이 시간(초)이 지나면 재생 상태와 무관하게 강제로 풀에 반환한다.")]
    [SerializeField] private float lifetime = 3.5f;

    /// <summary>풀에서 대여되어 활성화된 직후 호출된다.</summary>
    public void OnGetFromPool()
    {
        CancelInvoke(nameof(ReleaseSelf));
        Invoke(nameof(ReleaseSelf), lifetime);
    }

    /// <summary>풀로 반환되어 비활성화되기 직전에 호출된다.</summary>
    public void OnReleaseToPool()
    {
        CancelInvoke(nameof(ReleaseSelf));
    }

    private void ReleaseSelf()
    {
        ObjectPoolController.Instance?.Release(gameObject);
    }
}
