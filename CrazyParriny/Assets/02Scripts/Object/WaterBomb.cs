using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody))]
public class WaterBomb : ObjectPoolBase
{
    [Header("Components")]
    [SerializeField] private Renderer       balloonRenderer;
    [SerializeField] private Transform      balloonVisual;
    [SerializeField] private TrailRenderer  trailRenderer;
    [SerializeField] private GameObject     dangerZoneObj;
    // 위험구역 — Awake에서 코드로 생성 (프리팹에 별도 오브젝트 추가 불필요)
    private LineRenderer dangerZoneLine;
    private Material     dangerZoneFillMat;
    private Material     dangerZoneLineMat;

    [Header("Arc")]
    [SerializeField] private float arcHeight      = 8f;
    [SerializeField] private float enemyArcHeight = 2.5f;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float minFlightTime   = 0.35f;
    [SerializeField] private float maxFlightTime   = 2.5f;

    [Header("Colors")]
    [SerializeField] private Color colorSafe   = Color.green;
    [SerializeField] private Color colorDanger = Color.red;

    [Header("Parry")]
    [SerializeField] private float parrySpeed = 16f;

    private Rigidbody rb;
    private Material  balloonMat;
    private Color     originalColor;
    private Vector3   originalVisualScale;

    private BalloonType balloonType;
    private float damage;
    private float fuseTime;
    private float explosionRadius;
    private float alertRadius;

    private bool    hasLanded;
    private bool    isParried;
    private bool    isExploded;
    private float   spawnImmunityEndTime;
    private Vector3 incomingDir;

    private Sequence bounceSeq;
    private Tween    fuseTween;
    private Tween    colorTween;
    private Tween    dangerZoneColorTween;
    private Tween    arcTween;

    private static Mesh s_discMesh;

    private static int  s_hitMask;
    private static int  s_solidMask;
    private static int  s_playerLayer;
    private static bool s_maskInit;

    private static readonly Collider[] s_overlapBuffer = new Collider[32];

    public bool IsParryable => gameObject.activeSelf && !hasLanded;

    // 스폰 직후 Launch() 호출 전까지 모든 Trigger 차단
    public override void OnSpawn()
    {
        isParried            = false;
        isExploded           = false;
        hasLanded            = false;
        spawnImmunityEndTime = float.MaxValue;
        base.OnSpawn();
    }

    private void Awake()
    {
        rb                  = GetComponent<Rigidbody>();
        balloonMat          = balloonRenderer.material;
        originalColor       = balloonMat.color;
        originalVisualScale = balloonVisual.localScale;

        if (!s_maskInit)
        {
            s_hitMask   = LayerMask.GetMask("Enemy", "Player");
            s_solidMask = LayerMask.GetMask("Obstacle", "Box", "Cannon", "Wall");
            s_playerLayer = LayerMask.NameToLayer("Player");
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("WaterBomb"), LayerMask.NameToLayer("WaterBomb"), true);
            s_maskInit    = true;
        }

        // 위험구역 시각화 — dangerZoneObj 하위에 코드로 생성
        if (dangerZoneObj != null) BuildDangerZone();
    }

    // dangerZoneObj 하위에 Fill + Outline을 코드로 생성 (PlayerInput.BuildSectorMesh 방식)
    private void BuildDangerZone()
    {
        const int segs = 48;

        // ── 내부 Fill 디스크 ──────────────────────────────
        var fillGo = new GameObject("DangerFill");
        fillGo.transform.SetParent(dangerZoneObj.transform, false);

        var mf   = fillGo.AddComponent<MeshFilter>();
        mf.sharedMesh = GetDiscMesh(segs);

        var mr = fillGo.AddComponent<MeshRenderer>();
        dangerZoneFillMat = new Material(Shader.Find("Sprites/Default")) { renderQueue = 3000 };
        mr.material          = dangerZoneFillMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        // ── 외곽 Outline LineRenderer ──────────────────────
        var lineGo = new GameObject("DangerLine");
        lineGo.transform.SetParent(dangerZoneObj.transform, false);

        dangerZoneLine = lineGo.AddComponent<LineRenderer>();
        dangerZoneLineMat = new Material(Shader.Find("Sprites/Default"));
        dangerZoneLine.sharedMaterial  = dangerZoneLineMat;
        dangerZoneLine.useWorldSpace   = false;
        dangerZoneLine.loop            = true;
        dangerZoneLine.widthMultiplier = 0.08f;
        dangerZoneLine.shadowCastingMode = ShadowCastingMode.Off;
        dangerZoneLine.receiveShadows    = false;
        dangerZoneLine.positionCount     = segs;

        for (int i = 0; i < segs; i++)
        {
            float a = 2f * Mathf.PI * i / segs;
            dangerZoneLine.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.5f, 0.02f, Mathf.Sin(a) * 0.5f));
        }
    }

    private static Mesh GetDiscMesh(int segs)
    {
        if (s_discMesh != null) return s_discMesh;

        s_discMesh = new Mesh { name = "DangerDisc" };
        var verts = new Vector3[segs + 1];
        var tris  = new int[segs * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < segs; i++)
        {
            float a = 2f * Mathf.PI * i / segs;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, 0.01f, Mathf.Sin(a) * 0.5f);
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segs + 1;
        }
        s_discMesh.vertices  = verts;
        s_discMesh.triangles = tris;
        s_discMesh.RecalculateNormals();
        s_discMesh.UploadMeshData(true);
        return s_discMesh;
    }

    private void SetDangerZoneColor(Color c)
    {
        if (dangerZoneFillMat != null)
        {
            Color fill = c;
            fill.a = c.a * 0.2f;
            dangerZoneFillMat.color = fill;
        }
        if (dangerZoneLineMat != null)
        {
            Color outline = c;
            outline.a = Mathf.Min(1f, c.a * 1.5f);
            dangerZoneLineMat.color = outline;
            if (dangerZoneLine != null)
            {
                dangerZoneLine.startColor = outline;
                dangerZoneLine.endColor   = outline;
            }
        }
    }

    public void Launch(Vector3 targetPos, float dmg, float fuse, float expRadius, float altRadius, BalloonType type,
                       float speed = 0f, float minTime = 0f, float maxTime = 0f)
    {
        // 이전 스폰에서 남은 Tween이 있으면 먼저 제거
        KillAllTweens();

        hasLanded       = false;
        isParried       = false;
        isExploded      = false;
        damage          = dmg;
        fuseTime        = fuse;
        explosionRadius = expRadius;
        alertRadius     = altRadius;
        balloonType     = type;

        // 0이면 프리팹 기본값 유지
        if (speed   > 0f) projectileSpeed = speed;
        if (minTime > 0f) minFlightTime   = minTime;
        if (maxTime > 0f) maxFlightTime   = maxTime;

        dangerZoneObj.SetActive(false);
        balloonMat.color         = originalColor;
        balloonVisual.localScale = originalVisualScale;

        if (trailRenderer != null)
        {
            trailRenderer.enabled = false;
            trailRenderer.Clear();
            trailRenderer.enabled = true;
        }

        spawnImmunityEndTime = Time.time + 0.1f;

        incomingDir   = targetPos - transform.position;
        incomingDir.y = 0f;
        incomingDir   = incomingDir.normalized;

        LaunchArc(targetPos, type == BalloonType.Enemy ? enemyArcHeight : arcHeight);
    }

    private void LaunchArc(Vector3 targetPos, float height)
    {
        rb.isKinematic = true;
        rb.useGravity  = false;

        Vector3 startPos = transform.position;
        float   dist     = Vector3.Distance(startPos, targetPos);
        float   duration = Mathf.Clamp(dist / projectileSpeed, minFlightTime, maxFlightTime);

        arcTween?.Kill();
        arcTween = DOTween.To(() => 0f, t =>
        {
            Vector3 pos = Vector3.LerpUnclamped(startPos, targetPos, t);
            pos.y += Mathf.Sin(Mathf.PI * t) * height;
            transform.position = pos;
        }, 1f, duration)
        .SetEase(Ease.Linear)
        .OnComplete(OnLanded);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasLanded || Time.time < spawnImmunityEndTime) return;

        int layer    = 1 << other.gameObject.layer;
        int objLayer = other.gameObject.layer;

        if (balloonType == BalloonType.Enemy)
        {
            if ((layer & s_solidMask) != 0) { StopArc(); Explode(); return; }
            if ((layer & s_hitMask) == 0) return;

            bool hitsPlayer = objLayer == s_playerLayer;

            if (isParried)
            {
                if (hitsPlayer) return; // 패링된 공은 플레이어 무시
                other.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
                Explode();
                return;
            }

            StopArc();
            if (hitsPlayer) Explode();
            else            OnLanded();
            return;
        }

        if (balloonType == BalloonType.Cannon)
        {
            if ((layer & s_solidMask) != 0) { StopArc(); Explode(); return; }
            // Cannon은 플레이어에게만 직접 충돌 데미지 (Enemy 통과)
            if (objLayer == s_playerLayer)
            {
                StopArc();
                other.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
                OnDispawn();
            }
        }
    }

    private void StopArc()
    {
        arcTween?.Kill();
        arcTween       = null;
        rb.isKinematic = true;
    }

    private void OnLanded()
    {
        if (hasLanded) return;
        hasLanded = true;

        dangerZoneObj.SetActive(true);
        dangerZoneObj.transform.localScale = Vector3.one * explosionRadius * 2f;

        SetDangerZoneColor(colorSafe);
        PlayBounceLoop();

        colorTween = balloonMat.DOColor(colorDanger, fuseTime).SetEase(Ease.InQuad);

        Color startColor = colorSafe;
        dangerZoneColorTween = DOTween.To(
            () => startColor,
            c => { startColor = c; SetDangerZoneColor(c); },
            colorDanger, fuseTime
        ).SetEase(Ease.InQuad);

        fuseTween = DOVirtual.DelayedCall(fuseTime, Explode);

        AlertNearbyEnemies();
    }

    private void PlayBounceLoop()
    {
        bounceSeq?.Kill();
        balloonVisual.localScale = originalVisualScale;

        bounceSeq = DOTween.Sequence()
            .Append(balloonVisual.DOScaleX(originalVisualScale.x * 0.7f, 0.15f))
            .Join(balloonVisual.DOScaleY(originalVisualScale.y * 1.3f, 0.15f))
            .Append(balloonVisual.DOScaleX(originalVisualScale.x * 1.3f, 0.15f))
            .Join(balloonVisual.DOScaleY(originalVisualScale.y * 0.7f, 0.15f))
            .Append(balloonVisual.DOScale(originalVisualScale, 0.1f))
            .SetLoops(-1, LoopType.Restart);
    }

    private void AlertNearbyEnemies()
    {
        if (!PoolManager.Instance.activeQPoolDict.TryGetValue("Enemy", out var enemySet)) return;

        float sqrAlert = alertRadius * alertRadius;
        foreach (var poolObj in enemySet)
        {
            var enemy = poolObj as Enemy;
            if (enemy == null) continue;
            if ((transform.position - enemy.transform.position).sqrMagnitude <= sqrAlert)
                enemy.OnBalloonDetected(transform.position);
        }
    }

    public void Explode()
    {
        if (!gameObject.activeSelf || isExploded) return;
        isExploded = true;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, s_overlapBuffer, s_hitMask);
        for (int i = 0; i < hitCount; i++)
        {
            int layer = s_overlapBuffer[i].gameObject.layer;
            // 패링된 공: 적만 데미지 / 비패링 공: 플레이어만 데미지
            if ( isParried && layer == s_playerLayer) continue;
            if (!isParried && layer != s_playerLayer) continue;
            s_overlapBuffer[i].GetComponentInParent<IDamageable>()?.TakeDamage(damage);
        }

        PoolManager.Instance.SpawnQueue<PooledParticle>("WaterEffect", transform.position);

        // 연쇄 폭발 제거 — 개별 폭발
        OnDispawn();
    }

    public void OnParried(Vector3 mouseWorldPos)
    {
        if (!IsParryable) return;

        StopArc();
        KillAllTweens();

        isParried            = true;
        spawnImmunityEndTime = 0f; // 패링 직후 적 충돌 즉시 활성화
        balloonType          = BalloonType.Enemy;
        balloonVisual.localScale = originalVisualScale;
        dangerZoneObj.SetActive(false);
        balloonMat.color         = originalColor;

        balloonVisual.DOPunchScale(originalVisualScale * 0.6f, 0.25f, 6, 0.4f);

        Vector3 reflectDir = GetReflectDirection(mouseWorldPos);
        incomingDir = GetLeadPredictedDirection(reflectDir);

        if (incomingDir.sqrMagnitude < 0.01f) incomingDir = transform.forward;

        ParryLaunch(incomingDir);

        AlertAllEnemiesOnParry(incomingDir);
    }

    // 패링 후 이동: 일반 발사와 동일하게 DOTween arc 사용 (kinematic 유지 → 물리 전환 없음)
    // sin 호(0.5f)로 완만하게 올라갔다 내려와 지면 통과 없이 50m 직진
    private void ParryLaunch(Vector3 dir)
    {
        rb.isKinematic = true;
        rb.useGravity  = false;

        Vector3 startPos  = transform.position;
        Vector3 targetPos = new Vector3(
            startPos.x + dir.x * 50f,
            0f,
            startPos.z + dir.z * 50f);

        float duration = Mathf.Clamp(50f / parrySpeed, 0.5f, 3.5f);

        arcTween?.Kill();
        arcTween = DOTween.To(() => 0f, t =>
        {
            Vector3 pos = Vector3.LerpUnclamped(startPos, targetPos, t);
            pos.y += Mathf.Sin(Mathf.PI * t) * 0.5f; // 완만한 호 (직선에 가까움)
            transform.position = pos;
        }, 1f, duration)
        .SetEase(Ease.Linear)
        .OnComplete(() => { if (isParried && gameObject.activeSelf) Explode(); });
    }

    private void AlertAllEnemiesOnParry(Vector3 dir)
    {
        if (!PoolManager.Instance.activeQPoolDict.TryGetValue("Enemy", out var enemySet)) return;
        foreach (var poolObj in enemySet)
            (poolObj as Enemy)?.OnParriedBalloonAlert(transform.position, dir);
    }

    // 외곽 구간 패링 — 근처 랜덤 위치에 낙하 (적 방향 반사 없음)
    public void OnDeflected()
    {
        if (!IsParryable) return;

        StopArc();
        KillAllTweens();

        isParried                = true; // 낙하 중 플레이어 통과
        balloonVisual.localScale = originalVisualScale;
        dangerZoneObj.SetActive(false);
        balloonMat.color         = originalColor;

        balloonVisual.DOPunchScale(originalVisualScale * 0.3f, 0.2f, 4, 0.3f);

        Vector2 rnd     = Random.insideUnitCircle.normalized;
        float   dist    = Random.Range(1.5f, 3.5f);
        Vector3 landPos = new Vector3(
            transform.position.x + rnd.x * dist,
            0f,
            transform.position.z + rnd.y * dist);

        LaunchArc(landPos, arcHeight * 0.4f);
    }

    private Vector3 GetReflectDirection(Vector3 mouseWorldPos)
    {
        Vector3 mouseDir = mouseWorldPos - transform.position;
        mouseDir.y = 0f;
        if (mouseDir.sqrMagnitude < 0.01f) return -incomingDir;
        mouseDir.Normalize();

        // 날아온 방향을 마우스 방향 기준 법선으로 반사 후 마우스 방향과 blend
        // 마우스 방향이 입사 방향과 일치하면 combined = 0 → mouseDir 그대로 사용
        Vector3 reflected = Vector3.Reflect(incomingDir, mouseDir);
        Vector3 combined  = mouseDir + reflected;
        return combined.sqrMagnitude < 0.01f ? mouseDir : combined.normalized;
    }

    private Vector3 GetLeadPredictedDirection(Vector3 baseDir)
    {
        if (!PoolManager.Instance.activeQPoolDict.TryGetValue("Enemy", out var enemySet))
            return baseDir;

        Enemy bestEnemy = null;
        float bestScore = float.MinValue;

        foreach (var poolObj in enemySet)
        {
            var enemy = poolObj as Enemy;
            if (enemy == null) continue;

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (dist < 0.1f) continue;

            float dot = Vector3.Dot(toEnemy.normalized, baseDir);
            if (dot < 0f) continue;

            float score = dot * 2f - dist * 0.05f;
            if (score > bestScore) { bestScore = score; bestEnemy = enemy; }
        }

        if (bestEnemy == null) return baseDir;

        Vector3 predictedPos = bestEnemy.transform.position;
        for (int i = 0; i < 2; i++)
        {
            Vector3 toTarget  = predictedPos - transform.position;
            toTarget.y        = 0f;
            float timeToReach = toTarget.magnitude / parrySpeed;
            predictedPos      = bestEnemy.transform.position + bestEnemy.Velocity * timeToReach;
            predictedPos.y    = transform.position.y;
        }

        Vector3 finalDir = predictedPos - transform.position;
        finalDir.y = 0f;
        return finalDir.sqrMagnitude < 0.01f ? baseDir : finalDir.normalized;
    }

    private void KillAllTweens()
    {
        bounceSeq?.Kill();            bounceSeq            = null;
        fuseTween?.Kill();            fuseTween            = null;
        colorTween?.Kill();           colorTween           = null;
        dangerZoneColorTween?.Kill(); dangerZoneColorTween = null;
        arcTween?.Kill();             arcTween             = null;
    }

    public override void OnDispawn()
    {
        KillAllTweens();
        balloonVisual.DOKill();
        balloonVisual.localScale = originalVisualScale;
        dangerZoneObj.SetActive(false);

        if (trailRenderer != null) trailRenderer.enabled = false;

        if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        hasLanded  = false;
        isParried  = false;
        isExploded = false;

        base.OnDispawn();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, alertRadius);
    }
#endif
}
