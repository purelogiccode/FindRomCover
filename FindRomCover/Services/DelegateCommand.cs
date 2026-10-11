using System.Windows.Input;

namespace FindRomCover.Services;

public class DelegateCommand : ICommand, IDisposable
{
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<object?> _execute;

    public DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
