using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Scriptable Objects/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Stats")]
    public float maxHp = 3f;
    public float moveSpeed = 3f;

    [Header("Range")]
    public float detectRange = 10f;  // Ž�� ����
    public float attackRange = 5f;   // ���� ��Ÿ�

    [Header("Attack")]
    public float attackInterval = 2f;
    public float aimSpread = 2f;
    public float balloonDamage = 1f;
    public float fuseTime = 1.5f;

    [Header("Dodge")]
    public float reactionTime = 0.5f; // ���� ������ (Ŭ���� ����)
    public float dodgeChance = 0.7f; // ȸ�� Ȯ�� 0~1
    public float dodgeDistance = 3f;   // ȸ�� �̵� �Ÿ�
}
