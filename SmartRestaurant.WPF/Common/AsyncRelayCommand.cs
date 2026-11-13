using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SmartRestaurant.WPF.Common;

public interface IAsyncRelayCommand : ICommand
{
    bool IsExecuting { get; }
    Task ExecuteAsync(object? parameter = null);
}

public interface IAsyncRelayCommand<in T> : ICommand
{
    bool IsExecuting { get; }
    Task ExecuteAsync(T? parameter);
}

/// <summary>
/// Asynchronous ICommand preventing UI thread freezes during non-blocking operations.
/// </summary>
public class AsyncRelayCommand : IAsyncRelayCommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            _isExecuting = value;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute == null || _canExecute());

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (CanExecute(parameter))
        {
            try
            {
                IsExecuting = true;
                await _execute();
            }
            finally
            {
                IsExecuting = false;
            }
        }
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter);
    }
}

public class AsyncRelayCommand<T> : IAsyncRelayCommand<T>
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            _isExecuting = value;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        if (_isExecuting) return false;
        if (_canExecute == null) return true;
        return parameter is T t ? _canExecute(t) : _canExecute(default);
    }

    public async Task ExecuteAsync(T? parameter)
    {
        if (CanExecute(parameter))
        {
            try
            {
                IsExecuting = true;
                await _execute(parameter);
            }
            finally
            {
                IsExecuting = false;
            }
        }
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter is T t ? t : default);
    }
}
