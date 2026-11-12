using System;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;

namespace SmartRestaurant.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<DiningTable> Tables { get; }
    IRepository<Order> Orders { get; }
    IRepository<OrderItem> OrderItems { get; }
    IRepository<MenuItem> MenuItems { get; }
    IRepository<Ingredient> Ingredients { get; }
    IRepository<Recipe> Recipes { get; }
    IRepository<Invoice> Invoices { get; }
    IRepository<Payment> Payments { get; }

    Task<int> CommitAsync();
    Task<ITransactionScope> BeginTransactionAsync();
}

public interface ITransactionScope : IDisposable
{
    Task CommitAsync();
    Task RollbackAsync();
}
