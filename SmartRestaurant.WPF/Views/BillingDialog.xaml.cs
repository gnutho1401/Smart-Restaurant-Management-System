using System;
using System.Windows;
using System.Windows.Controls;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for BillingDialog.xaml with native WPF PrintDialog receipt output.
/// </summary>
public partial class BillingDialog : Window
{
    public BillingDialog()
    {
        InitializeComponent();
    }

    private void PrintPreCheck_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BillingViewModel vm)
        {
            vm.SwitchToPreCheckCommand.Execute(null);
        }
        DoPrint("SmartRestaurant Pre-Check Bill");
    }

    private void PrintReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BillingViewModel vm)
        {
            vm.SwitchToInvoiceCommand.Execute(null);
        }
        DoPrint("SmartRestaurant Invoice Receipt");
    }

    private void DoPrint(string jobTitle)
    {
        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(ReceiptPrintArea, jobTitle);
                MessageBox.Show("Đã gửi lệnh in tài liệu thành công!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể in: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}