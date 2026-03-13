using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceManager : Singleton<ResourceManager>
{
    public Dictionary<string, Sprite> spriteCache = new();

    public Dictionary<eAddressableType, Dictionary<string, AddressableMap>> addressableMap
        = new Dictionary<eAddressableType, Dictionary<string, AddressableMap>>();
    public bool isInit { get; private set; }

    public override void Init()
    {
        base.Init();
        
    }

    public async Task LoadResource()
    {
        Init();
        await CLoadAddressable();
    }

    public async Task CLoadAddressable()
    {
        await Addressables.InitializeAsync();
        Debug.Log("어드레서블 초기화 완료");
        var handle = Addressables.DownloadDependenciesAsync("InitDownload");
        Debug.Log("다운로드 시작");

        while (!handle.IsDone)
        {
            Debug.Log($"다운로드 진행률: {handle.PercentComplete * 100}%");
            await UniTask.Yield();
        }
        Debug.Log("다운로드 완료 상태 확인");

        switch (handle.Status)
        {
            case AsyncOperationStatus.None:
                break;
            case AsyncOperationStatus.Succeeded:
                Debug.Log("다운로드 성공!");
                break;
            case AsyncOperationStatus.Failed:
                Debug.Log("다운로드 실패 : " + handle.OperationException.Message);
                Debug.LogError(handle.OperationException.ToString());
                break;
            default:
                break;
        }
        Addressables.Release(handle);
        InitAddressableMap();
    }

    private void InitAddressableMap()
    {
        int adIndex = 0;
        Addressables.LoadAssetsAsync<TextAsset>("AddressableMap", (text) =>
        {
            var map = JsonUtility.FromJson<AddressableMapData>(text.text);
            var key = eAddressableType.prefab;
            Dictionary<string, AddressableMap> mapDic = new Dictionary<string, AddressableMap>();
            foreach (var data in map.list)
            {
                key = data.addressableType;
                if (!mapDic.ContainsKey(data.key))
                    mapDic.Add(data.key, data);
            }
            if (!addressableMap.ContainsKey(key)) addressableMap.Add(key, mapDic);
            if (adIndex++ == (int)eAddressableType.max - 1)
            {

                Debug.Log(adIndex);
                isInit = true;
            }
        });

    }

    public List<string> GetPaths(string key, eAddressableType addressableType, eAssetType assetType)
    {
        var keys = new List<string>(addressableMap[addressableType].Keys);
        keys.RemoveAll(obj => !obj.Contains(key));
        List<string> retList = new List<string>();
        keys.ForEach(obj =>
        {
            if (addressableMap[addressableType][obj].assetType == assetType)
                retList.Add(addressableMap[addressableType][obj].path);
        });
        return retList;
    }

    public string GetPath(string key, eAddressableType addressableType)
    {
        var map = addressableMap[addressableType][key];
        return map.path;
    }

    public async Task LoadAssets<T>(string key, eAddressableType addressableType, eAssetType assetType, Action<List<T>> callback)
    {
         await CLoadAssets(key, addressableType, assetType, callback);
    }

    async Task CLoadAssets<T>(string key, eAddressableType addressableType, eAssetType assetType, Action<List<T>> callback)
    {
        var paths = GetPaths(key, addressableType, assetType);
        List<T> retList = new List<T>();
        foreach (var path in paths)
        {
            await CLoadAsset<T>(path, obj =>
            {
                retList.Add(obj);
            });
        }
        await UniTask.WaitUntil(() => paths.Count == retList.Count);
        callback.Invoke(retList);
    }

    public async Task LoadAsset<T>(string key, eAddressableType addressableType, Action<T> callback)
    {
        var path = GetPath(key, addressableType);
        await LoadAsset<T>(path, callback);
    }

    public async Task LoadAsset<T>(string path, Action<T> callback)
    {
        await (CLoadAsset(path, callback));
    }

    public async Task CLoadAsset<T>(string path, Action<T> callback)
    {
        if (path.Contains(".prefab") && typeof(T) != typeof(GameObject) || path.Contains("UI/"))
        {
            var handler = Addressables.LoadAssetAsync<GameObject>(path);
            handler.Completed += (op) =>
            {
                callback.Invoke(op.Result.GetComponent<T>());
            };
            await handler;
        }
        else
        {
            var handler = Addressables.LoadAssetAsync<T>(path);
            handler.Completed += (op) =>
            {
                callback.Invoke(op.Result);
            };
            await handler;
        }
    }
}
