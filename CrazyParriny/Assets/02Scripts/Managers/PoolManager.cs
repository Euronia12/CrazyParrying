using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PoolManager : Singleton<PoolManager>
{
    [SerializeField] private Dictionary<string, Queue<ObjectPoolBase>> qPoolDict = new();
    public Dictionary<string, HashSet<ObjectPoolBase>> activeQPoolDict = new();
    [SerializeField] private Dictionary<string, List<ObjectPoolBase>> lPoolDict = new();
    [SerializeField] private Dictionary<string, ObjectPoolBase> nPoolDict = new();
    [SerializeField] private Dictionary<string, ObjectPoolBase> prefabs = new();
    public bool isInit = false;

    public override void Init()
    {
        base.Init();
    }

    #region 초기화
    [ContextMenu("초기화")]
    public async Task PreWarm()
    {
        var resourceMgr = ResourceManager.Instance;
        var prevPrefabDict = resourceMgr.addressableMap[eAddressableType.Prefab];
        foreach (var kvp in prevPrefabDict)
        {
            await InitNewPool(kvp.Key);
        }
        isInit = true;
    }

    public async UniTask InitNewPool(string key)
    {
        await ResourceManager.Instance.LoadAsset<ObjectPoolBase>(key, eAddressableType.Prefab, (obj) =>
        {
            if (prefabs.ContainsKey(key))
            {
                Debug.LogError($"Already has {key}");
                return;
            }
            SetNewPool(obj);
        });
    }

    public void SetNewPool(ObjectPoolBase obj)
    {
        prefabs.Add(obj.key, obj);

        switch (obj.spawnType)
        {
            case ePoolSpawnType.Queue:
                InitQueuePool(obj.key);
                break;
            case ePoolSpawnType.List:
                InitListPool(obj.key);
                break;
            case ePoolSpawnType.Single:
            default:
                InitSinglePool(obj.key);
                break;
        }
    }

    // 비활성 상태로 Instantiate — Awake/OnEnable을 OnSpawn 전까지 막음 (NavMesh 에러 방지)
    private T InstantiateInactive<T>(T source, Transform parent) where T : ObjectPoolBase
    {
        bool wasActive = source.gameObject.activeSelf;
        source.gameObject.SetActive(false);
        var obj = Instantiate(source, parent);
        if (wasActive)
            source.gameObject.SetActive(true); // 원본 프리팹 상태 복원
        obj.name = obj.name.Replace("(Clone)", "");
        obj.transform.SetParent(parent);
        return obj;
    }

    public void InitSinglePool(string key)
    {
        if (nPoolDict.ContainsKey(key))
        {
            Debug.LogWarning($"Already has {key}");
            return;
        }

        var data = prefabs[key];
        SetPoolParent(data);
        var obj = InstantiateInactive(data, data.parent);
        nPoolDict.Add(key, obj);
    }

    public void InitQueuePool(string key)
    {
        if (qPoolDict.ContainsKey(key))
        {
            Debug.LogWarning($"Already has {key}");
            return;
        }

        var queue = new Queue<ObjectPoolBase>();
        qPoolDict.Add(key, queue);
        activeQPoolDict[key] = new HashSet<ObjectPoolBase>();

        var data = prefabs[key];
        SetPoolParent(data);
        for (int i = 0; i < data.prevCount; i++)
        {
            var obj = InstantiateInactive(data, data.parent);
            queue.Enqueue(obj);
        }
    }

    public void InitListPool(string key)
    {
        if (lPoolDict.ContainsKey(key))
        {
            Debug.LogWarning($"Already has {key}");
            return;
        }

        var data = prefabs[key];
        var list = new List<ObjectPoolBase>(data.prevCount);
        lPoolDict.Add(key, list);

        SetPoolParent(data);
        for (int i = 0; i < data.prevCount; i++)
        {
            var obj = InstantiateInactive(data, data.parent);
            obj.index = i;
            list.Add(obj);
        }
    }

    private bool SyncInitPool<T>(string key) where T : ObjectPoolBase
    {
        var loadedPrefab = ResourceManager.Instance.GetAsset<T>(key);
        if (loadedPrefab != null)
        {
            SetNewPool(loadedPrefab);
            return true;
        }
        Debug.LogError($"{key} is null");
        return false;
    }
    #endregion

    #region Queue 풀 스폰
    public T SpawnQueue<T>(string key) where T : ObjectPoolBase
    {
        if (!qPoolDict.ContainsKey(key))
        {
            Debug.LogWarning($"{key} is Not ready");
            if (!SyncInitPool<T>(key))
                return null;
        }

        if (qPoolDict[key].Count == 0)
        {
            if (prefabs.TryGetValue(key, out var item) && item.isAddSpawn)
            {
                // 동적 확장 시에도 비활성 상태로 생성
                var newObj = InstantiateInactive(item, item.parent);
                qPoolDict[key].Enqueue(newObj);
            }
            else
            {
                Debug.LogError($"{key} pool is empty and cannot expand");
                return null;
            }
        }

        var retObj = (T)qPoolDict[key].Dequeue();

        if (!activeQPoolDict.TryGetValue(key, out var activeSet))
        {
            activeSet = new HashSet<ObjectPoolBase>();
            activeQPoolDict[key] = activeSet;
        }
        activeSet.Add(retObj);

        retObj.OnSpawn();
        return retObj;
    }

    public T SpawnQueue<T>(string key, Vector3 position) where T : ObjectPoolBase
    {
        var obj = SpawnQueue<T>(key);
        if (obj == null) return null;
        obj.transform.position = position;
        return obj;
    }

    public T SpawnQueue<T>(string key, Vector3 position, Transform parent) where T : ObjectPoolBase
    {
        var obj = SpawnQueue<T>(key, position);
        if (obj == null) return null;
        obj.transform.parent = parent;
        return obj;
    }

    public T SpawnQueue<T>(string key, Vector3 position, Quaternion rotation, Transform parent) where T : ObjectPoolBase
    {
        var obj = SpawnQueue<T>(key, position, parent);
        if (obj == null) return null;
        obj.transform.rotation = rotation;
        return obj;
    }

    /// <summary>오브젝트가 OnDispawn 후 풀에 반환될 때 호출</summary>
    public void ReturnToQueue(ObjectPoolBase obj)
    {
        if (activeQPoolDict.TryGetValue(obj.key, out var activeSet))
            activeSet.Remove(obj);

        if (qPoolDict.TryGetValue(obj.key, out var queue))
            queue.Enqueue(obj);
    }
    #endregion

    #region 반환
    /// <summary>외부에서 수동 반환 시 사용. OnDispawn 내부에서 자동 반환 중복 방지.</summary>
    public void Release(ObjectPoolBase item)
    {
        if (!item.gameObject.activeSelf) return; // 이미 반환된 경우 무시

        item.transform.SetParent(item.parent);
        item.OnDispawn(); // → base.OnDispawn → ReturnToQueue 자동 호출
    }

    public void ReleaseQPool(string key)
    {
        if (!activeQPoolDict.TryGetValue(key, out var pool)) return;

        // 순회 중 컬렉션 변경 방지 — 먼저 복사 후 초기화
        var items = new List<ObjectPoolBase>(pool);
        pool.Clear();

        foreach (var item in items)
        {
            if (item.gameObject.activeSelf)
            {
                // ReturnToQueue가 activeSet에서 제거 시도하지만 이미 Clear됨 — 무해
                item.OnDispawn();
            }
        }
    }

    public void ReleaseLPool(string key)
    {
        if (lPoolDict.TryGetValue(key, out var pool))
        {
            foreach (var item in pool)
            {
                if (item.gameObject.activeSelf)
                    item.OnDispawn();
            }
        }
    }

    public void ReleaseAll()
    {
        foreach (var key in new List<string>(activeQPoolDict.Keys))
            ReleaseQPool(key);

        foreach (var key in lPoolDict.Keys)
            ReleaseLPool(key);

        foreach (var key in nPoolDict.Keys)
        {
            if (nPoolDict.TryGetValue(key, out var pool) && pool.gameObject.activeSelf)
                pool.OnDispawn();
        }
    }

    public void DestoryQPool(string key)
    {
        if (qPoolDict.TryGetValue(key, out var pool))
            foreach (var item in pool)
                item.OnDestoy();

        if (activeQPoolDict.TryGetValue(key, out var activePool))
            foreach (var item in activePool)
                item.OnDestoy();
    }

    public void DestoryLPool(string key)
    {
        if (lPoolDict.TryGetValue(key, out var pool))
            foreach (var item in pool)
                item.OnDestoy();
    }

    public void DestoryNPool(string key)
    {
        if (nPoolDict.TryGetValue(key, out var item))
            item.OnDestoy();
    }
    #endregion

    #region Single / List 풀 스폰
    public T SpawnSingle<T>(string key) where T : ObjectPoolBase
    {
        if (!nPoolDict.ContainsKey(key))
        {
            Debug.LogWarning($"{key} is Not ready");
            if (!SyncInitPool<T>(key))
                return null;
        }

        var retObj = (T)nPoolDict[key];
        retObj.OnSpawn();
        return retObj;
    }

    public T SpawnList<T>(string key, int index = 0) where T : ObjectPoolBase
    {
        if (!lPoolDict.TryGetValue(key, out var pool))
        {
            Debug.LogWarning($"{key} is Not ready");
            if (!SyncInitPool<T>(key))
                return null;
            // SyncInitPool 성공 후 재조회
            if (!lPoolDict.TryGetValue(key, out pool))
                return null;
        }

        if (pool.Count == 0 || pool.Count <= index)
        {
            if (prefabs.TryGetValue(key, out var item) && item.isAddSpawn)
            {
                int curCount = pool.Count;
                int addCount = index - curCount + 1;
                for (int i = 0; i < addCount; i++)
                {
                    var obj = InstantiateInactive(item, item.parent);
                    obj.index = curCount + i;
                    lPoolDict[key].Add(obj);
                }
            }
        }

        var retObj = (T)pool[index];
        retObj.OnSpawn();
        return retObj;
    }

    public T SpawnList<T>(string key, Vector3 position, int index = 0) where T : ObjectPoolBase
    {
        var item = SpawnList<T>(key, index);
        if (item == null) return null;
        item.transform.position = position;
        return item;
    }
    #endregion

    public void ReleaseSingle(string key)
    {
        if (nPoolDict.TryGetValue(key, out var item) && item.gameObject.activeSelf)
            item.OnDispawn();
    }

    public void SetPoolParent(ObjectPoolBase data)
    {
        switch (data.poolType)
        {
            case ePoolType.UI:
                // TODO: 캔버스 하위에 붙이기
                break;
            case ePoolType.Prefab:
            default:
                var rootObj = new GameObject(data.key + "parent");
                rootObj.transform.SetParent(transform);
                data.parent = rootObj.transform;
                break;
        }
    }
}
