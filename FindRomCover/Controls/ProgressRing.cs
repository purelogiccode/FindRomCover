using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace FindRomCover.Controls;

/// <summary>
///     MahApps ProgressRing equivalent: a small rotating arc shown while a
///     background operation is running.
/// </summary>
public class ProgressRing : Control
{
    /// <summary>Styled property that backs <see cref="IsActive"/>.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsActive));

    /// <summary>Styled property that backs <see cref="StrokeThickness"/>.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(StrokeThickness), 3.0);

    /// <summary>Styled property that backs <see cref="Stroke"/>.</summary>
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ProgressRing, IBrush?>(nameof(Stroke));

    /// <summary>Styled property that backs <see cref="TrackBrush"/>.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ProgressRing, IBrush?>(nameof(TrackBrush));

    private readonly DispatcherTimer _timer;
    private double _angle;

    /// <summary>Initializes a new instance of the <see cref="ProgressRing"/> class and starts its animation timer setup.</summary>
    public ProgressRing()
    {
        Width = 60;
        Height = 60;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _timer.Tick += (_, _) =>
        {
            _angle = (_angle + 12) % 360;
            InvalidateVisual();
        };
    }

    /// <summary>Gets or sets a value indicating whether the ring is spinning.</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Gets or sets the thickness of the drawn arc and track.</summary>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Gets or sets the brush used to draw the rotating arc.</summary>
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Gets or sets the brush used to draw the background track.</summary>
    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <summary>
    ///     Starts or stops the animation timer when the <see cref="IsActive" /> property changes.
    /// </summary>
    /// <param name="change">The property change arguments.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty)
        {
            if (IsActive)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }

            InvalidateVisual();
        }
    }

    /// <summary>
    ///     Stops the animation timer when the control is detached from the visual tree so
    ///     an active ring does not stay rooted after its window is gone.
    /// </summary>
    /// <param name="e">The detach event arguments.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Draws the background track and the rotating arc of the progress ring.</summary>
    /// <param name="context">The drawing context to render the ring into.</param>
    public override void Render(DrawingContext context)
    {
        if (!IsActive) return;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var thickness = StrokeThickness;
        var radius = Math.Max(1, (size - thickness) / 2);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        var stroke = Stroke ?? ResolveBrush("AccentSolidBrush") ?? Brushes.SteelBlue;
        var track = TrackBrush ?? new SolidColorBrush(Color.FromArgb(0x33, 0x80, 0x80, 0x80));

        context.DrawEllipse(null, new Pen(track, 2), center, radius, radius);

        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            var startPoint = PointOnCircle(center, radius, _angle);
            var endPoint = PointOnCircle(center, radius, _angle + 100);
            geometryContext.BeginFigure(startPoint, false);
            geometryContext.ArcTo(endPoint, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            geometryContext.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(stroke, thickness, lineCap: PenLineCap.Round), geometry);
    }

    private IBrush? ResolveBrush(string key)
    {
        return TryGetResource(key, ActualThemeVariant, out var resource) ? resource as IBrush : null;
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}