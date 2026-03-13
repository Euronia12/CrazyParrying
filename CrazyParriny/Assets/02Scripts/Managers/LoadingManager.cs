using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.ResourceProviders;

public class LoadingManager : Singleton<LoadingManager>
{
    public static string nextSceneAddress;
    public static Action OnComplete = null;
    private bool isLoading = false;

    public void LoadScene(string sceneAddress)
    {
        if(isLoading) return;
        isLoading = true;

        nextSceneAddress = sceneAddress;
        StartCoroutine(CoLoadScene());
    }

    IEnumerator CoLoadScene()
    {
        yield return null;

        // 1. 주소 기반 씬 로드 시작 (Single 모드)
        var sceneHandle = Addressables.LoadSceneAsync(nextSceneAddress, LoadSceneMode.Single);
        sceneHandle.Completed += handle =>
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();
        };

        // 2. 로딩 퍼센트 기다림 (비동기)
        while (!sceneHandle.IsDone)
        {
            yield return null;
        }

        // 3. 완료 콜백
        OnComplete?.Invoke();
        OnComplete = null;

        // 4. 페이드 처리

        isLoading = true;
    }
}
