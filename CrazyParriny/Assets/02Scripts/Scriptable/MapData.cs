using UnityEngine;

[CreateAssetMenu(fileName = "MapData", menuName = "Scriptable Objects/MapData")]
public class MapData : ScriptableObject
{
    [Header("Player")]
    public PlayerData playerData;

    [Header("Enemy")]
    public EnemyData enemyData;
    public int enemyCount = 1;

    [Header("Cannon")]
    public ObstacleCannonData obstacleCannonData;
    public int objstacleCanonCount = 1;

    [Header("Map")]
    public int boxCount = 5;

    [Header("Explosion (난이도별 공통)")]
    public float explosionRadius = 2f;
    public float alertRadius     = 3f;

    [Header("Balloon Speed (난이도별 공통)")]
    public float balloonSpeed    = 15f;
    public float minFlightTime   = 0.35f;
    public float maxFlightTime   = 2.5f;
}
