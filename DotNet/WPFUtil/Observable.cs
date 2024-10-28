using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WPFUtil;

public class Observable<T> : PropertyContainerBase
{
    private ExceptionHandler? _errorHandler;

    private Action<T?>? _onChange;

    private Func<T?, Task>? _onChangeAsync;

    private T? _value;

    public Observable(T? defaultValue = default)
    {
        _value = defaultValue;
    }

    public T? Value
    {
        get => _value;

        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }

            _value = value;
            _onChange?.Invoke(_value);
            _onChangeAsync?.Invoke(_value).FireAndForget(_errorHandler!);
            OnPropertyChanged();
        }
    }

    public Observable<T> WithChangeHandler(Action<T?> onChange)
    {
        _onChange = onChange;
        return this;
    }

    public Observable<T> WithChangeHandlerAsync(Func<T?, Task> onChangeAsync, ExceptionHandler errorHandler)
    {
        _onChangeAsync = onChangeAsync;
        _errorHandler = errorHandler;
        return this;
    }
}