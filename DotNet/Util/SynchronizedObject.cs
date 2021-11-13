namespace ADLib.Util
{
    public class SynchronizedObject<T>
    {
        private readonly object _lock = new object();

        private T _data;

        public SynchronizedObject(T data)
        {
            _data = data;
        }

        public T Get()
        {
            lock (_lock)
            {
                return _data;
            }
        }

        public void Set(T value)
        {
            lock (_lock)
            {
                _data = value;
            }
        }
    }
}