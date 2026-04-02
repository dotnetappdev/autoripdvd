using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AutoRipDVD.ViewModels;

namespace AutoRipDVD.Views;

public sealed partial class LogsPage : Page
{
    public LogsViewModel ViewModel { get; }

    public LogsPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<LogsViewModel>();
        InitializeComponent();
    }
}
