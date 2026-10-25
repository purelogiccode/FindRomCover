namespace FindRomCover.Services;

/// <summary>
///     Detects a burst of image file-system events that indicates a bulk import (copying or
///     extracting many covers at once). While a bulk import is active the image folder
///     watcher must leave the incoming files untouched instead of renaming every one of them
///     to the currently selected missing ROM.
/// </summary>
internal sealed class BulkImportDetector
{
    private readonly TimeSpan _cooldown;
    private readonly Queue<long> _eventTicks = new();
    private readonly Lock _lock = new();
    private readonly int _threshold;
    private readonly TimeSpan _window;
    private long _activeUntilTicks;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BulkImportDetector" /> class.
    /// </summary>
    /// <param name="threshold">Number of events within the window that marks a bulk import.</param>
    /// <param name="window">Time span in which the events must arrive.</param>
    /// <param name="cooldown">Time span the detector stays active after the last event.</param>
    internal BulkImportDetector(int threshold, TimeSpan window, TimeSpan cooldown)
    {
        if (threshold < 2) throw new ArgumentOutOfRangeException(nameof(threshold));
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        if (cooldown <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cooldown));

        _threshold = threshold;
        _window = window;
        _cooldown = cooldown;
    }

    /// <summary>
    ///     Gets a value indicating whether a bulk import is currently active.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns><see langword="true" /> while incoming files must be left untouched.</returns>
    internal bool IsActive(DateTime utcNow)
    {
        lock (_lock)
        {
            return utcNow.Ticks < _activeUntilTicks;
        }
    }

    /// <summary>
    ///     Records an image event and reports whether it belongs to a bulk import.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns><see langword="true" /> when a bulk import is active after this event.</returns>
    internal bool Record(DateTime utcNow)
    {
        lock (_lock)
        {
            if (utcNow.Ticks < _activeUntilTicks)
            {
                // Keep the detector active while files keep arriving so a long copy or
                // extraction cannot be renamed halfway through.
                _activeUntilTicks = utcNow.Ticks + _cooldown.Ticks;
                return true;
            }

            _eventTicks.Enqueue(utcNow.Ticks);

            var cutoff = utcNow.Ticks - _window.Ticks;
            while (_eventTicks.Count > 0 && _eventTicks.Peek() < cutoff) _eventTicks.Dequeue();

            if (_eventTicks.Count < _threshold) return false;

            _eventTicks.Clear();
            _activeUntilTicks = utcNow.Ticks + _cooldown.Ticks;
            return true;
        }
    }
}
