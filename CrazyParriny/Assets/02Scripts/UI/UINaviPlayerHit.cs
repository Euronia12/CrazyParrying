using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UINaviPlayerHit : UIDefault
{
    [SerializeField] private Image overlay;

    private Tween hitTween;

    public override void Init()
    {
        if (overlay != null)
            overlay.color = new Color(0f, 0.3f, 1f, 0f);
    }

    public override void Closed(params object[] param)
    {
        hitTween?.Kill();
        if (overlay != null)
            overlay.color = new Color(0f, 0.3f, 1f, 0f);
    }

    public void PlayHit()
    {
        if (overlay == null) return;

        hitTween?.Kill();
        overlay.color = new Color(0f, 0.3f, 1f, 0f);

        hitTween = DOTween.Sequence()
            .Append(overlay.DOFade(0.45f, 0.2f).SetEase(Ease.OutQuad))
            .Append(overlay.DOFade(0f, 0.7f).SetEase(Ease.InQuad))
            .SetUpdate(true);
    }
}
