using System.ComponentModel;
using System.Runtime.CompilerServices;
using FindRomCover.Services;

namespace FindRomCover.Models;

public class ImageData : INotifyPropertyChanged
{
    private string _aiBadge = string.Empty;

    private int _imageHeight;

    private int _imageWidth;

    public ImageData()
    {
    }

    public ImageData(string? imagePath, string? imageName, double similarityScore)
    {
        ImagePath = imagePath;
        ImageName = imageName;
        SimilarityScore = similarityScore;
    }

    public string? ImagePath { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? ImageName { get; set; } = "Unknown Filename";
    public string ImageFileSize { get; set; } = "Unknown File Size";
    public string ImageEncodingFormat { get; set; } = "Unknown Encoding Format";

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
            if (ReferenceEquals(_imageSource, value)) return;

            _imageSource = value;
            OnPropertyChanged(nameof(ImageSource));
            OnPropertyChanged(nameof(DisplayImage));
        }
    }

    public object? DisplayImage => ImageSource ?? ImageSources.BrokenImage;

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

    public int ImageWidth
    {
        get => _imageWidth;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Image width must be positive.");

            _imageWidth = value;
        }
    }

    public int ImageHeight
    {
        get => _imageHeight;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Image height must be positive.");

            _imageHeight = value;
        }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}