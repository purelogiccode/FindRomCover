using System.Windows.Input;

namespace FindRomCover.Services;

/// <summary>
///     Minimal <see cref="ICommand" /> implementation that delegates execution and
///     availability checks to caller-supplied callbacks.
/// </summary>
public class DelegateCommand : ICommand, IDisposable
{
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<object?> _execute;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DelegateCommand" /> class.
    /// </summary>
    /// <param name="execute">The action invoked when the command executes.</param>
    /// <param name="canExecute">Optional predicate that determines whether the command can execute.</param>
    public DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    ///     Determines whether the command can execute for the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    /// <returns>True when execution is allowed; otherwise false.</returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <summary>
    ///     Executes the command action for the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <summary>Raised when the result of <see cref="CanExecute" /> may have changed.</summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>Raises the <see cref="CanExecuteChanged" /> event.</summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Releases resources used by the command.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
