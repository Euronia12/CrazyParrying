using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleCanon : ObjectPoolBase
{
    [Header("Data")]
    [SerializeField] private ObstacleCannonData data;

    [Header("Components")]
    [SerializeField] private Transform barrelTransform;
    [SerializeField] private Transform firePoint;

    [Header("Barrel")]
    [SerializeField] private float launchAngle = 45f;

    [Header("Barrel Recoil")]
    [SerializeField] private float recoilDistance        = 0.3f;
    [SerializeField] private float recoilBackDuration    = 0.06f;
    [SerializeField] private float recoilForwardDuration = 0.14f;

    [Header("Target Selection")]
    [SerializeField] private float predictLeadTime    = 1.2f;
    [SerializeField] private float nearbyScatterRadius = 3f;
    [SerializeField] private float minTargetSpacing   = 2.5f;

    private Transform playerTransform;
    private Vector3   prevPlayerPos;
    private Vector3   estimatedPlayerVelocity;
    private float     explosionRadius;
    private float     alertRadius;
    private float     balloonSpeed;
    private float     balloonMinTime;
    private float     balloonMaxTime;

    private static readonly Quaternion s_defaultBarrelRot = Quaternion.identity;

    private Tween          rotateTween;
    private Tween          resetTween;
    private Tween          recoilTween;
    private Coroutine      fireCoroutine;
    private Coroutine      initCoroutine;

    private Vector3        barrelOriginLocalPos;

    private readonly List<Vector3> usedTargets      = new List<Vector3>();
    private WaitForSeconds         waitInitDelay;
    private WaitForSeconds         waitShotDelay;
    private WaitForSeconds         waitFireInterval;
    private static readonly WaitForSeconds s_waitPlayerNull = new WaitForSeconds(1f);

    public override void OnSpawn()
    {
        SetActive(true);
        if (!isInit) Init();

        barrelTransform.rotation      = s_defaultBarrelRot;
        barrelOriginLocalPos          = barrelTransform.localPosition;

        if (initCoroutine != null) StopCoroutine(initCoroutine);
        initCoroutine = StartCoroutine(InitNextFrame());
    }

    private IEnumerator InitNextFrame()
    {
        yield return null;

        if (data == null)            { Debug.LogError("[ObstacleCanon] data is null");          yield break; }
        if (Player.Instance == null) { Debug.LogError("[ObstacleCanon] Player.Instance is null"); yield break; }

        playerTransform         = Player.Instance.transform;
        prevPlayerPos           = playerTransform.position;
        estimatedPlayerVelocity = Vector3.zero;
        waitInitDelay           = new WaitForSeconds(Mathf.Max(data.fireInterval, 2f));
        waitShotDelay           = new WaitForSeconds(data.shotDelay);
        waitFireInterval        = new WaitForSeconds(data.fireInterval);
        initCoroutine           = null;

        fireCoroutine = StartCoroutine(FireLoop());
    }

    public void SetData(ObstacleCannonData cannonData, float mapExplosionRadius, float mapAlertRadius,
                        float speed = 0f, float minTime = 0f, float maxTime = 0f)
    {
        data            = cannonData;
        explosionRadius = mapExplosionRadius;
        alertRadius     = mapAlertRadius;
        balloonSpeed    = speed;
        balloonMinTime  = minTime;
        balloonMaxTime  = maxTime;
    }

    private void Update()
    {
        if (playerTransform == null || Time.deltaTime <= 0f) return;
        Vector3 rawVel              = (playerTransform.position - prevPlayerPos) / Time.deltaTime;
        estimatedPlayerVelocity     = Vector3.Lerp(estimatedPlayerVelocity, rawVel, Time.deltaTime * 8f);
        prevPlayerPos               = playerTransform.position;
    }

    private IEnumerator FireLoop()
    {
        yield return waitInitDelay;

        while (true)
        {
            if (playerTransform == null) { yield return s_waitPlayerNull; continue; }
            if (Player.Instance != null && Player.Instance.status.IsDead) { yield return s_waitPlayerNull; continue; }

            usedTargets.Clear();

            for (int i = 0; i < data.shotsPerBurst; i++)
            {
                Vector3? targetPos = GetTargetPosition(usedTargets);
                if (targetPos.HasValue)
                {
                    usedTargets.Add(targetPos.Value);
                    yield return RotateBarrelTo(targetPos.Value);
                    FireAt(targetPos.Value);
                }
                yield return waitShotDelay;
            }

            yield return ResetBarrel();
            yield return waitFireInterval;
        }
    }

    private IEnumerator RotateBarrelTo(Vector3 targetPos)
    {
        Vector3 flat = targetPos - firePoint.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
        flat.Normalize();

        float   rad       = launchAngle * Mathf.Deg2Rad;
        Vector3 launchDir = flat * Mathf.Cos(rad) + Vector3.up * Mathf.Sin(rad);

        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, launchDir);
        float      duration  = Mathf.Max(Quaternion.Angle(barrelTransform.rotation, targetRot) / data.barrelRotateSpeed, 0.1f);

        if (rotateTween != null && rotateTween.IsActive()) { rotateTween.Kill(); rotateTween = null; }

        bool done = false;
        rotateTween = barrelTransform.DORotateQuaternion(targetRot, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() => done = true);

        yield return new WaitUntil(() => done);
    }

    private IEnumerator ResetBarrel()
    {
        if (resetTween != null && resetTween.IsActive()) { resetTween.Kill(); resetTween = null; }

        bool done = false;
        resetTween = barrelTransform.DORotateQuaternion(s_defaultBarrelRot, data.resetDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => done = true);

        yield return new WaitUntil(() => done);
    }

    private Vector3? GetTargetPosition(List<Vector3> usedTargets)
    {
        Vector3 candidate = PickInitialCandidate();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (!IsObstacleAt(candidate) && !IsTooClose(candidate, usedTargets))
                return candidate;
            candidate = Random.value < 0.5f ? GetPlayerNearbyPos() : GetRandomPosition();
        }

        // 모든 시도 실패 → 플레이어 현재 위치
        Vector3 playerPos = GetPlayerCurrentPos();
        if (!IsObstacleAt(playerPos) && !IsTooClose(playerPos, usedTargets))
            return playerPos;

        // 플레이어 위치도 실패 → 취소
        return null;
    }

    private Vector3 PickInitialCandidate()
    {
        // 이동 예측 위치를 항상 기준으로 삼고, accuracy로 scatter 반경 제어
        // accuracy 1.0 → scatter 0 (완벽한 예측 사격)
        // accuracy 0.0 → scatter nearbyScatterRadius * 2 (거의 랜덤)
        Vector3 predicted = GetPlayerPredictedPos();
        float scatter = Mathf.Lerp(nearbyScatterRadius * 2f, 0f, data.accuracy);
        Vector2 offset = Random.insideUnitCircle * scatter;
        return new Vector3(predicted.x + offset.x, 0f, predicted.z + offset.y);
    }

    private Vector3 GetPlayerCurrentPos()
    {
        return new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);
    }

    private Vector3 GetPlayerPredictedPos()
    {
        Vector3 predicted = playerTransform.position + estimatedPlayerVelocity * predictLeadTime;
        return new Vector3(predicted.x, 0f, predicted.z);
    }

    private Vector3 GetPlayerNearbyPos()
    {
        Vector2 offset = Random.insideUnitCircle * nearbyScatterRadius;
        return new Vector3(playerTransform.position.x + offset.x, 0f, playerTransform.position.z + offset.y);
    }

    // 플레이어 기준 랜덤 위치 — 월드 원점 고정값 사용 시 (0,0,0) 근처로만 날아가는 문제 방지
    private Vector3 GetRandomPosition()
    {
        Vector2 rnd = Random.insideUnitCircle * nearbyScatterRadius * 2f;
        return new Vector3(playerTransform.position.x + rnd.x, 0f, playerTransform.position.z + rnd.y);
    }

    private bool IsTooClose(Vector3 pos, List<Vector3> usedTargets)
    {
        float sqrMin = minTargetSpacing * minTargetSpacing;
        foreach (var used in usedTargets)
            if ((pos - used).sqrMagnitude < sqrMin) return true;
        return false;
    }

    private bool IsObstacleAt(Vector3 pos) =>
        Physics.CheckSphere(pos, data.landCheckRadius, avoidLayers);

    private void FireAt(Vector3 targetPos)
    {
        var balloon = PoolManager.Instance.SpawnQueue<WaterBomb>("WaterBomb", firePoint.position);
        if (balloon == null) return;
        balloon.Launch(targetPos, data.balloonDamage, data.fuseTime, explosionRadius, alertRadius, BalloonType.Cannon,
                       balloonSpeed, balloonMinTime, balloonMaxTime);
        PlayBarrelRecoil();
    }

    private void PlayBarrelRecoil()
    {
        recoilTween?.Kill();
        barrelTransform.localPosition = barrelOriginLocalPos;
        // barrelTransform의 로컬 Y(up)가 발사 방향이므로 -Y로 반동
        Vector3 recoilPos = barrelOriginLocalPos + new Vector3(0f, -recoilDistance, 0f);
        recoilTween = barrelTransform.DOLocalMove(recoilPos, recoilBackDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
                recoilTween = barrelTransform.DOLocalMove(barrelOriginLocalPos, recoilForwardDuration)
                    .SetEase(Ease.OutBack));
    }

    public override void OnDispawn()
    {
        if (rotateTween != null && rotateTween.IsActive()) { rotateTween.Kill(); rotateTween = null; }
        if (resetTween  != null && resetTween.IsActive())  { resetTween.Kill();  resetTween  = null; }
        if (recoilTween != null && recoilTween.IsActive()) { recoilTween.Kill(); recoilTween = null; }
        barrelTransform.localPosition = barrelOriginLocalPos;
        if (fireCoroutine != null) { StopCoroutine(fireCoroutine); fireCoroutine = null; }
        if (initCoroutine != null) { StopCoroutine(initCoroutine); initCoroutine = null; }

        barrelTransform.rotation = s_defaultBarrelRot;
        base.OnDispawn();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, data.landCheckRadius);
    }
#endif
}
