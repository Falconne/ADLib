using System;
using System.Collections.Generic;

namespace WPFUtil
{
    public class Observable<T> : ViewModelBase
    {
        private T? _value;

        private readonly Action<T?>? _onChange;

        public Observable(T? defaultValue = default, Action<T?>? onChange = null)
        {
            _value = defaultValue;
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