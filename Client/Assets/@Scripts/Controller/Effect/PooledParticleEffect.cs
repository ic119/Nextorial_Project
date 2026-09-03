using System.Collections;
using UnityEngine;

/// <summary>
/// 오브젝트 풀에서 대여된 원샷(looping = false) ParticleSystem 이펙트가 재생을 마치면
/// 자동으로 ObjectPoolController에 반환한다. Slash_Normal처럼 여러 자식 ParticleSystem이
/// 합쳐진 이펙트도 ParticleSystem.IsAlive(withChildren: true)로 전체 재생 종료 시점을 판단한다.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class PooledParticleEffect : MonoBehaviour, IPoolable
{
    #region Variable
    private ParticleSystem rootParticle;
    private Coroutine returnRoutine;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        rootParticle = GetComponent<ParticleSystem>();
    }
    #endregion

    #region Method
    /// <summary>
    /// 풀에서 대여되어 활성화된 직후 호출된다. 이전 재생 잔여물을 지우고 처음부터 다시 재생한다.
    /// </summary>
    public void OnGetFromPool()
    {
        rootParticle.Clear(true);
        rootParticle.Play(true);

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
        }
        returnRoutine = StartCoroutine(ReturnWhenFinished());
    }

    /// <summary>
    /// 풀로 반환되어 비활성화되기 직전에 호출된다. 재생 중이던 파티클과 대기 중인 반환 코루틴을 정리한다.
    /// </summary>
    public void OnReleaseToPool()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        rootParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// 이 파티클(및 모든 자식)의 재생이 완전히 끝날 때까지 기다린 뒤 풀로 반환한다.
    /// Play() 직후 한 프레임은 IsAlive가 과도기적으로 false를 반환할 수 있어 한 프레임 대기 후 검사한다.
    /// </summary>
    private IEnumerator ReturnWhenFinished()
    {
        yield return null;

        while (rootParticle.IsAlive(true))
        {
            yield return null;
        }

        returnRoutine = null;
        ObjectPoolController.Instance?.Release(gameObject);
    }
    #endregion
}
