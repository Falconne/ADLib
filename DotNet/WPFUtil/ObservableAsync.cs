using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WPFUtil
{
    public class ObservableAsync<T> : ViewModelBase
    {
        private T? _value;

        private readonly Func<T?, Task> _onChange;

        public ObservableAsync(Func<T?, Task> onChange)
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