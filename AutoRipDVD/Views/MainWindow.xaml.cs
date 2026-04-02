using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AutoRipDVD.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "AutoRip DVD - Automatic Disc Ripper";
        
        // Navigate to dashboard by default
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.InvokedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "Dashboard":
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
                case "Jobs":
                    ContentFrame.Navigate(typeof(JobsPage));
                    break;
                case "Logs":
                    ContentFrame.Navigate(typeof(LogsPage));
                    break;
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "Dashboard":
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
                case "Jobs":
                    ContentFrame.Navigate(typeof(JobsPage));
                    break;
                case "Logs":
                    ContentFrame.Navigate(typeof(LogsPage));
                    break;
            }
        }
    }
}
