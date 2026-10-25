using System.ComponentModel;
using System.Runtime.CompilerServices;
using FindRomCover.Services;

namespace FindRomCover.Models;

/// <summary>
///     A cover image candidate shown in the UI, including its similarity score and display metadata.
/// </summary>
public class ImageData : INotifyPropertyChanged, IDisposable
{
    private string _aiBadge = string.Empty;
    private bool _disposed;
    private int _imageHeight;
    private int _imageWidth;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageData" /> class.
    /// </summary>
    public ImageData()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageData" /> class with a path, display name,
    ///     and similarity score.
    /// </summary>
    /// <param name="imagePath">Path or URL of the image.</param>
    /// <param name="imageName">Display name of the image.</param>
    /// <param name="similarityScore">Similarity score (0-100) to the searched ROM name.</param>
    public ImageData(string? imagePath, string? imageName, double similarityScore)
    {
        ImagePath = imagePath;
        ImageName = string.IsNullOrWhiteSpace(imageName) ? "Unknown Filename" : imageName;
        SimilarityScore = similarityScore;
    }

    /// <summary>Path or URL of the image, or <see langword="null" /> when unknown.</summary>
    public string? ImagePath { get; init; }

    /// <summary>URL of a thumbnail image, when one is available.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Display name of the image; defaults to "Unknown Filename".</summary>
    public string? ImageName { get; set; } = "Unknown Filename";

    /// <summary>Human-readable file size, or "Unknown File Size" when unavailable.</summary>
    public string ImageFileSize { get; set; } = "Unknown File Size";

    /// <summary>Encoding format of the image, or "Unknown Encoding Format" when unavailable.</summary>
    public string ImageEncodingFormat { get; set; } = "Unknown Encoding Format";

    /// <summary>Similarity score (0-100) between the image and the searched ROM name.</summary>
    public double SimilarityScore { get; init; }

    private object? _imageSource;

    /// <summary>
    ///     The UI-specific image object (WPF BitmapImage or Avalonia Bitmap).
    ///     Created through <see cref="ImageSources" />.
    /// </summary>
    public object? ImageSource
    {
        get => _imageSource;
        set
        {
            if (_disposed)
            {
                (value as IDisposable)?.Dispose();
                return;
            }

            if (ReferenceEquals(_imageSource, value)) return;

            var previous = _imageSource;
            _imageSource = value;
            (previous as IDisposable)?.Dispose();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayImage));
        }
    }

    /// <summary>
    ///     Image to display, falling back to <see cref="ImageSources.BrokenImage" /> when
    ///     <see cref="ImageSource" /> is <see langword="null" />.
    /// </summary>
    public object? DisplayImage => ImageSource ?? ImageSources.BrokenImage;

    /// <summary>Short badge describing an AI pick; empty when the image was not picked.</summary>
    public string AiBadge
    {
        get => _aiBadge;
        set
        {
            if (string.Equals(_aiBadge, value, StringComparison.Ordinal)) return;

            _aiBadge = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Image width in pixels; 0 only until a positive value is assigned.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the assigned value is not positive.</exception>
    public int ImageWidth
    {
        get => _imageWidth;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Image width must be positive.");

            if (_imageWidth == value) return;

            _imageWidth = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Image height in pixels; 0 only until a positive value is assigned.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the assigned value is not positive.</exception>
    public int ImageHeight
    {
        get => _imageHeight;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Image height must be positive.");

            if (_imageHeight == value) return;

            _imageHeight = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Width of the thumbnail in pixels.</summary>
    public int ThumbnailWidth
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Height of the thumbnail in pixels.</summary>
    public int ThumbnailHeight
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Occurs when a property value changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Releases the UI bitmap held by <see cref="ImageSource" />. The item must not be
    ///     displayed afterwards.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        var source = _imageSource;
        _imageSource = null;
        (source as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Raises the <see cref="PropertyChanged" /> event for the specified property.
    /// </summary>
    /// <param name="propertyName">Name of the changed property; inferred from the caller when omitted.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}