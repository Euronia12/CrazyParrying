using Sirenix.OdinInspector;
using UnityEngine;

public class UIBase : SerializedMonoBehaviour
{
    public eUIPosition uiPosition;
    protected bool isInit = false;

    public virtual void Init() { } // 초기화
    public virtual void Setup() { } //갱신

    public virtual void Opened() { }
    public virtual void Opened(params object[] param) { }
    public virtual void Closed() { }
    public virtual void Closed(params object[] param) { }

    public void SetActive(bool isOn)
    {
        gameObject.SetActive(isOn);
    }
}
