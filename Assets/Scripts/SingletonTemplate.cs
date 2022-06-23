public abstract class SingletonTemplate<T> where T : SingletonTemplate<T>, new()
{
    private static T inst;

    public static T sharedInst
    {
        get
        {
            if (inst == null) inst = new T();

            return inst;
        }
    }

    public virtual void Init()
    {
    }
}