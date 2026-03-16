using UnityEngine;
using UnityEngine.AI;

public class ObstacleBox : ObjectPoolBase
{
    private NavMeshObstacle navObstacle;

    public override void Init()
    {
        base.Init();

        navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle == null)
            navObstacle = gameObject.AddComponent<NavMeshObstacle>();

        navObstacle.carving     = true;
        navObstacle.shape       = NavMeshObstacleShape.Box;
        navObstacle.carveOnlyStationary = false; // 이동해도 실시간 carving
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        if (navObstacle != null) navObstacle.enabled = true;
    }

    public override void OnDispawn()
    {
        if (navObstacle != null) navObstacle.enabled = false;
        base.OnDispawn();
    }
}
