using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for TableTransferMergeDialog.xaml orchestrating table reassignments.
/// </summary>
public partial class TableTransferMergeDialog : Window
{
    private readonly DiningTable _sourceTable;
    private readonly List<DiningTable> _allTables;
    private readonly ITableService _tableService;

    public class TableOption
    {
        public int TableId { get; set; }
        public string DisplayInfo { get; set; } = string.Empty;
    }

    public TableTransferMergeDialog(DiningTable sourceTable, List<DiningTable> allTables, ITableService tableService, bool defaultToMerge = false)
    {
        InitializeComponent();
        _sourceTable = sourceTable;
        _allTables = allTables;
        _tableService = tableService;

        TxtSourceTable.Text = $"{sourceTable.TableName} (Khu: {sourceTable.Area?.AreaName ?? "Chung"})";

        if (defaultToMerge)
        {
            RbMerge.IsChecked = true;
        }

        UpdateTargetList();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (CmbTargetTables == null) return;
        UpdateTargetList();
    }

    private void UpdateTargetList()
    {
        bool isTransfer = RbTransfer.IsChecked == true;

        if (isTransfer)
        {
            LblTargetTable.Text = "Bàn trống muốn chuyển đến:";
            BtnConfirm.Content = "✓ Xác Nhận Chuyển Bàn";
            TxtNote.Text = "💡 Khi chuyển bàn, toàn bộ món ăn và hóa đơn hiện tại sẽ được chuyển tự động sang bàn đích. Bàn cũ sẽ trở về trạng thái Trống.";

            var options = _allTables
                .Where(t => t.TableId != _sourceTable.TableId && t.Status == TableStatus.Available)
                .Select(t => new TableOption
                {
                    TableId = t.TableId,
                    DisplayInfo = $"{t.TableName} - Sức chứa: {t.Capacity} Khách ({t.Area?.AreaName ?? "Chung"})"
                }).ToList();

            CmbTargetTables.ItemsSource = options;
            if (options.Any()) CmbTargetTables.SelectedIndex = 0;
        }
        else
        {
            LblTargetTable.Text = "Bàn đích muốn gộp vào:";
            BtnConfirm.Content = "✓ Xác Nhận Gộp Bàn";
            TxtNote.Text = "💡 Khi gộp bàn, các món ăn từ bàn hiện tại sẽ được gộp chung vào đơn của bàn đích. Bàn hiện tại sẽ trở về trạng thái Trống.";

            var options = _allTables
                .Where(t => t.TableId != _sourceTable.TableId && (t.Status == TableStatus.Occupied || t.Status == TableStatus.Available))
                .Select(t => new TableOption
                {
                    TableId = t.TableId,
                    DisplayInfo = $"{t.TableName} [{t.Status}] - {t.Area?.AreaName ?? "Chung"}"
                }).ToList();

            CmbTargetTables.ItemsSource = options;
            if (options.Any()) CmbTargetTables.SelectedIndex = 0;
        }
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (CmbTargetTables.SelectedValue is not int targetTableId)
        {
            MessageBox.Show("Vui lòng chọn một bàn đích từ danh sách!", "Thiếu Thông Tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool isTransfer = RbTransfer.IsChecked == true;

        try
        {
            BtnConfirm.IsEnabled = false;

            if (isTransfer)
            {
                bool success = await _tableService.TransferTableAsync(_sourceTable.TableId, targetTableId);
                if (success)
                {
                    MessageBox.Show(
                        $"Đã chuyển thành công từ bàn {_sourceTable.TableName} sang bàn đích!",
                        "Chuyển Bàn Thành Công",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Không thể thực hiện chuyển bàn. Vui lòng kiểm tra lại trạng thái bàn.", "Thao Tác Thất Bại", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                bool success = await _tableService.MergeTablesAsync(_sourceTable.TableId, targetTableId);
                if (success)
                {
                    MessageBox.Show(
                        $"Đã gộp thành công món ăn từ bàn {_sourceTable.TableName} vào bàn đích!",
                        "Gộp Bàn Thành Công",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Không thể thực hiện gộp bàn. Vui lòng kiểm tra lại đơn đặt của hai bàn.", "Thao Tác Thất Bại", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi trong quá trình xử lý: {ex.Message}", "Lỗi Nghiệp Vụ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnConfirm.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}