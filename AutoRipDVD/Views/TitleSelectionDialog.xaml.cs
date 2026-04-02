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

    private string GetMetadataMessage(MediaMetadata? metadata)
    {
        if (metadata == null) return string.Empty;
        
        return metadata.MediaType == MediaType.TVShow
            ? $"{metadata.Title} - Season {metadata.Season}"
            : $"{metadata.Title} ({metadata.Year})";
    }
}

// Converter for Count > 0 to bool
public class CountToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count)
            return count > 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

// Converter for null to bool
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

// Converter for inverse bool
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}
