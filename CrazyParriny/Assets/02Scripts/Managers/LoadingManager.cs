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

    public void LoadScene(string sceneAddress, Action action = null)
    {
        if(isLoading) return;
        isLoading = true;

        nextSceneAddress = sceneAddress;
        StartCoroutine(CoLoadScene(action));
    }

    IEnumerator CoLoadScene(Action action = null)
    {
        yield return null;

        var sceneHandle = SceneManager.LoadSceneAsync(nextSceneAddress);

        while (!sceneHandle.isDone)
        {
            yield return null;
        }

        Resources.UnloadUnusedAssets();
        GC.Collect();

        OnComplete?.Invoke();
        OnComplete = null;

        action?.Invoke();

        UIManager.Instance.SetCanvasCamera();
        isLoading = false;
    }
}
