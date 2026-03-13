using UnityEngine;

public enum ePoolType
{
    Queue,
    List
}

public class ObjectPoolBase 
{
    public bool isInit;
    public string key;
    public int index;
    public ePoolType poolType;

    public virtual void Init() { }
    public virtual void Setup() { }
    public virtual void OnSpawn() { }
    public virtual void OnDispawn() { }
}
