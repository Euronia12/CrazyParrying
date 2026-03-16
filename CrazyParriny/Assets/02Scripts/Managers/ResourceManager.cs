using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        var handle = Addressables.DownloadDependenciesAsync("InitDownload");

        while (!handle.IsDone)
            await UniTask.Yield();

        if (handle.Status == AsyncOperationStatus.Failed)
            Debug.LogError(handle.OperationException.ToString());
        Addressables.Release(handle);
        await InitAddressableMap();
    }

    private async UniTask InitAddressableMap()
    {
        foreach (eAddressableType type in Enum.GetValues(typeof(eAddressableType)))
        {
            addressableMap[type] = new Dictionary<string, AddressableMap>();
        }

        var assets = await Addressables.LoadAssetsAsync<TextAsset>("AddressableMap", null);

        if (assets == null || assets.Count == 0)
        {
            Debug.LogError("[ResourceManager] AddressableMap ���̺��� ���� ������ ã�� �� �����ϴ�!");
            return;
        }
        
        foreach (var textAsset in assets)
        {
            var mapData = JsonUtility.FromJson<AddressableMapData>(textAsset.text);
            if (mapData == null || mapData.list.Count == 0) continue;

            foreach (var data in mapData.list)
            {
                addressableMap[data.addressableType].TryAdd(data.key, data);
            }
        }

        Addressables.Release(assets);

        isInit = true;
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

    public T GetAsset<T>(string key)
    {
        // ��巹���� ���� �ε� �ٽ�: .WaitForCompletion()
        var handle = Addressables.LoadAssetAsync<T>(key);
        var loadedPrefab = handle.WaitForCompletion();
        return loadedPrefab;
    }
}
