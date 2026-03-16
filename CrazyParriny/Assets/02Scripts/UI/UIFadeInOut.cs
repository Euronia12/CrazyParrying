using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIFadeInOut : UIBase
{
    [SerializeField] private Image fadeImage;

    public override void Init()
    {
        if (isInit) return;
        isInit = true;
        if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, 0f);
        gameObject.SetActive(false);
    }

    public async UniTask FadeOut(float duration = 0.4f)
    {
        gameObject.SetActive(true);
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        bool done = false;
        fadeImage.DOFade(1f, duration)
            .SetUpdate(true)
            .OnComplete(() => done = true);
        await UniTask.WaitUntil(() => done);
    }

    public async UniTask FadeIn(float duration = 0.4f)
    {
        bool done = false;
        fadeImage.DOFade(0f, duration)
            .SetUpdate(true)
            .OnComplete(() => done = true);
        await UniTask.WaitUntil(() => done);
        gameObject.SetActive(false);
    }
}
