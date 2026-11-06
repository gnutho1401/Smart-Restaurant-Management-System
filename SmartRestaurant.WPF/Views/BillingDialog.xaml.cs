using System;
using System.Windows;
using System.Windows.Controls;

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

    private void PrintReceipt_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(ReceiptPrintArea, "SmartRestaurant Invoice Receipt");
                MessageBox.Show("Đã gửi lệnh in hóa đơn thành công!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể in hóa đơn: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
