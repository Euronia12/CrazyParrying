using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : ObjectPoolBase, IDamageable
{
    private static Player instance;
    public static Player Instance => instance;

    public PlayerStatus status;
    public PlayerInput fsm;
    public PlayerEffect effect;

    private void Update()
    {
        float rawH = Input.GetAxisRaw("Horizontal");
        float h = (rawH == 0) ? 0 : Mathf.Sign(rawH);

        float rawV = Input.GetAxisRaw("Vertical");
        float v = (rawV == 0) ? 0 : Mathf.Sign(rawV);

        fsm.MoveAction(h, v);
    }

    private void OnDestroy()
    {
        if (instance != null && instance == this)
            instance = null;
    }

    public override void Init()
    {
        base.Init();

        if (isInit) return;
        isInit = true;

        if (instance == null)
            instance = this;
        else
        {
            Destroy(this);
            return;
        }

        fsm.Init();
        status.Init();
        effect.Init();
    }

    public override void Setup()
    {
        var cam = Camera.main;
        if (cam != null) cam.transform.SetParent(transform);
        fsm.Setup();
        status.Setup();
        effect.Setup();

        ShowGameUI().Forget();
    }

    private async UniTaskVoid ShowGameUI()
    {
        await UIManager.Instance.Show<UINaviPlayerHit>();
        await UIManager.Instance.Show<UIMain>();

        // UIMain은 Setup 이후 참조가 필요하므로 Show 뒤에 Setup 재호출
        UIManager.Instance.Get<UIMain>()?.Setup();
    }

    public override void OnDispawn()
    {
        // SetActive(false) 전에 카메라 분리 — 카메라가 Player 자식이면 같이 꺼지므로
        var cam = Camera.main;
        if (cam != null && cam.transform.parent == transform)
        {
            cam.transform.SetParent(null);
            // Player는 DontDestroyOnLoad 계층이므로 SetParent(null)만 하면
            // 카메라가 DontDestroyOnLoad 루트에 남아 씬 재로드 시 중복 카메라 발생.
            // 활성 씬으로 되돌려 씬 생명주기를 따르게 함.
            SceneManager.MoveGameObjectToScene(cam.gameObject, SceneManager.GetActiveScene());
        }

        UIManager.Instance.Hide<UINaviPlayerHit>();
        UIManager.Instance.Hide<UIMain>();

        base.OnDispawn();
    }

    public void WarpTo(Vector3 pos)
    {
        fsm.WarpTo(pos);
    }

    public void TakeDamage(float damage)
    {
        status.TakeDamage(damage);
    }
}
