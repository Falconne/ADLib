namespace WPFUtil
{
    public class ObservablePropertyWithOnChangeHandlerAsync<T> : ViewModelBase
    {
        private T? _value;

        private readonly Func<T?, Task> _onChange;

        public ObservablePropertyWithOnChangeHandlerAsync(Func<T?, Task> onChange)
        {
            _onChange = onChange;
        }

        public T? Value
        {
            get => _value;

            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value))
                    return;

                _value = value;
                _ = _onChange(_value);
                OnPropertyChanged();
            }
        }

    }
}