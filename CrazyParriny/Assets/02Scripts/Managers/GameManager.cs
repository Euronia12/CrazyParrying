using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    protected override void Awake()
    {
        base.Awake();
        SetDefaultGameSettings();
    }

    private void Start()
    {
        Init();
    }

    private void SetDefaultGameSettings()
    {
        Application.targetFrameRate = 60;
    }

    public async override void Init()
    {
        await ResourceManager.Instance.LoadResource();
        //await DataManager.Instance.Prewarm();
        UIManager.Instance.Init();
        await TitleManager.Instance.SetupAsync();

        await PoolManager.Instance.PreWarm();
        await SoundManager.Instance.Prewarm();
    }

    public void OnQuitGame()
    {
        isQuitting = true;
        Application.Quit();
    }
}
