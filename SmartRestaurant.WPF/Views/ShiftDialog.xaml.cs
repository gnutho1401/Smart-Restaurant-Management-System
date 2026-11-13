using System;
using System.Windows;
using System.Windows.Controls;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for ShiftDialog.xaml handling shift handover and Z-Report print output.
/// </summary>
public partial class ShiftDialog : Window
{
    public ShiftDialog()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is ShiftViewModel vm)
            {
                await vm.LoadShiftAsync();
            }
        };
    }

    public ShiftDialog(ShiftViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void PrintZReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(ZReportPrintArea, "SmartRestaurant Z-Report");
                MessageBox.Show("Đã gửi lệnh in Báo Cáo Kết Ca Z-Report thành công!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể in Z-Report: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
