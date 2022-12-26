namespace ADLib.Util
{
    public class SynchronizedObject<T>
    {
        private readonly object _lock = new();

        private T? _data;

        public SynchronizedObject(T? data)
        {
            _data = data;
        }

        public T? Get()
        {
            lock (_lock)
            {
                return _data;
            }
        }

        public void Set(T? value)
        {
            lock (_lock)
            {
                _data = value;
            }
        }

        // Use this to avoid race conditions if you must read the value before changing it
        public T? ApplyFunction(Func<T?, T?> function)
        {
            lock (_lock)
            {
                _data = function(_data);
                return _data;
            }
        }
    }
}