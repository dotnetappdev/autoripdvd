using Microsoft.UI.Xaml.Controls;
using AutoRipDVD.ViewModels;
using Windows.Media.Core;

namespace AutoRipDVD.Views;

public sealed partial class PreviewWindow : Page
{
    public PreviewWindow()
    {
        this.InitializeComponent();
    }

    public void SetSource(string mediaPath, IEnumerable<string> thumbnails, string title)
    {
        TitleText.Text = title;
        PreviewPlayer.Source = MediaSource.CreateFromUri(new Uri(mediaPath));
        ThumbnailList.ItemsSource = thumbnails;
    }
}
