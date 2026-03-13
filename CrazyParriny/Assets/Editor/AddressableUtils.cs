using UnityEngine;
using UnityEditor;
using System.IO;
using System.Net;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets;

internal class AddressableUtils : Editor
{
    const string assetBundlePath = "Assets/AddressableDatas";

    [MenuItem("ad/Addressable/Addressable Build")]
    internal static void BuildAddressable()
    {
        Mapping();
        AddressableAssetSettingsDefaultObject.Settings.OverridePlayerVersion = Application.version;
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);//
        var uploadList = result.FileRegistry.GetFilePaths().ToList();
        uploadList.RemoveAll(obj => !obj.Contains("ServerData"));
        var list = "";
        uploadList.ForEach(obj => list += obj + "\n");
        var path = Application.dataPath + "/lastBuildData.txt";
        File.WriteAllText(path, list);
    }

    [InitializeOnEnterPlayMode]
    [MenuItem("ad/Addressable/Mapping")]
    internal static void Mapping()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        foreach (var group in settings.groups)
        {
            if (!Enum.TryParse(group.Name, out eAddressableType type))
            {
                Debug.LogWarning($"[AddressableUtils] 그룹 이름 '{group.Name}'은 eAddressableType에 없음");
                continue;
            }

            foreach (var entry in group.entries)
            {
                if (!entry.AssetPath.Contains("Assets") || entry.AssetPath.Contains("addressableMap"))
                    continue;

                string dir = Application.dataPath + entry.AssetPath.Replace("Assets", "");

                if (!Directory.Exists(dir))
                {
                    Debug.LogWarning($"[AddressableUtils] 경로가 폴더가 아님 (스킵됨): {dir}");
                    continue;
                }

                var mapData = SetMapping(dir, type);
                var newPath = entry.AssetPath + "/addressableMap.json";
                var dt = JsonUtility.ToJson(mapData);
                File.WriteAllText(newPath, dt);
                AssetDatabase.ImportAsset(newPath);
            }
        }
    }

    static AddressableMapData SetMapping(string dir, eAddressableType type)
    {
        var files = Directory.GetFiles(dir).ToList();
        files.RemoveAll(obj => obj.Contains(".meta"));
        AddressableMapData mapData = new AddressableMapData();
        var dirs = Directory.GetDirectories(dir).ToList();
        foreach (var d in dirs)
        {
            if (d.Contains("Atlas")) continue;
            var res = SetMapping(d, type);
            mapData.AddRange(res.list);
        }
        foreach (var file in files)
        {
            var path = file.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            var data = new AddressableMap();
            data.addressableType = type;
            data.assetType = path.Contains(".png") ? eAssetType.sprite : path.Contains(".json") ? eAssetType.jsondata : eAssetType.prefab;
            data.key = Path.GetFileName(path).Split('.').First();
            data.path = path;
            mapData.Add(data);
        }
        return mapData;
    }

}
