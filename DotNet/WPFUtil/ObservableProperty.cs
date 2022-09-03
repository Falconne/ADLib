namespace WPFUtil
{
    public class ObservableProperty<T> : ViewModelBase
    {
        private T? _value;

        private readonly Action<T?>? _onChange;

        public ObservableProperty(Action<T?>? onChange = null)
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
                _onChange?.Invoke(_value);
                OnPropertyChanged();
            }
        }

    }
}