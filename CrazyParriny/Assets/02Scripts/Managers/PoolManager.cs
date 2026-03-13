using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TextCore.Text;
using static Unity.Cinemachine.CinemachineSplineRoll;
using static UnityEngine.Analytics.IAnalytic;

public class PoolManager : Singleton<PoolManager>
{
    [SerializeField] private Dictionary<string, Queue<ObjectPoolBase>> qPoolDict = new();
    [SerializeField] private Dictionary<string, HashSet<ObjectPoolBase>> activeQPoolDict = new();
    [SerializeField] private Dictionary<string, List<ObjectPoolBase>> lPoolDict = new();
    [SerializeField] private Dictionary<string, ObjectPoolBase> nPoolDict = new();
    [SerializeField] private Dictionary<string, ObjectPoolBase> prefabs = new();
    [SerializeField] private Dictionary<string, GameObject> parents = new();
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
        var prevPrefabDict = resourceMgr.addressableMap[eAddressableType.prevPrefap];
        foreach (var kvp in prevPrefabDict)
        {
            await InitNewPool(kvp.Key);
        }
        isInit = true;
    }

    public async UniTask InitNewPool(string key)
    {
        await ResourceManager.Instance.LoadAsset<ObjectPoolBase>(key, eAddressableType.prefab, (obj) =>
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
        SetPoolParent(obj);

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

    public void InitSinglePool(string key)
    {
        if (nPoolDict.ContainsKey(key))
        {
            Debug.LogWarning($"Already has {key}");
            return;
        }

        var data = prefabs[key];
        var obj = Instantiate(data, data.parent.transform);
        obj.name = obj.name.Replace("(Clone)", "");
        obj.parent = data.parent;
        obj.gameObject.SetActive(false);
        nPoolDict.Add(key, obj);     
    }

    public void InitQueuePool(string key)
    {
        if(qPoolDict.ContainsKey(key))
        {
            Debug.LogWarning($"Already has {key}");
            return;
        }

        Queue<ObjectPoolBase> queue = new Queue<ObjectPoolBase>();
        qPoolDict.Add(key, queue);

        var data = prefabs[key];
        for (int i = 0; i < data.prevCount; i++)
        {
            var obj = Instantiate(data, data.parent.transform);
            obj.name = obj.name.Replace("(Clone)", "");
            obj.parent = data.parent;
            obj.gameObject.SetActive(false);
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

        var list = new List<ObjectPoolBase>();
        list.Capacity = data.prevCount;
        lPoolDict.Add(key, list);

        for (int i = 0; i < data.prevCount; i++)
        {
            var obj = Instantiate(data, data.parent.transform);
            obj.name = obj.name.Replace("(Clone)", "");
            obj.parent = data.parent;
            obj.index = i;
            obj.gameObject.SetActive(false);
            list.Add(obj);
        }
    }

    private bool SyncInitPool<T>(string rcode) where T : ObjectPoolBase
    {
        var loadedPrefab = ResourceManager.Instance.GetAsset<T>(rcode);
        if (loadedPrefab != null)
        {
            SetNewPool(loadedPrefab);
            return true;
        }
        else
        {
            Debug.LogError($"{rcode} is null");
            return false;
        }
    }


    #endregion

    #region 스폰 큐
    public T SpawnQueue<T>(string key) where T : ObjectPoolBase
    {
        if(!qPoolDict.ContainsKey(key))
        {
            Debug.LogWarning($"{key} is Not ready");
            if(!SyncInitPool<T>(key))
                return null;
        }

        if (qPoolDict[key].Count == 0)
        {
            if(prefabs.TryGetValue(key, out var item) && item.isAddSpawn)
            {
                var obj = Instantiate(item, item.parent.transform);
                obj.name = obj.name.Replace("(Clone)", "");
                obj.transform.position = Vector3.down * 100;
                qPoolDict[key].Enqueue(obj);
            }
            else
            {
                Debug.LogError($"{key} is null");
                return null;
            }
        }
        var retObj = (T)qPoolDict[key].Dequeue();
        retObj.Init();
        return retObj;
    }

    public T SpawnQueue<T>(string rcode, Vector3 position) where T : ObjectPoolBase
    {
        var obj = SpawnQueue<T>(rcode);
        obj.transform.position = position;
        return obj;
    }

    public T SpawnQueue<T>(string rcode, Vector3 position, Transform parent) where T : ObjectPoolBase
    {
        var obj = SpawnQueue<T>(rcode, position);
        obj.transform.parent = parent;
        return obj;
    }

    public T SpawnQueue<T>(string rcode, Vector3 position, Quaternion rotation, Transform parent) where T : ObjectPoolBase
    {
        var obj = SpawnQueue<T>(rcode, position, parent);
        obj.transform.rotation = rotation;
        return obj;
    }
    #endregion
    #region 릴리즈
    public void Release(ObjectPoolBase item)
    {
        item.OnDispawn();
        if (!qPoolDict.ContainsKey(item.key))
        {
            item.transform.SetParent(item.parent.transform);
            qPoolDict[item.name].Enqueue(item);
        }
        else
        {
            Debug.LogError($"{item.key} - PoolBase is null");
        }
    }

    public void ReleaseQPool(string key)
    {
        if (activeQPoolDict.TryGetValue(key, out var pool))
        {
            foreach (var item in pool)
            {
                if (item.gameObject.activeSelf)
                    item.OnDispawn();
                qPoolDict[key].Enqueue(item);
            }
        }
    }

    public void ReleaseLPool(string key)
    {
        if (lPoolDict.TryGetValue(key, out var pool))
        {
            foreach(var item in pool)
            {
                if (item.gameObject.activeSelf)
                    item.OnDispawn();
            }
        }
    }
    public void ReleaseAll()
    {
        foreach(var key in activeQPoolDict.Keys)
        {
            ReleaseQPool(key);
        }

        foreach (var key in lPoolDict.Keys)
        {
            ReleaseLPool(key);
        }

        foreach (var key in lPoolDict.Keys)
        {
            if (nPoolDict.TryGetValue(key, out var pool))
            {
                if (pool.gameObject.activeSelf)
                    pool.OnDispawn();
            }
        }
    }

    public void DestoryQPool(string key)
    {
        if (qPoolDict.TryGetValue(key, out var pool))
        {
            foreach (var item in pool)
            {
                if (item.gameObject.activeSelf)
                    item.OnDestoy();
            }
        }

        if(activeQPoolDict.TryGetValue(key, out var activePool))
        {
            foreach (var item in activePool)
            {
                item.OnDestoy();
            }
        }
    }

    public void DestoryLPool(string key)
    {
        if (lPoolDict.TryGetValue(key, out var pool))
        {
            foreach (var item in pool)
            {
                item.OnDestoy();
            }
        }
    }

    public void DestoryNPool(string key)
    {
        if (nPoolDict.TryGetValue(key, out var item))
        {
            item.OnDestoy();
        }
    }
    #endregion

    #region 스폰 리스트
    //리스트풀에서 인덱스로 해당 번쨰를 가져옴
    public T SpawnList<T>(string key, int index = 0) where T : ObjectPoolBase
    {
        if (!lPoolDict.TryGetValue(key, out var pool))
        {
            Debug.LogWarning("rcode is Not ready");
            if (!SyncInitPool<T>(key))
                return null;
        }

        if (pool.Count == 0 || pool.Count <= index)
        {
            if(prefabs.TryGetValue(key, out var item) && item.isAddSpawn)
            {
                var curCount = pool.Count;
                int addCount = index - pool.Count;

                for (int i = 0; i < addCount; i++)
                {
                    var obj = Instantiate(item, item.parent.transform);
                    obj.name.Replace("(Clone)", "");
                    obj.index = curCount + i;
                    lPoolDict[key].Add(obj); ;
                }
            }
        }

        var retObj = (T)pool[index];
        retObj.Init();
        return retObj;
    }

    public T SpawnList<T>(string key, Vector3 position, int index = 0) where T : ObjectPoolBase
    {
        var item = SpawnList<T>(key);
        item.transform.position = position;
        return item;
    }
    #endregion

    public void SetPoolParent(ObjectPoolBase data)
    {
        switch (data.poolType)
        {
            case ePoolType.UI:
                //TODO 캔버스 밑에 넣기
                break;
            case ePoolType.Prefab:
            default:
                var parentTr = new GameObject(data.key + "parent").transform;
                data.transform.SetParent(parentTr);
                break;
        }
    }
}
