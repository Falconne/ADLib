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

        // Raised on the thread that changed the value, outside the lock
        public event Action<T>? Changed;

        public T Get()
        {
            lock (_lock)
            {
                return _data;
            }
        }

        public void Set(T value)
        {
            T oldValue;
            lock (_lock)
            {
                oldValue = _data;
                _data = value;
            }

            RaiseChangedIfDifferent(oldValue, value);
        }

        // Use this to avoid race conditions if you must read the value before changing it
        public T ApplyFunction(Func<T, T> function)
        {
            T oldValue;
            T newValue;
            lock (_lock)
            {
                oldValue = _data;
                _data = newValue = function(_data);
            }

            RaiseChangedIfDifferent(oldValue, newValue);
            return newValue;
        }

        private void RaiseChangedIfDifferent(T oldValue, T newValue)
        {
            if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
            {
                Changed?.Invoke(newValue);
            }
        }
    }
}