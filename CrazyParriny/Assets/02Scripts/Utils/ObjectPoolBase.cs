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
    public GameObject parent;
    public ePoolType poolType;
    public ePoolSpawnType spawnType;

    public virtual void Init() { }
    public virtual void Setup() { }
    public virtual void OnSpawn() 
    { 
        SetActive(true);
        Setup();
    }
    public virtual void OnDispawn() { SetActive(false); }
    public virtual void OnDestoy() { SetActive(false); }
    public virtual void SetActive(bool isOn) { gameObject.SetActive(isOn); }
}
