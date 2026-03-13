using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    public List<UIBase> uiList = new List<UIBase>();
    [SerializeField] private List<Transform> parents;
    public override void Init()
    {
        base.Init();
    }

    public void SetParents(List<Transform> parents)
    {

    }

    public async Task<T> Show<T>() where T : UIBase
    {
        var ui = Instance.uiList.Find(obj => obj.name == typeof(T).ToString());
        if (ui == null)
        {

            T prefab = null;
            //await ResourceManager.Instance.LoadAsset<T>(typeof(T).ToString(), eAddressableType.ui, obj =>
            //{
            //    prefab = obj;
            //});
            await UniTask.WaitUntil(() => prefab != null);

            ui = Instance.uiList.Find(obj => obj.name == typeof(T).ToString());

            if (ui != null)
            {
                ui.SetActive(true);
                ui.SetActive(false);
                return (T)ui;
            }

            ui = Instantiate(prefab, Instance.parents[(int)prefab.uiPosition]);
            ui.name = ui.name.Replace("(Clone)", "");
            if (ui.uiPosition == eUIPosition.Default)
            {
                Instance.uiList.ForEach(obj =>
                {
                    if (obj.uiPosition == eUIPosition.Default) obj.gameObject.SetActive(false);
                });
            }
            Instance.uiList.Add(ui);
        }
        ui.SetActive(true);
        ui.SetActive(false);
        return (T)ui;
    }

    public void Hide<T>(params object[] param) where T : UIBase
    {
        var ui = Instance.uiList.Find(obj => obj.name == typeof(T).ToString());
        if (ui != null)
        {
            if (ui.uiPosition == eUIPosition.Default)
            {
                var prevUI = Instance.uiList.FindLast(obj => obj.uiPosition == eUIPosition.Default);
                prevUI.SetActive(true);
            }
            //ui.closed?.Invoke(param);
            //if (ui.uiOptions.isDestroyOnHide)
            {
                Instance.uiList.Remove(ui);
                Destroy(ui.gameObject);
            }
            //else
            {
                ui.SetActive(false);
            }
        }
    }


    public T Get<T>() where T : UIBase
    {
        var temp = (T)Instance.uiList.Find(obj => obj.name == typeof(T).ToString());
        if (temp == null)
        {
            Debug.Log("데이터 UI 없음");
            return null;
        }
        return temp;
    }

    public bool IsOpened<T>() where T : UIBase
    {
        var ui = Instance.uiList.Find(obj => obj.name == typeof(T).ToString());
        return ui != null && ui.gameObject.activeInHierarchy;
    }

}
