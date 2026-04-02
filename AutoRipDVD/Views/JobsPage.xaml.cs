using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AutoRipDVD.ViewModels;

namespace AutoRipDVD.Views;

public sealed partial class JobsPage : Page
{
    public JobsViewModel ViewModel { get; }

    public JobsPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<JobsViewModel>();
        InitializeComponent();
    }
}
