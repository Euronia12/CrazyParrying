using Cysharp.Threading.Tasks;
using Sirenix.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    public Dictionary<string, UIBase> uiList = new();
    public HashSet<string> loadKey = new();
    [SerializeField] private List<Transform> parents;
    public override void Init()
    {
        base.Init();
    }

    public async UniTask<T> Show<T>() where T : UIBase
    {
        var key = typeof(T).ToString();

        if (uiList.TryGetValue(key, out var ui))
        {
            ui.Setup();
            return (T)ui;
        }

        if (loadKey.Contains(key))
        {
            Debug.LogWarning($"{key} is already loading.");
            return null;
        }

        loadKey.Add(key);
        try
        {
            T prefab = null;
            await ResourceManager.Instance.LoadAsset<T>(key, eAddressableType.ui, obj =>
            {
                prefab = obj;
            });

            if (prefab == null) return null;

            ui = Instantiate(prefab, parents[(int)prefab.uiPosition]);
            ui.name = key;
            uiList.Add(key, ui);
            ui.Init();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{key} Load Failed: {e.Message}");
            return null;
        }
        finally
        {
            loadKey.Remove(key);
        }

        ui.Setup();
        return (T)ui;
    }

    public void Hide<T>(params object[] param) where T : UIBase
    {
        var key = typeof(T).ToString();

        if (uiList.TryGetValue(key, out var ui))
        {
            ui.Closed(param);
            ui.SetActive(false);
        }
    }

    public T Get<T>() where T : UIBase
    {
        if (uiList.TryGetValue(typeof(T).ToString(), out var ui))
            return (T)ui;

        return null;
    }

    public bool IsOpened<T>() where T : UIBase
    {
        if (uiList.TryGetValue(typeof(T).ToString(), out var ui))
            return ui != null && ui.gameObject.activeInHierarchy;

        return false;
    }

}
