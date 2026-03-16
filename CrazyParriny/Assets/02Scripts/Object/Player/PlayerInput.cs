using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerInput : MonoBehaviour
{
    Player player;

    [Header("Move")]
    [SerializeField] float moveSpd = 10;
    [SerializeField] float gravity = 1;
    [SerializeField] CharacterController cc;

    [Header("Dash")]
    [SerializeField] float dashDuration = 0.2f;
    [SerializeField] float dashSpd      = 10;
    [SerializeField] float dashCoolTime = 1f;

    [Header("Parry")]
    [SerializeField] float parryOuterRadius   = 5f;
    [SerializeField] float parryPreciseRadius = 2.5f;
    [SerializeField] float parryMaxHeight     = 5f;
    [SerializeField] float parryCoolTime      = 0.8f;
    [SerializeField] float parryAngle         = 120f;

    [Header("Parry Indicator")]
    [SerializeField] int   indicatorSegments = 32;
    [SerializeField] float indicatorFadeTime = 0.5f;
    [SerializeField] Color colorOuter        = new Color(0.3f, 0.8f, 1f,  0.30f);
    [SerializeField] Color colorPrecise      = new Color(0.2f, 1f,  0.4f, 0.55f);
    [SerializeField] Color colorCooldown     = new Color(0.5f, 0.5f, 0.5f, 0.25f);

    [Header("Camera Shake (Precise Parry)")]
    [SerializeField] float shakeDuration  = 0.15f;
    [SerializeField] float shakeMagnitude = 0.08f;

    // indicatorRoot만 회전 — 런타임 메시 업데이트 없음
    private Transform    indicatorRoot;
    private MeshRenderer outerMeshRenderer, preciseMeshRenderer;
    private Material     outerMeshMat,      preciseMeshMat;
    private LineRenderer outerLine,         preciseLine;

    private float indicatorAlpha;
    private bool  showPrecise;

    private bool isDashing;
    private bool isDashCoolTime;
    private bool isParryCoolTime;

    private float dashCoolStartTime  = -999f;
    private float parryCoolStartTime = -999f;

    private Vector3 lastValidDir = Vector3.forward;
    private Vector3 dir;
    private Vector3 velocity;

    public float DashCoolRatio  => isDashCoolTime
        ? Mathf.Clamp01((Time.unscaledTime - dashCoolStartTime)  / dashCoolTime)  : 1f;

    public float ParryCoolRatio => isParryCoolTime
        ? Mathf.Clamp01((Time.unscaledTime - parryCoolStartTime) / parryCoolTime) : 1f;

    private readonly Collider[]      _parryBuffer = new Collider[8];
    private readonly List<WaterBomb> _preciseHits = new List<WaterBomb>();
    private readonly List<WaterBomb> _outerHits   = new List<WaterBomb>();
    private int _waterBombMask;

    bool isInit;

    // ──────────────────────────────────────────────────────────────

    public void Init()
    {
        if (isInit) return;

        player         ??= GetComponent<Player>();
        gravity          = Physics.gravity.y;
        _waterBombMask   = LayerMask.GetMask("WaterBomb");

        BuildParryIndicator();
        isInit = true;
    }

    private void BuildParryIndicator()
    {
        indicatorRoot = new GameObject("ParryIndicator").transform;
        // 씬 루트에 배치 — 플레이어 scale 영향을 받지 않도록

        // 메시는 Init 시 1회만 생성 — 이후 런타임에서 버텍스 변경 없음
        BuildSectorMesh("OuterFill",   parryOuterRadius,   out outerMeshRenderer,   out outerMeshMat,   out outerLine);
        BuildSectorMesh("PreciseFill", parryPreciseRadius, out preciseMeshRenderer, out preciseMeshMat, out preciseLine);
    }

    // +Z 방향 고정 부채꼴 메시 1회 생성 (런타임에서 indicatorRoot 회전으로만 방향 제어)
    private void BuildSectorMesh(string goName, float radius,
        out MeshRenderer mr, out Material mat, out LineRenderer lr)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(indicatorRoot, false);

        int     seg       = indicatorSegments;
        float   halfAngle = parryAngle * 0.5f * Mathf.Deg2Rad;
        Vector3 center    = Vector3.up * 0.05f;

        var verts   = new Vector3[seg + 2];
        var tris    = new int[seg * 3];
        var normals = new Vector3[seg + 2];

        verts[0]   = center;
        normals[0] = Vector3.up;

        for (int i = 0; i <= seg; i++)
        {
            float ang    = Mathf.Lerp(-halfAngle, halfAngle, (float)i / seg);
            verts[i + 1]   = center + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * radius;
            normals[i + 1] = Vector3.up;
        }
        for (int i = 0; i < seg; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        var mesh = new Mesh();
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.normals   = normals;
        mesh.UploadMeshData(true); // GPU 업로드 후 CPU 메모리 해제 (불변 처리)

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        mr  = go.AddComponent<MeshRenderer>();
        mat = new Material(Shader.Find("Sprites/Default")) { renderQueue = 3000 };
        mr.material          = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        mr.enabled           = false;

        // 외곽선 위치도 1회 고정 설정
        var lineGo = new GameObject("Outline");
        lineGo.transform.SetParent(go.transform, false);
        lr = lineGo.AddComponent<LineRenderer>();
        lr.material          = new Material(Shader.Find("Sprites/Default"));
        lr.useWorldSpace     = false;
        lr.loop              = false;
        lr.widthMultiplier   = 0.07f;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.positionCount     = seg + 3;

        lr.SetPosition(0, center);
        for (int i = 0; i <= seg; i++)
            lr.SetPosition(i + 1, verts[i + 1]);
        lr.SetPosition(seg + 2, center);
        lr.enabled = false;
    }

    public void WarpTo(Vector3 pos)
    {
        cc.enabled         = false;
        transform.position = pos;
        cc.enabled         = true;
    }

    public void Setup()
    {
        isDashing       = false;
        isDashCoolTime  = false;
        isParryCoolTime = false;
        DashAsync(this.GetCancellationTokenOnDestroy()).Forget();
        ParryAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void LateUpdate()
    {
        // 씬 루트에 있는 인디케이터를 플레이어 위치로 동기화
        if (indicatorRoot != null)
            indicatorRoot.position = transform.position;
    }

    private void OnDestroy()
    {
        if (indicatorRoot != null)
            Destroy(indicatorRoot.gameObject);
    }

    private void Update()
    {
        if (!outerMeshRenderer.enabled) return;

        indicatorAlpha -= Time.deltaTime / indicatorFadeTime;
        if (indicatorAlpha <= 0f)
        {
            SetIndicatorVisible(false);
            return;
        }

        ApplyColor(outerMeshMat, outerLine, isParryCoolTime ? colorCooldown : colorOuter);
        if (showPrecise)
            ApplyColor(preciseMeshMat, preciseLine, colorPrecise);
    }

    // 런타임에서 변경되는 건 Material.color 뿐 — temp alloc 없음
    private void ApplyColor(Material mat, LineRenderer lr, Color baseColor)
    {
        Color fill  = baseColor;
        fill.a     *= indicatorAlpha;
        mat.color   = fill;

        Color outline  = fill;
        outline.a      = Mathf.Min(1f, fill.a * 2.5f);
        lr.startColor  = outline;
        lr.endColor    = outline;
    }

    private void SetIndicatorVisible(bool visible)
    {
        outerMeshRenderer.enabled   = visible;
        outerLine.enabled           = visible;
        preciseMeshRenderer.enabled = visible && showPrecise;
        preciseLine.enabled         = visible && showPrecise;
    }

    // 방향은 indicatorRoot 회전만으로 — 메시 버텍스 변경 없음
    private void ShowParryIndicator(Vector3 parryDir, bool isPrecise)
    {
        showPrecise = isPrecise;

        if (parryDir.sqrMagnitude > 0.001f)
            indicatorRoot.rotation = Quaternion.LookRotation(parryDir, Vector3.up);

        Color outerColor = isParryCoolTime ? colorCooldown : colorOuter;
        outerMeshMat.color   = outerColor;
        preciseMeshMat.color = colorPrecise;

        Color outerOutline   = outerColor;   outerOutline.a   = Mathf.Min(1f, outerColor.a  * 2.5f);
        Color preciseOutline = colorPrecise; preciseOutline.a = Mathf.Min(1f, colorPrecise.a * 2.5f);
        outerLine.startColor   = outerLine.endColor   = outerOutline;
        preciseLine.startColor = preciseLine.endColor = preciseOutline;

        indicatorAlpha = 1f;
        SetIndicatorVisible(true);
    }

    // ──────────────────────────────────────────────────────────────

    public void MoveAction(float h, float v)
    {
        if (isDashing) return;

        dir = new Vector3(h, 0f, v).normalized;
        if (dir != Vector3.zero) lastValidDir = dir;

        if (!cc.isGrounded) velocity.y += gravity * Time.unscaledDeltaTime;
        else                velocity.y  = -2f;

        cc.Move((dir * moveSpd + velocity) * Time.unscaledDeltaTime);
    }

    private async UniTaskVoid DashAsync(CancellationToken cts)
    {
        while (true)
        {
            if (cts.IsCancellationRequested) return;

            if (Input.GetKeyDown(KeyCode.Space) && !isDashCoolTime)
            {
                try
                {
                    isDashing         = true;
                    isDashCoolTime    = true;
                    dashCoolStartTime = Time.unscaledTime;

                    player.effect.PlayDash(lastValidDir);

                    float elapsed = 0f;
                    while (elapsed < dashDuration)
                    {
                        cc.Move(lastValidDir * dashSpd * Time.unscaledDeltaTime);
                        elapsed += Time.unscaledDeltaTime;
                        await UniTask.Yield();
                    }

                    isDashing = false;
                    await UniTask.Delay(TimeSpan.FromSeconds(dashCoolTime), ignoreTimeScale: true, cancellationToken: cts);
                }
                finally
                {
                    isDashing      = false;
                    isDashCoolTime = false;
                }
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cts);
        }
    }

    private async UniTaskVoid ParryAsync(CancellationToken cts)
    {
        while (true)
        {
            if (cts.IsCancellationRequested) return;

            if (Input.GetMouseButtonDown(0) && !isParryCoolTime)
            {
                isParryCoolTime    = true;
                parryCoolStartTime = Time.unscaledTime;
                ResetParryCoolAsync(cts).Forget();
                TryParry();
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cts);
        }
    }

    private void TryParry()
    {
        if (Camera.main == null) return;

        // DOTween이 isKinematic Rigidbody transform을 직접 변경 시
        // Physics Collider 위치가 지연 동기화됨 → OverlapCapsule 전에 강제 동기화
        Physics.SyncTransforms();

        Ray   ray         = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (!groundPlane.Raycast(ray, out float enter)) return;

        Vector3 mouseWorldPos = ray.GetPoint(enter);

        Vector3 parryDir = mouseWorldPos - transform.position;
        parryDir.y = 0f;
        if (parryDir.sqrMagnitude < 0.01f) parryDir = transform.forward;
        parryDir.Normalize();

        int count = Physics.OverlapCapsuleNonAlloc(
            transform.position,
            transform.position + Vector3.up * parryMaxHeight,
            parryOuterRadius,
            _parryBuffer, _waterBombMask, QueryTriggerInteraction.Collide);

        float cosHalf    = Mathf.Cos(parryAngle * 0.5f * Mathf.Deg2Rad);
        float preciseSqr = parryPreciseRadius * parryPreciseRadius;

        _preciseHits.Clear();
        _outerHits.Clear();

        for (int i = 0; i < count; i++)
        {
            var wb = _parryBuffer[i].GetComponentInParent<WaterBomb>();
            if (wb == null || !wb.IsParryable) continue;

            Vector3 toWb  = wb.transform.position - transform.position;
            float   xzSqr = toWb.x * toWb.x + toWb.z * toWb.z;

            // 수평 거리가 거의 0이면 (바로 위) 각도 무시하고 정밀 판정
            if (xzSqr < 0.01f)
            {
                _preciseHits.Add(wb);
                continue;
            }

            float xzLen = Mathf.Sqrt(xzSqr);
            float dot   = (toWb.x * parryDir.x + toWb.z * parryDir.z) / xzLen;
            if (dot < cosHalf) continue;

            if (xzSqr < preciseSqr) _preciseHits.Add(wb);
            else                    _outerHits.Add(wb);
        }

        ShowParryIndicator(parryDir, _preciseHits.Count > 0);

        if (_preciseHits.Count > 0)
        {
            foreach (var wb in _preciseHits) wb.OnParried(mouseWorldPos);
            player.effect.PlayParry();
            StartCoroutine(ShakeCameraRoutine());
        }

        foreach (var wb in _outerHits) wb.OnDeflected();
    }

    private IEnumerator ShakeCameraRoutine()
    {
        if (Camera.main == null) yield break;

        Transform cam       = Camera.main.transform;
        Vector3   originPos = cam.localPosition;
        float     elapsed   = 0f;

        while (elapsed < shakeDuration)
        {
            float mag         = shakeMagnitude * (1f - elapsed / shakeDuration);
            cam.localPosition = originPos + new Vector3(
                UnityEngine.Random.Range(-mag, mag),
                UnityEngine.Random.Range(-mag, mag), 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.localPosition = originPos;
    }

    private async UniTaskVoid ResetParryCoolAsync(CancellationToken cts)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(parryCoolTime), ignoreTimeScale: true, cancellationToken: cts);
        isParryCoolTime = false;
    }
}
