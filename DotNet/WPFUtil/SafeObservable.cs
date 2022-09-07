using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WPFUtil
{
    public class SafeObservable<T> : ViewModelBase
    {
        private T _value;

        private Action<T>? _onChange;

        private Func<T, Task>? _onChangeAsync;

        public SafeObservable(T defaultValue)
        {
            _value = defaultValue;
        }

        public SafeObservable<T> WithOnChangeHandler(Action<T> onChange)
        {
            _onChange = onChange;
            return this;
        }

        public SafeObservable<T> WithOnChangeHandlerAsync(Func<T, Task> onChange)
        {
            _onChangeAsync = onChange;
            return this;
        }

        public T Value
        {
            get => _value;

            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value))
                    return;

                _value = value;
                _onChange?.Invoke(_value);
                _ = _onChangeAsync?.Invoke(_value);
                OnPropertyChanged();
            }
        }

    }
}