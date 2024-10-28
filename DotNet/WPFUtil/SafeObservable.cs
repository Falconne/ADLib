using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WPFUtil;

public class SafeObservable<T> : PropertyContainerBase where T : notnull
{
    private Action<Exception>? _errorHandler;

    private Action<T>? _onChange;

    private Func<T, Task>? _onChangeAsync;

    private T _value;

    public SafeObservable(T defaultValue)
    {
        _value = defaultValue;
    }

    public T Value
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

    public SafeObservable<T> WithOnChangeHandler(Action<T> onChange)
    {
        _onChange = onChange;
        return this;
    }

    public SafeObservable<T> WithOnChangeHandlerAsync(
        Func<T, Task> onChange,
        Action<Exception> errorHandler)
    {
        _onChangeAsync = onChange;
        _errorHandler = errorHandler;
        return this;
    }

    public override string? ToString()
    {
        return Value?.ToString();
    }
}