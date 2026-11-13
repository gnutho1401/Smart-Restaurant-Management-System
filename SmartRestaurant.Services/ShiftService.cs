using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service managing cashier shifts, float balancing, and end-of-shift Z-Report financial reconciliation.
/// </summary>
public class ShiftService : IShiftService
{
    private readonly RestaurantDbContext _context;

    public ShiftService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<WorkShift> OpenShiftAsync(int userId, decimal openingFloat)
    {
        if (openingFloat < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(openingFloat), "Số tiền đầu ca không thể là số âm.");
        }

        // Check if there is already an open shift for this user
        var existingShift = await _context.WorkShifts
            .FirstOrDefaultAsync(w => w.UserId == userId && !w.IsClosed);

        if (existingShift != null)
        {
            throw new InvalidOperationException($"Nhân viên ID {userId} hiện đang có một ca làm việc chưa chốt (Ca #{existingShift.ShiftId}). Vui lòng chốt ca trước khi mở ca mới.");
        }

        var shift = new WorkShift
        {
            UserId = userId,
            StartTime = DateTime.UtcNow,
            OpeningFloat = openingFloat,
            CashSales = 0,
            CardSales = 0,
            QrSales = 0,
            TotalSales = 0,
            IsClosed = false
        };

        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();
        return shift;
    }

    public async Task<WorkShift?> GetCurrentOpenShiftAsync(int userId)
    {
        return await _context.WorkShifts
            .FirstOrDefaultAsync(w => w.UserId == userId && !w.IsClosed);
    }

    public async Task<WorkShift> CloseShiftAsync(int shiftId, decimal actualCashEnd, string? note = null)
    {
        var shift = await _context.WorkShifts
            .FirstOrDefaultAsync(w => w.ShiftId == shiftId);

        if (shift == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy ca làm việc ID {shiftId}.");
        }

        if (shift.IsClosed)
        {
            return shift;
        }

        var now = DateTime.UtcNow;

        // Calculate settled payments during the shift timeframe
        var payments = await _context.Payments
            .Where(p => p.PaidAt >= shift.StartTime && p.PaidAt <= now)
            .ToListAsync();

        shift.CashSales = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.AmountPaid);
        shift.CardSales = payments.Where(p => p.Method == PaymentMethod.CreditCard).Sum(p => p.AmountPaid);
        shift.QrSales = payments.Where(p => p.Method == PaymentMethod.BankTransferQR).Sum(p => p.AmountPaid);
        shift.TotalSales = shift.CashSales + shift.CardSales + shift.QrSales;

        shift.ActualCashEnd = actualCashEnd;
        // Cash Variance: Actual Cash in Drawer - (Opening Float + Cash Sales)
        shift.CashVariance = actualCashEnd - (shift.OpeningFloat + shift.CashSales);

        shift.EndTime = now;
        shift.IsClosed = true;
        shift.Note = note;

        await _context.SaveChangesAsync();
        return shift;
    }

    public async Task<IEnumerable<WorkShift>> GetShiftHistoryAsync()
    {
        return await _context.WorkShifts
            .Include(w => w.User)
            .OrderByDescending(w => w.StartTime)
            .ToListAsync();
    }
}
