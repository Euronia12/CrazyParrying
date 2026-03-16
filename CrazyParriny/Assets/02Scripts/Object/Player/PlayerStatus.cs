using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    [SerializeField] private PlayerData playerData;

    private Player player;
    private float currentHp;

    public float CurrentHp => currentHp;
    public float MaxHp => playerData != null ? playerData.maxHp : 5f;
    public bool IsDead => currentHp <= 0f;

    public void Init()
    {
        player ??= GetComponent<Player>();
    }

    public void SetData(PlayerData data)
    {
        if (data != null) playerData = data;
    }

    public void Setup()
    {
        currentHp = MaxHp;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        currentHp -= amount;
        currentHp = Mathf.Max(currentHp, 0f);

        player.effect.PlayHit();

        if (IsDead)
            Die();
    }

    private void Die()
    {
        UIManager.Instance.Hide<UINaviPlayerHit>();
        InGameManager.Instance?.OnGameOver();
    }
}
