using DG.Tweening;
using UnityEngine;

public class PlayerEffect : MonoBehaviour
{
    [Header("Dash")]
    [SerializeField] private ParticleSystem dashParticle;

    [Header("Parry")]
    [SerializeField] private ParticleSystem parryParticle;

    private Player player;
    private Vector3 originalScale;

    public void Init()
    {
        player ??= GetComponent<Player>();
        originalScale = transform.localScale;
    }

    public void Setup() { }

    public void PlayDash(Vector3 dashDir)
    {
        if (dashParticle != null)
        {
            // 대시 방향의 반대로 파티클이 방출되도록 회전
            if (dashDir != Vector3.zero)
                dashParticle.transform.rotation = Quaternion.LookRotation(-dashDir);

            dashParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dashParticle.Play();
        }
    }

    public void PlayParry()
    {
        // 파티클
        if (parryParticle != null)
        {
            parryParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            parryParticle.Play();
        }

        // 순간 확대 → 원래 크기 복귀 (펀치 느낌)
        transform.DOKill();
        transform.localScale = originalScale;
        transform.DOPunchScale(originalScale * 0.4f, 0.2f, 5, 0.5f);
    }

    public void PlayHit()
    {
        UIManager.Instance.Get<UINaviPlayerHit>()?.PlayHit();
    }
}
