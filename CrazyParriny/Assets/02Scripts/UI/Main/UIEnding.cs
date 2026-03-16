using UnityEngine;
using UnityEngine.UI;

public class UIEnding : UIDefault
{
    [SerializeField] Button titleBtn;
    [SerializeField] Button exitBtn;

    public override void Init()
    {
        if (isInit) return;
        isInit = true;
        base.Init();

        titleBtn?.onClick.AddListener(() => InGameManager.Instance.GoToTitle().Forget());
        exitBtn?.onClick.AddListener(() => GameManager.Instance.OnQuitGame());
    }

    private void OnDestroy()
    {
        titleBtn?.onClick.RemoveAllListeners();
        exitBtn?.onClick.RemoveAllListeners();
    }
}
