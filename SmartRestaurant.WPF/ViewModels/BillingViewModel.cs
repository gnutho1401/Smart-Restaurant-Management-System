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
    private readonly ICustomerService _customerService;
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

    // Membership & Loyalty Points properties
    private string _phoneNumberSearch = string.Empty;
    private Customer? _foundCustomer;
    private string _newCustomerName = string.Empty;
    private int _pointsToRedeem;
    private bool _isCustomerFound;
    private string _billTitle = "PHIẾU THANH TOÁN (VAT 8%)";
    private bool _isPreCheckMode;

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
        set
        {
            if (SetProperty(ref _subTotal, value))
            {
                RecalculateTaxAndTotal();
            }
        }
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
                    StatusMessage = $"Thu ngân chỉ được áp dụng chiết khấu tối đa 10% (tối đa {maxAllowed:N0} đ). Mức chiết khấu cao hơn phải do Quản Lý (Store Manager) phê duyệt.";
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

    public string PhoneNumberSearch
    {
        get => _phoneNumberSearch;
        set => SetProperty(ref _phoneNumberSearch, value);
    }

    public Customer? FoundCustomer
    {
        get => _foundCustomer;
        set
        {
            if (SetProperty(ref _foundCustomer, value))
            {
                OnPropertyChanged(nameof(HasFoundCustomer));
                OnPropertyChanged(nameof(CustomerSummaryText));
            }
        }
    }

    public bool HasFoundCustomer => FoundCustomer != null;

    public string CustomerSummaryText => FoundCustomer != null
        ? $"{FoundCustomer.FullName} | Hạng: {FoundCustomer.Tier} | Điểm khả dụng: {FoundCustomer.LoyaltyPoints:N0} pts"
        : "Chưa áp dụng hội viên";

    public string NewCustomerName
    {
        get => _newCustomerName;
        set => SetProperty(ref _newCustomerName, value);
    }

    public int PointsToRedeem
    {
        get => _pointsToRedeem;
        set => SetProperty(ref _pointsToRedeem, value);
    }

    public bool IsCustomerFound
    {
        get => _isCustomerFound;
        set => SetProperty(ref _isCustomerFound, value);
    }

    public string BillTitle
    {
        get => _billTitle;
        set => SetProperty(ref _billTitle, value);
    }

    public bool IsPreCheckMode
    {
        get => _isPreCheckMode;
        set => SetProperty(ref _isPreCheckMode, value);
    }

    public string CurrentFormattedDate => DateTime.Now.ToString("dd/MM/yyyy HH:mm");

    public IAsyncRelayCommand LoadOrderCommand { get; }
    public IAsyncRelayCommand SettlePaymentCommand { get; }
    public IAsyncRelayCommand SearchCustomerCommand { get; }
    public IAsyncRelayCommand RegisterCustomerCommand { get; }
    public IAsyncRelayCommand ApplyPointsCommand { get; }
    public RelayCommand SwitchToPreCheckCommand { get; }
    public RelayCommand SwitchToInvoiceCommand { get; }

    // Touch Ergonomics & On-Screen Numpad Commands
    public RelayCommand<string> NumpadDigitCommand { get; }
    public RelayCommand NumpadClearCommand { get; }
    public RelayCommand NumpadBackspaceCommand { get; }
    public RelayCommand NumpadTripleZeroCommand { get; }
    public RelayCommand<string> QuickCashCommand { get; }
    public RelayCommand ExactAmountCommand { get; }

    public BillingViewModel(
        IBillingService billingService,
        IOrderService orderService,
        ICustomerService customerService,
        IAuthService? authService = null)
    {
        _billingService = billingService;
        _orderService = orderService;
        _customerService = customerService;
        _authService = authService;

        LoadOrderCommand = new AsyncRelayCommand(LoadOrderAsync);
        SettlePaymentCommand = new AsyncRelayCommand(SettlePaymentAsync);
        SearchCustomerCommand = new AsyncRelayCommand(SearchCustomerAsync);
        RegisterCustomerCommand = new AsyncRelayCommand(RegisterCustomerAsync);
        ApplyPointsCommand = new AsyncRelayCommand(ApplyPointsAsync);
        SwitchToPreCheckCommand = new RelayCommand(ExecuteSwitchToPreCheck);
        SwitchToInvoiceCommand = new RelayCommand(ExecuteSwitchToInvoice);

        // Initialize Touch Numpad & Quick Cash
        NumpadDigitCommand = new RelayCommand<string>(ExecuteNumpadDigit);
        NumpadClearCommand = new RelayCommand(ExecuteNumpadClear);
        NumpadBackspaceCommand = new RelayCommand(ExecuteNumpadBackspace);
        NumpadTripleZeroCommand = new RelayCommand(ExecuteNumpadTripleZero);
        QuickCashCommand = new RelayCommand<string>(ExecuteQuickCash);
        ExactAmountCommand = new RelayCommand(ExecuteExactAmount);
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

        SubTotal = OrderItems.Where(oi => !oi.IsCancelled).Sum(oi => oi.UnitPrice * oi.Quantity);
        DiscountAmount = order.DiscountAmount;

        if (order.CustomerId.HasValue)
        {
            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId.Value);
            if (customer != null)
            {
                FoundCustomer = customer;
                PhoneNumberSearch = customer.PhoneNumber;
                IsCustomerFound = true;
            }
        }

        RecalculateTaxAndTotal();
        AmountPaid = TotalDue; // Default exact payment
    }

    private async Task SearchCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumberSearch))
        {
            StatusMessage = "Vui lòng nhập số điện thoại để tra cứu hội viên.";
            return;
        }

        var customer = await _customerService.GetCustomerByPhoneAsync(PhoneNumberSearch.Trim());
        if (customer != null)
        {
            FoundCustomer = customer;
            IsCustomerFound = true;

            // Auto-attach to order and compute tier discount
            await _orderService.AttachCustomerToOrderAsync(OrderId, customer.CustomerId);
            await _orderService.ApplyMemberDiscountAsync(OrderId, customer.CustomerId, 0);

            var updatedOrder = await _orderService.GetOrderByIdAsync(OrderId);
            if (updatedOrder != null)
            {
                DiscountAmount = updatedOrder.DiscountAmount;
            }

            StatusMessage = $"Đã nhận diện hội viên {customer.FullName} ({customer.Tier})! Điểm tích lũy: {customer.LoyaltyPoints:N0} pts.";
        }
        else
        {
            FoundCustomer = null;
            IsCustomerFound = false;
            StatusMessage = "Số điện thoại chưa đăng ký hội viên. Vui lòng nhập họ tên để đăng ký mới ngay bên dưới.";
        }
    }

    private async Task RegisterCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumberSearch) || string.IsNullOrWhiteSpace(NewCustomerName))
        {
            StatusMessage = "Vui lòng nhập đầy đủ Số điện thoại và Họ tên hội viên để đăng ký.";
            return;
        }

        try
        {
            var newCustomer = await _customerService.RegisterCustomerAsync(PhoneNumberSearch.Trim(), NewCustomerName.Trim());
            FoundCustomer = newCustomer;
            IsCustomerFound = true;
            NewCustomerName = string.Empty;

            await _orderService.AttachCustomerToOrderAsync(OrderId, newCustomer.CustomerId);

            StatusMessage = $"Đăng ký thành công hội viên VIP: {newCustomer.FullName} ({newCustomer.PhoneNumber})!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể đăng ký: {ex.Message}";
        }
    }

    private async Task ApplyPointsAsync()
    {
        if (FoundCustomer == null)
        {
            StatusMessage = "Vui lòng tra cứu hoặc đăng ký thông tin hội viên trước khi đổi điểm.";
            return;
        }

        if (PointsToRedeem <= 0)
        {
            StatusMessage = "Số điểm muốn đổi phải lớn hơn 0.";
            return;
        }

        if (PointsToRedeem > FoundCustomer.LoyaltyPoints)
        {
            StatusMessage = $"Hội viên chỉ có {FoundCustomer.LoyaltyPoints:N0} điểm khả dụng. Không thể đổi {PointsToRedeem:N0} điểm.";
            return;
        }

        await _orderService.ApplyMemberDiscountAsync(OrderId, FoundCustomer.CustomerId, PointsToRedeem);
        var updatedOrder = await _orderService.GetOrderByIdAsync(OrderId);
        if (updatedOrder != null)
        {
            DiscountAmount = updatedOrder.DiscountAmount;
        }

        StatusMessage = $"Đã quy đổi thành công {PointsToRedeem:N0} điểm (giảm {PointsToRedeem * 1000m:N0} đ)!";
    }

    private void ExecuteSwitchToPreCheck()
    {
        IsPreCheckMode = true;
        BillTitle = "PHIẾU TẠM TÍNH (PRE-CHECK BILL - CHƯA THANH TOÁN)";
        StatusMessage = "Đang xem bản xem trước Phiếu Tạm Tính. Thu ngân có thể bấm 'In Phiếu Tạm Tính' để gửi khách kiểm tra.";
    }

    private void ExecuteSwitchToInvoice()
    {
        IsPreCheckMode = false;
        BillTitle = "PHIẾU THANH TOÁN (VAT 8%)";
        StatusMessage = "Đang ở chế độ Quyết toán Hóa đơn chính thức.";
    }

    private void ExecuteNumpadDigit(string? digit)
    {
        if (string.IsNullOrEmpty(digit) || !char.IsDigit(digit[0])) return;
        int d = digit[0] - '0';
        decimal newAmount = AmountPaid == 0 ? d : (AmountPaid * 10m) + d;
        if (newAmount <= 100000000m) // 100 million cap
        {
            AmountPaid = newAmount;
        }
    }

    private void ExecuteNumpadTripleZero()
    {
        decimal newAmount = AmountPaid * 1000m;
        if (newAmount <= 100000000m)
        {
            AmountPaid = newAmount;
        }
    }

    private void ExecuteNumpadBackspace()
    {
        AmountPaid = Math.Floor(AmountPaid / 10m);
    }

    private void ExecuteNumpadClear()
    {
        AmountPaid = 0;
    }

    private void ExecuteQuickCash(string? amountStr)
    {
        if (decimal.TryParse(amountStr, out var addAmount))
        {
            decimal newAmount = AmountPaid + addAmount;
            if (newAmount <= 100000000m)
            {
                AmountPaid = newAmount;
            }
        }
    }

    private void ExecuteExactAmount()
    {
        AmountPaid = TotalDue;
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
            StatusMessage = "Số tiền khách trả không đủ quyết toán hóa đơn.";
            return;
        }

        try
        {
            // 1. Generate statutory VAT invoice
            var invoice = await _billingService.GenerateInvoiceAsync(OrderId, DiscountAmount);
            InvoiceCode = invoice.InvoiceCode;

            // 2. Settle multi-tender payment with Idempotency Key protection
            string idempotencyKey = $"{invoice.InvoiceCode}_{SelectedMethod}_{AmountPaid:0}";
            await _billingService.SettlePaymentAsync(invoice.InvoiceId, SelectedMethod, AmountPaid, idempotencyKey);

            IsSettled = true;
            StatusMessage = $"Đã quyết toán thành công hóa đơn {invoice.InvoiceCode}!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi thanh toán: {ex.Message}";
        }
    }
}
