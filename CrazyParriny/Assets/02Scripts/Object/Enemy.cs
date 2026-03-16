using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : ObjectPoolBase, IDamageable
{
    [Header("Data")]
    [SerializeField] private EnemyData data;

    [Header("Components")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Renderer     bodyRenderer;
    [SerializeField] private Transform    balloonSpawnPoint;

    [Header("Attack")]
    [SerializeField] private float attackLeadTime  = 1.2f;
    [SerializeField] private float bodyRotateSpeed = 240f; // 발사 전 목표 방향 회전 속도 (도/초)

    [Header("Barrel Recoil")]
    [SerializeField] private Transform barrelTransform;
    [SerializeField] private float recoilDistance        = 0.3f;
    [SerializeField] private float recoilBackDuration    = 0.06f;
    [SerializeField] private float recoilForwardDuration = 0.14f;

    private enum EnemyState { Idle, Chase, Attack, Dodge, Dead }

    private EnemyState currentState = EnemyState.Dead;
    private Transform  playerTransform;
    private Material   bodyMat;
    private Color      originalColor;

    private float   currentHp;
    private float   attackCooldown;
    private float   dodgeCooldown;
    private float   explosionRadius;
    private float   alertRadius;
    private float   balloonSpeed;
    private float   balloonMinTime;
    private float   balloonMaxTime;
    private float   strafeTimer;
    private Vector3 lastDestination = Vector3.positiveInfinity;

    private Vector3 prevPlayerPos;
    private Vector3 playerVelocity;

    private Coroutine dodgeCoroutine;
    private Coroutine attackCoroutine;
    private Coroutine initCoroutine;
    private Tween     fadeTween;
    private Tween     recoilTween;

    private Vector3 barrelOriginLocalPos;

    private static int   s_losBlockMask;
    private static bool  s_maskInit;
    private static readonly float[] s_losOffsets = { 2f, -2f, 4f, -4f };

    private WaitForSeconds waitReactionTime;

    public Vector3 Velocity => agent.enabled ? agent.velocity : Vector3.zero;

    private void Awake()
    {
        bodyMat       = bodyRenderer.material;
        originalColor = bodyMat.color;
        agent.enabled = false;

        if (barrelTransform != null)
            barrelOriginLocalPos = barrelTransform.localPosition;

        if (!s_maskInit)
        {
            s_losBlockMask = LayerMask.GetMask("Obstacle", "Box");
            s_maskInit     = true;
        }
    }

    public override void OnSpawn()
    {
        SetActive(true);
        if (!isInit) Init();

        currentState   = EnemyState.Dead;
        attackCooldown = 0f;
        dodgeCooldown  = 0f;
        bodyMat.color  = originalColor;

        if (initCoroutine != null) StopCoroutine(initCoroutine);
        initCoroutine = StartCoroutine(InitNextFrame());
    }

    private IEnumerator InitNextFrame()
    {
        yield return null;

        if (data == null)            { Debug.LogError("[Enemy] data is null");            yield break; }
        if (Player.Instance == null) { Debug.LogError("[Enemy] Player.Instance is null"); yield break; }

        playerTransform  = Player.Instance.transform;
        prevPlayerPos    = playerTransform.position;
        playerVelocity   = Vector3.zero;
        currentHp        = data.maxHp;
        waitReactionTime = new WaitForSeconds(data.reactionTime);

        agent.enabled               = true;
        agent.Warp(transform.position);
        agent.speed                 = data.moveSpeed;
        agent.stoppingDistance      = data.attackRange * 0.85f;
        agent.isStopped             = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority     = Random.Range(30, 70);

        initCoroutine = null;
        ChangeState(EnemyState.Idle);
    }

    public void SetData(EnemyData enemyData, float mapExplosionRadius, float mapAlertRadius,
                        float speed = 0f, float minTime = 0f, float maxTime = 0f)
    {
        data            = enemyData;
        explosionRadius = mapExplosionRadius;
        alertRadius     = mapAlertRadius;
        balloonSpeed    = speed;
        balloonMinTime  = minTime;
        balloonMaxTime  = maxTime;
    }

    private void Update()
    {
        if (currentState == EnemyState.Dead) return;

        attackCooldown -= Time.deltaTime;
        dodgeCooldown  -= Time.deltaTime;

        if (playerTransform != null && Time.deltaTime > 0f)
        {
            Vector3 rawVel = (playerTransform.position - prevPlayerPos) / Time.deltaTime;
            playerVelocity = Vector3.Lerp(playerVelocity, rawVel, Time.deltaTime * 8f);
            prevPlayerPos  = playerTransform.position;
        }

        switch (currentState)
        {
            case EnemyState.Idle:   UpdateIdle();   break;
            case EnemyState.Chase:  UpdateChase();  break;
            case EnemyState.Attack: UpdateAttack(); break;
        }
    }

    private void UpdateIdle()
    {
        if (IsPlayerInDetectRange()) ChangeState(EnemyState.Chase);
    }

    private void UpdateChase()
    {
        if (!IsPlayerInDetectRange()) { ChangeState(EnemyState.Idle);   return; }
        if (IsPlayerInAttackRange())  { ChangeState(EnemyState.Attack); return; }

        Vector3 dest = playerTransform.position;
        if ((dest - lastDestination).sqrMagnitude > 0.09f)
        {
            agent.SetDestination(dest);
            lastDestination = dest;
        }
    }

    private void UpdateAttack()
    {
        if (!IsPlayerInAttackRange()) { ChangeState(EnemyState.Chase); return; }

        // 공격 루틴 없을 때 무빙 (무빙샷)
        if (attackCoroutine == null)
            StrafeAroundPlayer();

        // 회피 대기 중에는 새 공격 시작 안 함
        if (attackCooldown <= 0f && attackCoroutine == null && dodgeCoroutine == null)
        {
            attackCooldown  = data.attackInterval;
            attackCoroutine = StartCoroutine(AttackRoutine());
        }
    }

    // ── 무빙샷 스트레이프 ────────────────────────────────

    private void StrafeAroundPlayer()
    {
        if (playerTransform == null || !agent.enabled) return;

        strafeTimer -= Time.deltaTime;
        if (strafeTimer > 0f) return;

        strafeTimer = Random.Range(1.5f, 3f);

        float   angle     = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float   dist      = Random.Range(data.attackRange * 0.6f, data.attackRange * 0.9f);
        Vector3 candidate = playerTransform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    // ── 상태 전환 ─────────────────────────────────────────

    private void ChangeState(EnemyState next)
    {
        if (currentState == next) return;

        // Attack → 다른 상태: 공격 루틴 중단
        if (currentState == EnemyState.Attack && attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        currentState = next;

        switch (next)
        {
            case EnemyState.Idle:
                if (agent.enabled) agent.ResetPath();
                break;

            case EnemyState.Chase:
                if (agent.enabled)
                {
                    agent.updateRotation = true;
                    agent.isStopped      = false;
                    lastDestination      = Vector3.positiveInfinity;
                    if (playerTransform != null) agent.SetDestination(playerTransform.position);
                }
                break;

            case EnemyState.Attack:
                if (agent.enabled) { agent.updateRotation = true; agent.isStopped = false; }
                strafeTimer = 0f;
                break;

            case EnemyState.Dodge:
                // 에이전트 복구 (AttackRoutine이 멈춰뒀을 수 있으므로)
                if (agent.enabled) { agent.updateRotation = true; agent.isStopped = false; }
                break;

            case EnemyState.Dead:
                if (agent.enabled) agent.isStopped = true;
                StopAllCoroutines();
                attackCoroutine = null;
                dodgeCoroutine  = null;
                break;
        }
    }

    // ── 공격 루틴 ────────────────────────────────────────

    private IEnumerator AttackRoutine()
    {
        // 조준·발사 중 이동 정지
        if (agent.enabled) { agent.isStopped = true; agent.updateRotation = false; }

        // 시야 확보 이동 (상태 변경 없이)
        if (!HasLineOfSight()) yield return MoveForLOS();
        if (currentState == EnemyState.Dead) { attackCoroutine = null; yield break; }

        Vector3 targetPos = GetAimTarget();
        yield return RotateToward(targetPos);
        if (currentState == EnemyState.Dead) { attackCoroutine = null; yield break; }

        Vector3 spawnPos = balloonSpawnPoint != null ? balloonSpawnPoint.position : transform.position;
        var balloon = PoolManager.Instance.SpawnQueue<WaterBomb>("WaterBomb", spawnPos);
        if (balloon == null) { attackCoroutine = null; yield break; }
        balloon.Launch(targetPos, data.balloonDamage, data.fuseTime, explosionRadius, alertRadius, BalloonType.Enemy,
                       balloonSpeed, balloonMinTime, balloonMaxTime);

        PlayBarrelRecoil();

        // 발사 후 즉시 이동 재개 (무빙샷)
        if (agent.enabled && currentState == EnemyState.Attack)
        {
            agent.updateRotation = true;
            agent.isStopped      = false;
            strafeTimer          = 0f;
        }

        attackCoroutine = null;
    }

    // 시야 확보 이동 — 상태 변경 없이 에이전트만 이동, 회피와 독립
    private IEnumerator MoveForLOS()
    {
        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;
        Vector3 right = Vector3.Cross(toPlayer, Vector3.up).normalized;

        foreach (float offset in s_losOffsets)
        {
            Vector3 candidate = transform.position + right * offset;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) continue;

            Vector3 origin = hit.position              + Vector3.up * 0.5f;
            Vector3 target = playerTransform.position  + Vector3.up * 0.5f;
            if (Physics.Linecast(origin, target, s_losBlockMask)) continue;

            agent.isStopped = false;
            agent.SetDestination(hit.position);

            yield return new WaitUntil(() =>
                currentState == EnemyState.Dead ||
                (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f));

            if (agent.enabled) agent.isStopped = true;
            yield break;
        }
    }

    private IEnumerator RotateToward(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) yield break;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        while (Quaternion.Angle(transform.rotation, targetRot) > 0.5f)
        {
            if (currentState == EnemyState.Dead) yield break;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, bodyRotateSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = targetRot;
    }

    private bool HasLineOfSight()
    {
        Vector3 origin = transform.position       + Vector3.up * 0.5f;
        Vector3 target = playerTransform.position + Vector3.up * 0.5f;
        return !Physics.Linecast(origin, target, s_losBlockMask);
    }

    private Vector3 GetAimTarget()
    {
        Vector3 predicted = playerTransform.position + playerVelocity * attackLeadTime;
        Vector3 offset    = new Vector3(Random.Range(-data.aimSpread, data.aimSpread), 0f,
                                        Random.Range(-data.aimSpread, data.aimSpread));
        return new Vector3(predicted.x + offset.x, 0f, predicted.z + offset.z);
    }

    // ── 포신 반동 ────────────────────────────────────────

    private void PlayBarrelRecoil()
    {
        if (barrelTransform == null) return;
        recoilTween?.Kill();
        barrelTransform.localPosition = barrelOriginLocalPos;
        Vector3 recoilPos = barrelOriginLocalPos - barrelTransform.InverseTransformDirection(transform.forward) * recoilDistance;
        recoilTween = barrelTransform.DOLocalMove(recoilPos, recoilBackDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
                recoilTween = barrelTransform.DOLocalMove(barrelOriginLocalPos, recoilForwardDuration)
                    .SetEase(Ease.OutBack));
    }

    // ── 회피 이벤트 (최우선) ─────────────────────────────

    // 패링된 물풍선 벡터가 자신을 향할 때 회피
    public void OnParriedBalloonAlert(Vector3 balloonPos, Vector3 balloonDir)
    {
        if (currentState == EnemyState.Dead) return;
        if (dodgeCooldown > 0f) return;

        Vector3 toSelf = transform.position - balloonPos;
        toSelf.y = 0f;
        if (toSelf.sqrMagnitude < 0.01f) return;

        // 물풍선 진행 방향과 나를 향하는 방향이 ~45° 이내일 때만
        if (Vector3.Dot(toSelf.normalized, balloonDir) < 0.7f) return;

        InterruptAndDodge(balloonPos);
    }

    // 착지 물풍선 경보
    public void OnBalloonDetected(Vector3 balloonPos)
    {
        if (currentState == EnemyState.Dead) return;
        if (dodgeCooldown > 0f) return;

        InterruptAndDodge(balloonPos);
    }

    // 공격 루틴 중단 후 회피 시작
    private void InterruptAndDodge(Vector3 balloonPos)
    {
        if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }
        if (dodgeCoroutine  != null) { StopCoroutine(dodgeCoroutine);  dodgeCoroutine  = null; }
        dodgeCoroutine = StartCoroutine(DodgeRoutine(balloonPos));
    }

    private IEnumerator DodgeRoutine(Vector3 balloonPos)
    {
        yield return waitReactionTime;
        if (currentState == EnemyState.Dead) { dodgeCoroutine = null; yield break; }
        if (Random.value > data.dodgeChance)  { dodgeCoroutine = null; yield break; }

        dodgeCooldown = data.reactionTime + 0.5f;

        Vector3 toBalloon = (balloonPos - transform.position);
        toBalloon.y = 0f;
        toBalloon.Normalize();

        Vector3 dodgeDir  = Vector3.Cross(toBalloon, Vector3.up);
        if (Random.value > 0.5f) dodgeDir = -dodgeDir;

        Vector3 dodgeDest = transform.position + dodgeDir * data.dodgeDistance;
        if (!NavMesh.SamplePosition(dodgeDest, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            dodgeCoroutine = null;
            yield break;
        }

        ChangeState(EnemyState.Dodge);
        agent.isStopped = false;
        agent.SetDestination(hit.position);

        yield return new WaitUntil(() =>
            !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        if (currentState != EnemyState.Dead) ChangeState(EnemyState.Chase);
        dodgeCoroutine = null;
    }

    // ── 데미지 / 사망 ────────────────────────────────────

    public void TakeDamage(float amount)
    {
        if (currentState == EnemyState.Dead) return;
        currentHp -= amount;

        fadeTween?.Kill();
        bodyMat.color = Color.white;
        fadeTween = bodyMat.DOColor(originalColor, 0.2f);

        if (currentHp <= 0f) Die();
    }

    private void Die()
    {
        ChangeState(EnemyState.Dead);
        PoolManager.Instance.SpawnQueue<PooledParticle>("DeathEffect", transform.position);
        fadeTween = bodyMat.DOFade(0f, 0.5f).SetEase(Ease.InQuad)
            .OnComplete(() => { if (gameObject.activeSelf) OnDispawn(); });
    }

    private bool IsPlayerInDetectRange() =>
        (transform.position - playerTransform.position).sqrMagnitude <= data.detectRange * data.detectRange;

    private bool IsPlayerInAttackRange() =>
        (transform.position - playerTransform.position).sqrMagnitude <= data.attackRange * data.attackRange;

    public override void OnDispawn()
    {
        if (fadeTween       != null && fadeTween.IsActive())       { fadeTween.Kill();       fadeTween       = null; }
        if (recoilTween     != null && recoilTween.IsActive())     { recoilTween.Kill();     recoilTween     = null; }
        if (barrelTransform != null) barrelTransform.localPosition = barrelOriginLocalPos;
        if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }
        if (dodgeCoroutine  != null) { StopCoroutine(dodgeCoroutine);  dodgeCoroutine  = null; }
        if (initCoroutine   != null) { StopCoroutine(initCoroutine);   initCoroutine   = null; }

        if (agent.enabled) { agent.ResetPath(); agent.isStopped = true; }
        agent.enabled = false;
        currentState  = EnemyState.Dead;

        base.OnDispawn();
        InGameManager.Instance?.OnEnemyDied();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.attackRange);
    }
#endif
}
