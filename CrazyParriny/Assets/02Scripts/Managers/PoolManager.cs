using System.Collections.Generic;
using UnityEngine;

public class PoolManager : Singleton<PoolManager>
{
    [SerializeField] private Dictionary<string, Queue<ObjectPoolBase>> qPoolDict = new();
    [SerializeField] private Dictionary<string, List<ObjectPoolBase>> lPoolDict = new();
    [SerializeField] private Dictionary<string, Transform> parents = new();

    public override void Init()
    {
        base.Init();
        PrewarmPool();
    }

    private void PrewarmPool()
    {

    }
}
