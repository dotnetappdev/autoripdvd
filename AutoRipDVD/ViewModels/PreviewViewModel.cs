using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AutoRipDVD.ViewModels;

public partial class PreviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _mediaPath = string.Empty;

    public ObservableCollection<string> Thumbnails { get; } = new();

    public void Load(string mediaPath, IEnumerable<string> thumbs, string title)
    {
        MediaPath = mediaPath;
        Title = title;
        Thumbnails.Clear();
        foreach (var t in thumbs) Thumbnails.Add(t);
    }
}
