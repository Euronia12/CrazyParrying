using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InGameManager : Singleton<InGameManager>
{
    private const string playerKey = "Player";
    private const string mapKey    = "FirstMap";

    [SerializeField] private eDifficulty eDifficulty = eDifficulty.Normal;
    [SerializeField] private MapData     curMapData;
    private UIFadeInOut uiFade;

    [SerializeField] float spawnMin      = 5f;
    [SerializeField] float spawnMax      = 85f;
    [SerializeField] float personalSpace = 1.5f;

    private readonly Collider[] _overlapBuffer  = new Collider[1];
    private readonly List<Vector3> _spawnedPositions = new List<Vector3>();
    private Vector3 _tempPos;

    private PoolManager poolMgr;
    public  bool        _isSettingUp    = false;
    private bool        _isTransitioning = false;
    private bool        _isStageClear   = false;

    public int currentStage { get; private set; } = 1;
    private float gameStartTime;

    // ── 초기화 ────────────────────────────────────────

    public async override void Init()
    {
        base.Init();
        poolMgr        = PoolManager.Instance;
        _isSettingUp   = false;
        _isStageClear  = false;

        // UIFadeInOut은 Show()하지 않음 — Show()는 기존 UI에 SetActive(true)를 호출해
        // 투명한 채로 활성화된 레이어가 UIPopupDiffSettings 클릭을 차단하는 버그 유발.
        // FadeOut()에서 레이지 초기화하므로 여기서는 참조만 캐싱.
        uiFade = UIManager.Instance.Get<UIFadeInOut>();
        if (uiFade == null)
        {
            uiFade = await UIManager.Instance.Show<UIFadeInOut>();
            uiFade?.SetActive(false);
        }

        await UIManager.Instance.Show<UIPopupDiffSettings>();
        UIManager.Instance.Hide<UITitle>();
    }

    // ── 난이도 선택 후 게임 시작 ──────────────────────

    public void Setup(int idx, Action complete = null)
    {
        SetGameDifficulty(idx);
        StartGameAsync().Forget();
        complete?.Invoke();
    }

    public void SetGameDifficulty(int diff)
    {
        eDifficulty = (eDifficulty)diff;
    }

    private async UniTaskVoid StartGameAsync()
    {
        currentStage  = 1;
        _isStageClear = false;
        gameStartTime = Time.time;
        await LoadStageAsync(spawnPlayer: true);
        _isSettingUp = false;
    }

    private async UniTask LoadStageAsync(bool spawnPlayer)
    {
        string assetName = $"MapData_{eDifficulty}{currentStage}";
        await ResourceManager.Instance.LoadAsset<MapData>(assetName, eAddressableType.DefaultLocalGroup,
            obj => curMapData = obj);

        SummonMap();
        if (spawnPlayer) SummonPlayer();
        else
        {
            if (Player.Instance != null)
            {
                Player.Instance.WarpTo(Vector3.zero);
                Player.Instance.status.SetData(curMapData.playerData);
                Player.Instance.Setup();
            }
        }
        SummonDiffObject();
    }

    private void SummonMap()
    {
        var map = poolMgr.SpawnSingle<ObjectPoolBase>(mapKey);
        if (map != null) map.transform.position = Vector3.zero;
    }

    private void SummonPlayer()
    {
        var player = poolMgr.SpawnSingle<Player>(playerKey);
        if (player == null) return;
        player.WarpTo(Vector3.zero);
        player.status.SetData(curMapData.playerData);
        player.Setup();
    }

    private void SummonDiffObject()
    {
        _spawnedPositions.Clear();

        for (int i = 0; i < curMapData.boxCount; i++)
            SpawnObject<ObstacleBox>("ObstacleBox");

        for (int i = 0; i < curMapData.enemyCount; i++)
        {
            var enemy = poolMgr.SpawnQueue<Enemy>("Enemy");
            if (enemy == null) continue;
            enemy.SetData(curMapData.enemyData, curMapData.explosionRadius, curMapData.alertRadius,
                          curMapData.balloonSpeed, curMapData.minFlightTime, curMapData.maxFlightTime);
            SpawnObjectAt(enemy);
        }

        for (int i = 0; i < curMapData.objstacleCanonCount; i++)
        {
            var canon = poolMgr.SpawnQueue<ObstacleCanon>("ObstacleCanon");
            if (canon == null) continue;
            canon.SetData(curMapData.obstacleCannonData, curMapData.explosionRadius, curMapData.alertRadius,
                          curMapData.balloonSpeed, curMapData.minFlightTime, curMapData.maxFlightTime);
            SpawnObjectAt(canon);
        }
    }

    // ── 클리어 / 게임오버 ─────────────────────────────

    public void OnEnemyDied()
    {
        if (_isTransitioning) return;
        if (!poolMgr.activeQPoolDict.TryGetValue("Enemy", out var set) || set.Count > 0) return;
        OnStageClearAsync().Forget();
    }

    private bool HasNextStage()
    {
        string nextKey = $"MapData_{eDifficulty}{currentStage + 1}";
        return ResourceManager.Instance.addressableMap[eAddressableType.DefaultLocalGroup].ContainsKey(nextKey);
    }

    private async UniTaskVoid OnStageClearAsync()
    {
        _isStageClear = true;
        // 잔여 물풍선 즉시 정리
        poolMgr.ReleaseQPool("WaterBomb");

        bool hasNext = HasNextStage();

        if (!hasNext)
        {
            float clearTime = Time.time - gameStartTime;
            DataManager.Instance.SaveRanking(eDifficulty, clearTime);
            // 마지막 스테이지 → 바로 엔딩
            await GoToClearMapAsync();
            return;
        }

        var popup = await UIManager.Instance.Show<UIPopupClear>();
        popup.Setup(isLastStage: false);
    }

    private async UniTask GoToClearMapAsync()
    {
        await FadeOut();
        DespawnForTransition();
        await FadeIn();
        await UIManager.Instance.Show<UIEnding>();
    }

    public void OnGameOver()
    {
        if (_isStageClear) return;
        UIManager.Instance.Show<UIPopupGameOver>().Forget();
    }

    // ── 스테이지 전환 ─────────────────────────────────

    public async UniTaskVoid GoNextStage()
    {
        UIManager.Instance.Hide<UIPopupClear>();
        await FadeOut();
        DespawnForTransition();
        currentStage++;
        _isStageClear = false;
        await LoadStageAsync(spawnPlayer: false);
        await FadeIn();
    }

    public async UniTaskVoid GoToClearMap()
    {
        UIManager.Instance.Hide<UIPopupClear>();
        await GoToClearMapAsync();
    }

    // ── 재시작 / 난이도변경 / 타이틀 ──────────────────

    public async UniTaskVoid RestartFromStage1()
    {
        Time.timeScale = 1f;
        UIManager.Instance.Hide<UIPopupGameOver>();
        UIManager.Instance.Hide<UIPopupPause>();
        UIManager.Instance.Hide<UIPopupClear>();
        await FadeOut();
        DespawnAll();
        currentStage  = 1;
        gameStartTime = Time.time;
        await LoadStageAsync(spawnPlayer: true);
        await FadeIn();
    }

    public async UniTaskVoid ChangeDifficulty()
    {
        Time.timeScale = 1f;
        UIManager.Instance.Hide<UIPopupGameOver>();
        await FadeOut();
        DespawnAll();
        _isSettingUp = false;
        await FadeIn();
        await UIManager.Instance.Show<UIPopupDiffSettings>();
    }

    public async UniTaskVoid GoToTitle()
    {
        Time.timeScale = 1f;
        UIManager.Instance.Hide<UIPopupGameOver>();
        UIManager.Instance.Hide<UIPopupPause>();
        UIManager.Instance.Hide<UIPopupClear>();
        UIManager.Instance.Hide<UIEnding>();
        await FadeOut();
        DespawnAll();
        await FadeIn();
        await UIManager.Instance.Show<UITitle>();
    }

    // ── ESC 포즈 ──────────────────────────────────────

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !_isSettingUp)
            TogglePause();
    }

    private async void TogglePause()
    {
        if (UIManager.Instance.IsOpened<UIPopupPause>())
        {
            UIManager.Instance.Hide<UIPopupPause>();
            Time.timeScale = 1f;
        }
        else
        {
            Time.timeScale = 0f;
            await UIManager.Instance.Show<UIPopupPause>();
        }
    }

    // ── 페이드 ────────────────────────────────────────

    private async UniTask FadeOut()
    {
        if (uiFade == null) uiFade = await UIManager.Instance.Show<UIFadeInOut>();
        if (uiFade == null) return;
        await uiFade.FadeOut();
    }

    private async UniTask FadeIn()
    {
        if (uiFade == null) return;
        await uiFade.FadeIn();
    }

    // ── 디스폰 ────────────────────────────────────────

    // 스테이지 전환: 플레이어 제외 전부 디스폰
    private void DespawnForTransition()
    {
        _isTransitioning = true;
        foreach (var key in new List<string>(poolMgr.activeQPoolDict.Keys))
            poolMgr.ReleaseQPool(key);
        poolMgr.ReleaseSingle(mapKey);
        _isTransitioning = false;
    }

    // 전체 디스폰: 재시작 / 타이틀
    private void DespawnAll()
    {
        _isTransitioning = true;
        foreach (var key in new List<string>(poolMgr.activeQPoolDict.Keys))
            poolMgr.ReleaseQPool(key);
        poolMgr.ReleaseSingle(mapKey);
        poolMgr.ReleaseSingle(playerKey);
        _isTransitioning = false;
    }

    // ── 스폰 유틸 ─────────────────────────────────────

    public T SpawnObject<T>(string key) where T : ObjectPoolBase
    {
        var obj = poolMgr.SpawnQueue<T>(key);
        if (obj == null) return null;
        SpawnObjectAt(obj);
        return obj;
    }

    private void SpawnObjectAt(ObjectPoolBase obj)
    {
        LayerMask mask = obj.avoidLayers | (1 << obj.gameObject.layer);
        Vector3 pos = GetRandomPosInDonut(
            transform.position, obj.transform.position.y,
            spawnMin, spawnMax, personalSpace, mask);
        obj.transform.position = pos;

        // 유효한 위치일 때만 추적 (폴백 위치 Vector3.down * 100 제외)
        if (pos.y >= 0f)
            _spawnedPositions.Add(new Vector3(pos.x, 0f, pos.z));
    }

    private Vector3 GetRandomPosInDonut(Vector3 center, float height, float minR, float maxR, float checkRadius, LayerMask layerMask)
    {
        float minSq    = minR * minR;
        float maxSq    = maxR * maxR;
        float sqrSpace = checkRadius * checkRadius;

        for (int i = 0; i < 30; i++)
        {
            float randX = UnityEngine.Random.Range(-maxR, maxR);
            float randZ = UnityEngine.Random.Range(-maxR, maxR);
            float sqMag = randX * randX + randZ * randZ;

            if (sqMag < minSq || sqMag > maxSq) continue;

            _tempPos.x = center.x + randX;
            _tempPos.y = center.y + height;
            _tempPos.z = center.z + randZ;

            if (Physics.OverlapSphereNonAlloc(_tempPos, checkRadius, _overlapBuffer, layerMask) > 0) continue;

            // 이미 배치된 오브젝트와 겹침 체크 (물리 싱크 지연 우회)
            bool overlaps = false;
            foreach (var p in _spawnedPositions)
            {
                float dx = _tempPos.x - p.x;
                float dz = _tempPos.z - p.z;
                if (dx * dx + dz * dz < sqrSpace) { overlaps = true; break; }
            }
            if (!overlaps) return _tempPos;
        }

        Debug.LogWarning("유효한 스폰 위치를 찾지 못했습니다.");
        return Vector3.down * 100;
    }
}
