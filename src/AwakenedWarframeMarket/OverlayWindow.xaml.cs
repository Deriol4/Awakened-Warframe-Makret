using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AwakenedWarframeMarket.Models;

namespace AwakenedWarframeMarket;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OverlayWindow_Loaded;
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Fade-in animation
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200)
        };
        BeginAnimation(OpacityProperty, fadeIn);

        // Center on screen
        CenterOnScreen();
    }

    private void CenterOnScreen()
    {
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2;
        Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2;
    }

    public void ShowLoadingState(string message)
    {
        LoadingPanel.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
        PriceGrid.Visibility = Visibility.Collapsed;

        LoadingText.Text = message;
        ItemNameText.Text = "Loading...";
        SubtitleText.Text = "";
    }

    public void UpdateStatus(string message)
    {
        LoadingText.Text = message;
    }

    public void ShowError(string message)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        PriceGrid.Visibility = Visibility.Collapsed;

        ErrorText.Text = message;
        ItemNameText.Text = "Error";
        SubtitleText.Text = "";
    }

    public void SetItemData(string itemName, List<PriceData> prices)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        PriceGrid.Visibility = Visibility.Visible;

        ItemNameText.Text = itemName;
        SubtitleText.Text = prices.Count > 0
            ? $"Found {prices.Count} listings"
            : "No listings found";

        PriceGrid.ItemsSource = prices;

        // Animate grid appearance
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(150)
        };
        PriceGrid.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseWithAnimation();
        }
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private void CloseWithAnimation()
    {
        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(150)
        };
        fadeOut.Completed += (s, e) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }
}
