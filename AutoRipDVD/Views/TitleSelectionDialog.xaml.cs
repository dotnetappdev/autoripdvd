using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using AutoRipDVD.ViewModels;
using AutoRipDVD.Models;

namespace AutoRipDVD.Views;

public sealed partial class TitleSelectionDialog : ContentDialog
{
    public TitleSelectionViewModel ViewModel { get; }

    public TitleSelectionDialog(TitleSelectionViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    // ── Tree selection → right-panel info update ──────────────────────────────

    private void DiscTreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        ViewModel.SelectedNode = DiscTreeView.SelectedItem as DiscTreeNode;
    }

    // ── x:Bind function helpers ───────────────────────────────────────────────

    private string GetMetadataMessage(MediaMetadata? metadata)
    {
        if (metadata == null) return string.Empty;
        return metadata.MediaType == MediaType.TVShow
            ? $"{metadata.Title} – Season {metadata.Season}"
            : $"{metadata.Title} ({metadata.Year})";
    }
}

// ── Value converters ──────────────────────────────────────────────────────────

/// <summary>Converts int > 0 to true (used to enable the "Start Ripping" button).</summary>
public class CountToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int count && count > 0;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>Converts non-null to true (used for InfoBar.IsOpen).</summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value != null;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>Inverts a bool (used for IsEnabled on controls that should be disabled while scanning).</summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : false;
}
