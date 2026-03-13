using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
        await PoolManager.Instance.PreWarm();
        await SoundManager.Instance.Prewarm();
        //await DataManager.Instance.Prewarm();
        
        UIManager.Instance.Init();
        TitleManager.Instance.Init();
    }
}
