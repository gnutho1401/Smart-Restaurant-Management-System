using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Exceptions;
using SmartRestaurant.Core.Interfaces;

namespace SmartRestaurant.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly RestaurantDbContext _context;

    public IRepository<DiningTable> Tables { get; }
    public IRepository<Order> Orders { get; }
    public IRepository<OrderItem> OrderItems { get; }
    public IRepository<MenuItem> MenuItems { get; }
    public IRepository<Ingredient> Ingredients { get; }
    public IRepository<Recipe> Recipes { get; }
    public IRepository<Invoice> Invoices { get; }
    public IRepository<Payment> Payments { get; }

    public UnitOfWork(RestaurantDbContext context)
    {
        _context = context;
        Tables = new EfRepository<DiningTable>(_context);
        Orders = new EfRepository<Order>(_context);
        OrderItems = new EfRepository<OrderItem>(_context);
        MenuItems = new EfRepository<MenuItem>(_context);
        Ingredients = new EfRepository<Ingredient>(_context);
        Recipes = new EfRepository<Recipe>(_context);
        Invoices = new EfRepository<Invoice>(_context);
        Payments = new EfRepository<Payment>(_context);
    }

    public async Task<int> CommitAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Kho nguyên liệu hoặc dữ liệu vừa được cập nhật bởi một quầy khác. Giao dịch đã được hủy an toàn, vui lòng thử lại.", ex);
        }
    }

    public async Task<ITransactionScope> BeginTransactionAsync()
    {
        var dbTransaction = await _context.Database.BeginTransactionAsync();
        return new EfTransactionScope(dbTransaction);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private sealed class EfTransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _transaction;

        public EfTransactionScope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync()
        {
            await _transaction.CommitAsync();
        }

        public async Task RollbackAsync()
        {
            await _transaction.RollbackAsync();
        }

        public void Dispose()
        {
            _transaction.Dispose();
        }
    }
}
