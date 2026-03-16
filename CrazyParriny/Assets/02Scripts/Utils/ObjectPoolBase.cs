using UnityEngine;

public enum ePoolType
{
    Prefab,
    UI,
}

public enum ePoolSpawnType
{
    Single,
    Queue,
    List
}

public class ObjectPoolBase : MonoBehaviour
{
    public bool isInit = false;
    public bool isAddSpawn = true;
    public string key;
    public int prevCount = 1;
    public int index = 0;
    public Transform parent;
    public ePoolType poolType;
    public ePoolSpawnType spawnType;
    public LayerMask avoidLayers;

    public virtual void Init() { }
    public virtual void Setup() { }

    public virtual void OnSpawn()
    {
        SetActive(true);
        if (!isInit)
            Init();
        Setup();
    }

    public virtual void OnDispawn()
    {
        SetActive(false);

        // Queue 풀 오브젝트는 자동으로 풀에 반환
        if (spawnType == ePoolSpawnType.Queue && PoolManager.Instance != null)
            PoolManager.Instance.ReturnToQueue(this);
    }

    public virtual void OnDestoy() { SetActive(false); }
    public virtual void SetActive(bool isOn) { gameObject.SetActive(isOn); }
}
