using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleCannonData", menuName = "Scriptable Objects/ObstacleCannonData")]
public class ObstacleCannonData : ScriptableObject
{
    [Header("Fire")]
    public float fireInterval = 4f;  // ����Ʈ ����
    public int shotsPerBurst = 3;   // ����Ʈ�� �߻� ��
    public float shotDelay = 0.5f;// �߻� �� ������
    public float accuracy = 0.7f;// �÷��̾� ���� Ȯ�� 0~1

    [Header("Barrel")]
    public float barrelRotateSpeed = 120f; // ���� ȸ�� �ӵ� (��/��)
    public float resetDuration = 0.8f; // �⺻ ���� ���� �ð�

    [Header("Balloon")]
    public float balloonDamage = 1f;
    public float fuseTime = 1.5f;
    public float landCheckRadius = 0.5f;
}
