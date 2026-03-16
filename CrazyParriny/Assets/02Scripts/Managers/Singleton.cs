using UnityEngine;
using Sirenix.OdinInspector;
public class Singleton<T> : SerializedMonoBehaviour where T : SerializedMonoBehaviour
{
    private static T instance;
    public static T Instance
    {
        get 
        {
            if (isQuitting) return null;
            if (instance == null)
            {
                instance = FindFirstObjectByType<T>();
                if(instance == null)
                {
                    instance = new GameObject(typeof(T).Name).AddComponent<T>();
                }
            }
            return instance; 
        }
        protected set { instance = value; }
    }
    [SerializeField] private bool isDontDestory = true;
    protected static bool isQuitting = false; // 앱 종료 시 생성 방지용

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            if (isDontDestory)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else
        {
            Debug.LogError($"{typeof(T)} 중복");
            Destroy(gameObject);
        }
    }

    public virtual void Init() { }

    protected virtual void OnApplicationQuit()
    {
        isQuitting = true;
    }
}
