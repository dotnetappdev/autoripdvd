using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using AutoRipDVD.Models;

namespace AutoRipDVD.Views;

public sealed partial class DiscInfoDialog : ContentDialog
{
    public DiscInfo DiscInfo { get; }

    public DiscInfoDialog(DiscInfo discInfo)
    {
        DiscInfo = discInfo;
        InitializeComponent();
    }
}
