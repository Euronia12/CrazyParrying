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

    }

    private async void Init()
    {

    }
}
