using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMain : UIDefault
{
    [Header("HP")]
    [SerializeField] private Image             hpFill;
    [SerializeField] private TextMeshProUGUI   hpText;

    [Header("Enemy Count")]
    [SerializeField] private TextMeshProUGUI enemyCountText;

    [Header("Cooltime")]
    [SerializeField] private Image dashCooltimeFill;
    [SerializeField] private Image parryCooltimeFill;

    private PlayerStatus playerStatus;
    private PlayerInput playerInput;

    public override void Init() { }

    public override void Setup()
    {
        playerStatus = Player.Instance?.status;
        playerInput  = Player.Instance?.fsm;
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        UpdateHp();
        UpdateEnemyCount();
        UpdateCooltimes();
    }

    private void UpdateHp()
    {
        if (playerStatus == null) return;
        float cur = playerStatus.CurrentHp;
        float max = playerStatus.MaxHp;
        if (hpFill != null) hpFill.fillAmount = cur / max;
        if (hpText != null) hpText.text = $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateEnemyCount()
    {
        if (enemyCountText == null) return;

        int count = 0;
        if (PoolManager.Instance.activeQPoolDict.TryGetValue("Enemy", out var set))
            count = set.Count;

        enemyCountText.text = $"Remain Enemy : {count}";
    }

    private void UpdateCooltimes()
    {
        if (dashCooltimeFill != null && playerInput != null)
            dashCooltimeFill.fillAmount = playerInput.DashCoolRatio;

        if (parryCooltimeFill != null && playerInput != null)
            parryCooltimeFill.fillAmount = playerInput.ParryCoolRatio;
    }
}
