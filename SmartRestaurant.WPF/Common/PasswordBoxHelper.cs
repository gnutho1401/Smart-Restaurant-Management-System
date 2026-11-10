using System.Windows;
using System.Windows.Controls;

namespace SmartRestaurant.WPF.Common;

/// <summary>
/// Attached property enabling two-way data-binding with WPF PasswordBox within MVVM architecture.
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

    public static readonly DependencyProperty BindPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BindPassword",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false, OnBindPasswordChanged));

    private static readonly DependencyProperty UpdatingPasswordProperty =
        DependencyProperty.RegisterAttached(
            "UpdatingPassword",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject dp) =>
        (string)dp.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject dp, string value) =>
        dp.SetValue(BoundPasswordProperty, value);

    public static bool GetBindPassword(DependencyObject dp) =>
        (bool)dp.GetValue(BindPasswordProperty);

    public static void SetBindPassword(DependencyObject dp, bool value) =>
        dp.SetValue(BindPasswordProperty, value);

    private static bool GetUpdatingPassword(DependencyObject dp) =>
        (bool)dp.GetValue(UpdatingPasswordProperty);

    private static void SetUpdatingPassword(DependencyObject dp, bool value) =>
        dp.SetValue(UpdatingPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox box)
        {
            if ((bool)box.GetValue(BindPasswordProperty))
            {
                box.PasswordChanged -= HandlePasswordChanged;
                if (!GetUpdatingPassword(box))
                {
                    box.Password = (string)e.NewValue;
                }
                box.PasswordChanged += HandlePasswordChanged;
            }
        }
    }

    private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
    {
        if (dp is PasswordBox box)
        {
            var wasBound = (bool)e.OldValue;
            var needToBind = (bool)e.NewValue;

            if (wasBound)
            {
                box.PasswordChanged -= HandlePasswordChanged;
            }

            if (needToBind)
            {
                box.PasswordChanged += HandlePasswordChanged;
                if (!string.IsNullOrEmpty(GetBoundPassword(box)))
                {
                    box.Password = GetBoundPassword(box);
                }
            }
        }
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            SetUpdatingPassword(box, true);
            SetBoundPassword(box, box.Password);
            SetUpdatingPassword(box, false);
        }
    }
}

