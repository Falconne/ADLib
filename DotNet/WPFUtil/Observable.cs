using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WPFUtil
{
    public class Observable<T> : PropertyContainerBase
    {
        private T? _value;

        private Action<T?>? _onChange;

        private Func<T?, Task>? _onChangeAsync;

        private Action<Exception>? _errorHandler;


        public Observable(T? defaultValue = default)
        {
            _value = defaultValue;
        }

        public Observable<T> WithChangeHandler(Action<T?> onChange)
        {
            _onChange = onChange;
            return this;
        }

        public Observable<T> WithChangeHandlerAsync(Func<T?, Task> onChangeAsync, Action<Exception> errorHandler)
        {
            _onChangeAsync = onChangeAsync;
            _errorHandler = errorHandler;
            return this;
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
                _onChangeAsync?.Invoke(_value).FireAndForget(_errorHandler!);
                OnPropertyChanged();
            }
        }

    }
}