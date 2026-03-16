using UnityEngine;
using UnityEngine.UI;

public class UIPopupPause : UIPopup
{
    [SerializeField] Button resumeBtn;
    [SerializeField] Button restartBtn;
    [SerializeField] Button titleBtn;

    public override void Init()
    {
        if (isInit) return;
        isInit = true;
        base.Init();

        resumeBtn?.onClick.AddListener(OnResume);
        restartBtn?.onClick.AddListener(() => InGameManager.Instance.RestartFromStage1().Forget());
        titleBtn?.onClick.AddListener(() => InGameManager.Instance.GoToTitle().Forget());
    }

    private void OnResume()
    {
        Time.timeScale = 1f;
        UIManager.Instance.Hide<UIPopupPause>();
    }

    private void OnDestroy()
    {
        resumeBtn?.onClick.RemoveAllListeners();
        restartBtn?.onClick.RemoveAllListeners();
        titleBtn?.onClick.RemoveAllListeners();
    }
}
