using UnityEngine;
using UnityEngine.UI;

public class UITitle : UIDefault
{
    [SerializeField] Button playBtn;
    [SerializeField] Button exitBtn;

    public override void Init()
    {
        playBtn?.onClick.AddListener(OnClickStartButton);
        exitBtn?.onClick.AddListener(OnClickExitButton);
    }

    public override void Setup()
    {
        base.Setup();
    }

    public void OnClickStartButton()
    {
        LoadingManager.Instance.LoadScene("Main", () =>
        {
            InGameManager.Instance.Init();
        });

    }

    public void OnClickExitButton()
    {
        GameManager.Instance.OnQuitGame();
    }
}
