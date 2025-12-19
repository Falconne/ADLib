using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WPFUtil;

public class Observable<T> : PropertyContainerBase
{
    public Observable(T? defaultValue = default)
    {
        _value = defaultValue;
    }

    // Allow easy assignment from the underlying value type in tests and initializers
    public static implicit operator Observable<T>(T? value) => new Observable<T>(value);

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

    private ExceptionHandler? _errorHandler;

    private Action<T?>? _onChange;

    private Func<T?, Task>? _onChangeAsync;

    private T? _value;

    public Observable<T> WithChangeHandler(Action<T?> onChange)
    {
        _onChange = onChange;
        return this;
    }

    public Observable<T> WithChangeHandlerAsync(
        Func<T?, Task> onChangeAsync,
        ExceptionHandler? errorHandler = null)
    {
        _onChangeAsync = onChangeAsync;
        _errorHandler = errorHandler ?? TopLevelExceptionHandler.ShowError;
        return this;
    }
}