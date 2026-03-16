using UnityEngine;
using UnityEngine.SceneManagement;
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
        // GoToTitle()은 씬 전환 없이 UITitle만 오버레이로 띄움.
        // 이미 Main 씬이면 DespawnAll()로 정리된 상태이므로 씬 리로드 없이 바로 Init.
        // Title 씬(또는 다른 씬)에서 왔을 경우에만 Main 씬을 로드.
        if (SceneManager.GetActiveScene().name == "Main")
        {
            InGameManager.Instance.Init();
        }
        else
        {
            LoadingManager.Instance.LoadScene("Main", () =>
            {
                InGameManager.Instance.Init();
            });
        }
    }

    public void OnClickExitButton()
    {
        GameManager.Instance.OnQuitGame();
    }
}
