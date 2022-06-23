using UnityEngine;

public abstract class MonoSingletonTemplate<T> : MonoBehaviour where T : MonoSingletonTemplate<T>
{
    private static T inst;

    public static T sharedInst
    {
        get
        {
            if (inst == null)
            {
                inst = GameObject.FindObjectOfType<T>();
                if (inst == null)
                {
                    var o = new GameObject(typeof(T).Name);
                    inst = o.AddComponent<T>();
                }

                DontDestroyOnLoad(inst);
            }

            return inst;
        }
    }

    public virtual void Init()
    {
    }
}