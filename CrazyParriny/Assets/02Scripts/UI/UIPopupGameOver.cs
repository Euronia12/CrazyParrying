using UnityEngine;
using UnityEngine.UI;

public class UIPopupGameOver : UIPopup
{
    [SerializeField] Button restartBtn;
    [SerializeField] Button changeDiffBtn;
    [SerializeField] Button titleBtn;

    public override void Init()
    {
        if (isInit) return;
        isInit = true;
        base.Init();

        restartBtn?.onClick.AddListener(() => InGameManager.Instance.RestartFromStage1().Forget());
        changeDiffBtn?.onClick.AddListener(() => InGameManager.Instance.ChangeDifficulty().Forget());
        titleBtn?.onClick.AddListener(() => InGameManager.Instance.GoToTitle().Forget());
    }

    private void OnDestroy()
    {
        restartBtn?.onClick.RemoveAllListeners();
        changeDiffBtn?.onClick.RemoveAllListeners();
        titleBtn?.onClick.RemoveAllListeners();
    }
}
