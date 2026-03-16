using UnityEngine;
using UnityEngine.UI;

public class UIPopupDiffSettings : UIPopup
{
    [SerializeField] Button[] diffBtn = new Button[3];

    public override void Init()
    {
        if (isInit) return;
        isInit = true;

        base.Init();
        SetButtonEvent();
    }

    public override void Setup()
    {
        base.Setup();
    }

    private void SetButtonEvent()
    {
        for (int i = 0; i < diffBtn.Length; i++)
        {
            int idx = i;
            diffBtn[i].onClick.AddListener(() => SetGameDiff(idx));
        }
    }

    private void SetGameDiff(int idx)
    {
        var inGameMgr = InGameManager.Instance;
        if (inGameMgr._isSettingUp) return;
        inGameMgr._isSettingUp = true;
        inGameMgr.Setup(idx, () => UIManager.Instance.Hide<UIPopupDiffSettings>());
    }


    private void OnDestroy()
    {
        for (int i = 0; i < diffBtn.Length; i++)
        {
            diffBtn[i].onClick.RemoveAllListeners();
        }
    }
}
