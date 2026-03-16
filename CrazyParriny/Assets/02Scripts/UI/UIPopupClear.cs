using UnityEngine;
using UnityEngine.UI;

public class UIPopupClear : UIPopup
{
    [SerializeField] Button continueBtn;

    private bool _isLastStage;

    public override void Init()
    {
        if (isInit) return;
        isInit = true;
        base.Init();

        continueBtn?.onClick.AddListener(OnContinue);
    }

    public void Setup(bool isLastStage)
    {
        _isLastStage = isLastStage;
        base.Setup();
    }

    private void OnContinue()
    {
        if (_isLastStage) InGameManager.Instance.GoToClearMap().Forget();
        else              InGameManager.Instance.GoNextStage().Forget();
    }

    private void OnDestroy()
    {
        continueBtn?.onClick.RemoveAllListeners();
    }
}
