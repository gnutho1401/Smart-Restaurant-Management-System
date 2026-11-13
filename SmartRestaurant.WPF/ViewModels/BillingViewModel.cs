using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel managing financial settlement conforming to statutory 8% VAT and multi-tender receipt generation.
/// </summary>
public class BillingViewModel : ViewModelBase
{
    private readonly IBillingService _billingService;
    private readonly IOrderService _orderService;
    private readonly IAuthService? _authService;

    private int _orderId;
    private string _tableName = string.Empty;
    private ObservableCollection<OrderItem> _orderItems = new();
    private decimal _subTotal;
    private decimal _discountAmount;
    private decimal _taxableAmount;
    private decimal _vatAmount;
    private decimal _totalDue;
    private PaymentMethod _selectedMethod = PaymentMethod.Cash;
    private decimal _amountPaid;
    private decimal _changeDue;
    private string _invoiceCode = string.Empty;
    private bool _isSettled;
    private string _statusMessage = string.Empty;

    public int OrderId
    {
        get => _orderId;
        set => SetProperty(ref _orderId, value);
    }

    public string TableName
    {
        get => _tableName;
        set => SetProperty(ref _tableName, value);
    }

    public ObservableCollection<OrderItem> OrderItems
    {
        get => _orderItems;
        set => SetProperty(ref _orderItems, value);
    }

    public decimal SubTotal
    {
        get => _subTotal;
        set => SetProperty(ref _subTotal, value);
    }

    public decimal DiscountAmount
    {
        get => _discountAmount;
        set
        {
            // Subordinate governance: Cashier cannot grant arbitrary discounts without Manager approval
            if (_authService?.CurrentUser?.Role == UserRole.Cashier && SubTotal > 0)
            {
                decimal maxAllowed = Math.Min(SubTotal * 0.10m, 100000m);
                if (value > maxAllowed)
                {
                    StatusMessage = $"⚠️ Thu ngân chỉ được áp dụng chiết khấu tối đa 10% (tối đa {maxAllowed:N0} đ). Mức chiết khấu cao hơn phải do Quản Lý (Store Manager) phê duyệt.";
                    value = maxAllowed;
                }
            }

            if (SetProperty(ref _discountAmount, value))
            {
                RecalculateTaxAndTotal();
            }
        }
    }

    public decimal TaxableAmount
    {
        get => _taxableAmount;
        set => SetProperty(ref _taxableAmount, value);
    }

    public decimal VatAmount
    {
        get => _vatAmount;
        set => SetProperty(ref _vatAmount, value);
    }

    public decimal TotalDue
    {
        get => _totalDue;
        set => SetProperty(ref _totalDue, value);
    }

    public PaymentMethod SelectedMethod
    {
        get => _selectedMethod;
        set
        {
            if (SetProperty(ref _selectedMethod, value))
            {
                if (value == PaymentMethod.CreditCard || value == PaymentMethod.BankTransferQR)
                {
                    AmountPaid = TotalDue;
                    ChangeDue = 0;
                }
            }
        }
    }

    public decimal AmountPaid
    {
        get => _amountPaid;
        set
        {
            if (SetProperty(ref _amountPaid, value))
            {
                ChangeDue = Math.Max(0, value - TotalDue);
            }
        }
    }

    public decimal ChangeDue
    {
        get => _changeDue;
        set => SetProperty(ref _changeDue, value);
    }

    public string InvoiceCode
    {
        get => _invoiceCode;
        set => SetProperty(ref _invoiceCode, value);
    }

    public bool IsSettled
    {
        get => _isSettled;
        set => SetProperty(ref _isSettled, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentFormattedDate => DateTime.Now.ToString("dd/MM/yyyy HH:mm");

    public IAsyncRelayCommand LoadOrderCommand { get; }
    public IAsyncRelayCommand SettlePaymentCommand { get; }

    public BillingViewModel(
        IBillingService billingService,
        IOrderService orderService,
        IAuthService? authService = null)
    {
        _billingService = billingService;
        _orderService = orderService;
        _authService = authService;

        LoadOrderCommand = new AsyncRelayCommand(LoadOrderAsync);
        SettlePaymentCommand = new AsyncRelayCommand(SettlePaymentAsync);
    }

    public async Task InitializeWithOrderAsync(int orderId)
    {
        OrderId = orderId;
        await LoadOrderAsync();
    }

    private async Task LoadOrderAsync()
    {
        var order = await _orderService.GetOrderByIdAsync(OrderId);
        if (order == null) return;

        TableName = order.Table?.TableName ?? $"Bàn #{order.TableId}";
        OrderItems = new ObservableCollection<OrderItem>(order.OrderItems);

        SubTotal = OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity);
        DiscountAmount = order.DiscountAmount;

        RecalculateTaxAndTotal();
        AmountPaid = TotalDue; // Default exact payment
    }

    private void RecalculateTaxAndTotal()
    {
        TaxableAmount = Math.Max(0, SubTotal - DiscountAmount);
        VatAmount = Math.Round(TaxableAmount * 0.0800m, 2, MidpointRounding.AwayFromZero);
        TotalDue = TaxableAmount + VatAmount;
        ChangeDue = Math.Max(0, AmountPaid - TotalDue);
    }

    private async Task SettlePaymentAsync()
    {
        if (AmountPaid < TotalDue)
        {
            StatusMessage = "❌ Số tiền khách trả không đủ quyết toán hóa đơn.";
            return;
        }

        try
        {
            // 1. Generate statutory VAT invoice
            var invoice = await _billingService.GenerateInvoiceAsync(OrderId, DiscountAmount);
            InvoiceCode = invoice.InvoiceCode;

            // 2. Settle multi-tender payment
            await _billingService.SettlePaymentAsync(invoice.InvoiceId, SelectedMethod, AmountPaid);

            IsSettled = true;
            StatusMessage = $"✅ Đã quyết toán thành công hóa đơn {invoice.InvoiceCode}!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi thanh toán: {ex.Message}";
        }
    }
}
