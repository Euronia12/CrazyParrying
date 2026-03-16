using System.Threading.Tasks;
using UnityEngine;

public class TitleManager : Singleton<TitleManager>
{
    public override void Init()
    {
        base.Init();
    }

    public async Task SetupAsync()
    {
        Init();
        await UIManager.Instance.Show<UITitle>();
    }
}
