namespace ADLib.Util
{
    public class SafeSynchronizedObject<T>
    {
        private readonly object _lock = new();

        private T _data;

        public SafeSynchronizedObject(T data)
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