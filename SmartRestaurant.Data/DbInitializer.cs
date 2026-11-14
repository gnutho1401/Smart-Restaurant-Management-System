using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Data;

/// <summary>
/// Database seeder populating initial 3NF data catalog, dining areas, tables, BOM recipes, and users.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(RestaurantDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        // 0. Self-Healing Schema Migration for SQL Server (Prevents Missing Column/Table exceptions)
        if (context.Database.IsSqlServer())
        {
            await EnsureSchemaUpgradesAsync(context);
        }

        // Check if data already exists
        if (await context.DiningAreas.AnyAsync())
        {
            return; // DB already seeded
        }

        // 1. Seed Dining Areas
        var areaMain = new DiningArea { AreaName = "Sảnh Chính (Main Dining)", FloorNumber = 1 };
        var areaVip = new DiningArea { AreaName = "Phòng VIP Hoàng Gia", FloorNumber = 2 };
        var areaTerrace = new DiningArea { AreaName = "Khu Vực Sân Vườn (Terrace)", FloorNumber = 1 };

        context.DiningAreas.AddRange(areaMain, areaVip, areaTerrace);
        await context.SaveChangesAsync();

        // 2. Seed Dining Tables
        var tables = new List<DiningTable>
        {
            new() { AreaId = areaMain.AreaId, TableName = "T-01", Capacity = 4, Status = TableStatus.Available },
            new() { AreaId = areaMain.AreaId, TableName = "T-02", Capacity = 2, Status = TableStatus.Available },
            new() { AreaId = areaMain.AreaId, TableName = "T-03", Capacity = 6, Status = TableStatus.Occupied },
            new() { AreaId = areaMain.AreaId, TableName = "T-04", Capacity = 4, Status = TableStatus.Available },
            new() { AreaId = areaMain.AreaId, TableName = "T-05", Capacity = 8, Status = TableStatus.Available },
            new() { AreaId = areaVip.AreaId, TableName = "VIP-01", Capacity = 10, Status = TableStatus.Reserved },
            new() { AreaId = areaVip.AreaId, TableName = "VIP-02", Capacity = 6, Status = TableStatus.Cleaning },
            new() { AreaId = areaVip.AreaId, TableName = "VIP-03", Capacity = 4, Status = TableStatus.Available },
            new() { AreaId = areaTerrace.AreaId, TableName = "O-01", Capacity = 4, Status = TableStatus.Available },
            new() { AreaId = areaTerrace.AreaId, TableName = "O-02", Capacity = 2, Status = TableStatus.Available }
        };
        context.DiningTables.AddRange(tables);
        await context.SaveChangesAsync();

        // 3. Seed Menu Categories
        var catAppetizer = new MenuCategory { CategoryName = "Khai Vị (Appetizers)" };
        var catMain = new MenuCategory { CategoryName = "Món Chính (Main Courses)" };
        var catDessert = new MenuCategory { CategoryName = "Tráng Miệng (Desserts)" };
        var catBeverage = new MenuCategory { CategoryName = "Đồ Uống (Beverages)" };

        context.MenuCategories.AddRange(catAppetizer, catMain, catDessert, catBeverage);
        await context.SaveChangesAsync();

        // 4. Seed Raw Ingredients
        var ingBeef = new Ingredient { Name = "Thịt Bò Wagyu A5", Unit = "g", CurrentStock = 5000m, MinStock = 1000m };
        var ingTruffle = new Ingredient { Name = "Nấm Truffle Đen", Unit = "g", CurrentStock = 1000m, MinStock = 200m };
        var ingLobster = new Ingredient { Name = "Tôm Hùm Alaska", Unit = "g", CurrentStock = 8000m, MinStock = 1500m };
        var ingSalmon = new Ingredient { Name = "Cá Hồi Na Uy Tươi", Unit = "g", CurrentStock = 6000m, MinStock = 1200m };
        var ingCheese = new Ingredient { Name = "Sốt Phô Mai Ý", Unit = "ml", CurrentStock = 4000m, MinStock = 800m };
        var ingCream = new Ingredient { Name = "Kem Tươi Whipping Cream", Unit = "ml", CurrentStock = 5000m, MinStock = 1000m };
        var ingChocolate = new Ingredient { Name = "Sô-cô-la Đen Bỉ", Unit = "g", CurrentStock = 3000m, MinStock = 500m };
        var ingWine = new Ingredient { Name = "Rượu Vang Đỏ Cabernet", Unit = "ml", CurrentStock = 10000m, MinStock = 2000m };
        var ingTea = new Ingredient { Name = "Trà Ô Long Thượng Hạng", Unit = "g", CurrentStock = 2000m, MinStock = 300m };
        var ingPotato = new Ingredient { Name = "Khoai Tây Vàng Đà Lạt", Unit = "g", CurrentStock = 15000m, MinStock = 3000m };
        var ingOliveOil = new Ingredient { Name = "Dầu Ô-liu Nguyên Chất", Unit = "ml", CurrentStock = 5000m, MinStock = 1000m };

        context.Ingredients.AddRange(
            ingBeef, ingTruffle, ingLobster, ingSalmon, ingCheese, 
            ingCream, ingChocolate, ingWine, ingTea, ingPotato, ingOliveOil);
        await context.SaveChangesAsync();

        // 5. Seed Menu Items
        var dishSteak = new MenuItem
        {
            CategoryId = catMain.CategoryId,
            Name = "Bò Wagyu Áp Chảo Sốt Tiêu Đen",
            Price = 450000m,
            Description = "Thịt bò Wagyu A5 mềm mọng tan chảy trên đầu lưỡi, phục vụ kèm khoai tây nghiền và sốt tiêu đen đậm đà.",
            IsAvailable = true
        };

        var dishSoup = new MenuItem
        {
            CategoryId = catAppetizer.CategoryId,
            Name = "Súp Kem Nấm Truffle Hoàng Gia",
            Price = 185000m,
            Description = "Súp kem béo ngậy nấu từ nấm truffle đen thượng hạng và whipping cream thơm lừng.",
            IsAvailable = true
        };

        var dishLobster = new MenuItem
        {
            CategoryId = catMain.CategoryId,
            Name = "Tôm Hùm Nướng Phô Mai Đút Lò",
            Price = 680000m,
            Description = "Tôm hùm Alaska tươi sống phủ lớp sốt phô mai Ý nướng vàng óng thơm ngậy.",
            IsAvailable = true
        };

        var dishSalmon = new MenuItem
        {
            CategoryId = catMain.CategoryId,
            Name = "Cá Hồi Na Uy Áp Chảo Dầu Ô-liu",
            Price = 320000m,
            Description = "Phi lê cá hồi Na Uy da giòn rụm, thịt hồng ngọt tự nhiên áp chảo cùng dầu ô-liu nguyên chất.",
            IsAvailable = true
        };

        var dishMousse = new MenuItem
        {
            CategoryId = catDessert.CategoryId,
            Name = "Bánh Mousse Sô-cô-la Bỉ",
            Price = 95000m,
            Description = "Mousse socola mịn màng chế tác từ 70% sô-cô-la nguyên chất từ vương quốc Bỉ.",
            IsAvailable = true
        };

        var dishWine = new MenuItem
        {
            CategoryId = catBeverage.CategoryId,
            Name = "Rượu Vang Đỏ Cabernet Sauvignon",
            Price = 250000m,
            Description = "Ly vang đỏ thượng hạng niên vụ 2020 hương vị quả mọng và gỗ sồi tinh tế.",
            IsAvailable = true
        };

        var dishTea = new MenuItem
        {
            CategoryId = catBeverage.CategoryId,
            Name = "Trà Ô Long Sen Vàng",
            Price = 55000m,
            Description = "Trà ô long ướp hạt sen bùi béo thanh nhiệt, ngọt ngào dễ chịu.",
            IsAvailable = true
        };

        context.MenuItems.AddRange(dishSteak, dishSoup, dishLobster, dishSalmon, dishMousse, dishWine, dishTea);
        await context.SaveChangesAsync();

        // 6. Seed BOM Recipes (Bill-of-Materials Formulation)
        var recipes = new List<Recipe>
        {
            // Wagyu Steak BOM
            new() { MenuItemId = dishSteak.MenuItemId, IngredientId = ingBeef.IngredientId, QuantityRequired = 250m },
            new() { MenuItemId = dishSteak.MenuItemId, IngredientId = ingPotato.IngredientId, QuantityRequired = 100m },
            new() { MenuItemId = dishSteak.MenuItemId, IngredientId = ingOliveOil.IngredientId, QuantityRequired = 15m },

            // Truffle Soup BOM
            new() { MenuItemId = dishSoup.MenuItemId, IngredientId = ingTruffle.IngredientId, QuantityRequired = 50m },
            new() { MenuItemId = dishSoup.MenuItemId, IngredientId = ingCream.IngredientId, QuantityRequired = 100m },

            // Grilled Lobster BOM
            new() { MenuItemId = dishLobster.MenuItemId, IngredientId = ingLobster.IngredientId, QuantityRequired = 400m },
            new() { MenuItemId = dishLobster.MenuItemId, IngredientId = ingCheese.IngredientId, QuantityRequired = 80m },

            // Salmon BOM
            new() { MenuItemId = dishSalmon.MenuItemId, IngredientId = ingSalmon.IngredientId, QuantityRequired = 200m },
            new() { MenuItemId = dishSalmon.MenuItemId, IngredientId = ingOliveOil.IngredientId, QuantityRequired = 15m },

            // Chocolate Mousse BOM
            new() { MenuItemId = dishMousse.MenuItemId, IngredientId = ingChocolate.IngredientId, QuantityRequired = 80m },
            new() { MenuItemId = dishMousse.MenuItemId, IngredientId = ingCream.IngredientId, QuantityRequired = 50m },

            // Wine BOM
            new() { MenuItemId = dishWine.MenuItemId, IngredientId = ingWine.IngredientId, QuantityRequired = 150m },

            // Oolong Tea BOM
            new() { MenuItemId = dishTea.MenuItemId, IngredientId = ingTea.IngredientId, QuantityRequired = 15m }
        };
        context.Recipes.AddRange(recipes);
        await context.SaveChangesAsync();

        // 7. Seed Staff Users
        var users = new List<User>
        {
            new() { Username = "admin", PasswordHash = Core.Security.PasswordHasher.HashPassword("admin123"), FullName = "Hồ Thăng Tùng (Admin)", Role = UserRole.Admin },
            new() { Username = "manager", PasswordHash = Core.Security.PasswordHasher.HashPassword("manager123"), FullName = "Lương Đức Tín (Quản Lý)", Role = UserRole.StoreManager },
            new() { Username = "cashier", PasswordHash = Core.Security.PasswordHasher.HashPassword("cashier123"), FullName = "Phan Trần Hoàng Bảo (Thu Ngân)", Role = UserRole.Cashier },
            new() { Username = "chef", PasswordHash = Core.Security.PasswordHasher.HashPassword("chef123"), FullName = "Nguyễn Văn Bếp Trưởng (Bếp)", Role = UserRole.HeadChef }
        };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        // 8. Seed Sample Loyalty Member Customers
        var customers = new List<Customer>
        {
            new() { FullName = "Đặng Hoàng Giang (VIP)", PhoneNumber = "0901234567", Email = "giang.dh@gmail.com", LoyaltyPoints = 650, Tier = MembershipTier.Gold, TotalSpent = 15800000m },
            new() { FullName = "Nguyễn Thị Mai Lan", PhoneNumber = "0987654321", Email = "lan.ntm@gmail.com", LoyaltyPoints = 220, Tier = MembershipTier.Silver, TotalSpent = 4500000m },
            new() { FullName = "Trần Quốc Hưng", PhoneNumber = "0912345678", Email = "hung.tq@gmail.com", LoyaltyPoints = 1250, Tier = MembershipTier.Diamond, TotalSpent = 38500000m }
        };
        context.Customers.AddRange(customers);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Executes idempotent SQL DDL upgrades to ensure newer tables and columns exist in pre-existing SQL Server databases.
    /// Runs each statement independently so a single failure never cascades or blocks other schema upgrades.
    /// </summary>
    public static async Task EnsureSchemaUpgradesAsync(RestaurantDbContext context)
    {
        if (!context.Database.IsSqlServer()) return;

        var statements = new (string Name, string Sql)[]
        {
            ("OrderItems.IsCancelled",
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'IsCancelled') ALTER TABLE OrderItems ADD IsCancelled BIT NOT NULL DEFAULT 0;"),

            ("OrderItems.CancelReason",
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'CancelReason') ALTER TABLE OrderItems ADD CancelReason NVARCHAR(250) NULL;"),

            ("OrderItems.CancelledAt",
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'CancelledAt') ALTER TABLE OrderItems ADD CancelledAt DATETIME2 NULL;"),

            ("Payments.IdempotencyKey",
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'IdempotencyKey') ALTER TABLE Payments ADD IdempotencyKey NVARCHAR(100) NULL;"),

            ("Customers table",
                @"IF OBJECT_ID('Customers', 'U') IS NULL
                BEGIN
                    CREATE TABLE Customers (
                        CustomerId INT IDENTITY(1,1) PRIMARY KEY,
                        PhoneNumber NVARCHAR(20) NOT NULL UNIQUE,
                        FullName NVARCHAR(100) NOT NULL,
                        Email NVARCHAR(100) NULL,
                        Tier NVARCHAR(20) NOT NULL DEFAULT 'Bronze',
                        LoyaltyPoints INT NOT NULL DEFAULT 0,
                        TotalSpent DECIMAL(18,2) NOT NULL DEFAULT 0,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        LastVisitAt DATETIME2 NULL
                    );
                END"),

            ("WorkShifts table",
                @"IF OBJECT_ID('WorkShifts', 'U') IS NULL
                BEGIN
                    CREATE TABLE WorkShifts (
                        ShiftId INT IDENTITY(1,1) PRIMARY KEY,
                        UserId INT NOT NULL,
                        StartTime DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        EndTime DATETIME2 NULL,
                        OpeningFloat DECIMAL(18,2) NOT NULL DEFAULT 0,
                        CashSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                        CardSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                        QrSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                        TotalSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ActualCashEnd DECIMAL(18,2) NOT NULL DEFAULT 0,
                        CashVariance DECIMAL(18,2) NOT NULL DEFAULT 0,
                        Status NVARCHAR(20) NOT NULL DEFAULT 'Open',
                        Notes NVARCHAR(500) NULL
                    );
                END"),

            ("AuditLogs table",
                @"IF OBJECT_ID('AuditLogs', 'U') IS NULL
                BEGIN
                    CREATE TABLE AuditLogs (
                        AuditLogId INT IDENTITY(1,1) PRIMARY KEY,
                        Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        Username NVARCHAR(50) NOT NULL,
                        UserRole NVARCHAR(30) NOT NULL,
                        Action NVARCHAR(100) NOT NULL,
                        EntityName NVARCHAR(100) NOT NULL,
                        EntityId NVARCHAR(50) NULL,
                        OldValue NVARCHAR(MAX) NULL,
                        NewValue NVARCHAR(MAX) NULL,
                        Reason NVARCHAR(MAX) NULL,
                        MachineName NVARCHAR(100) NULL,
                        Details NVARCHAR(1000) NULL
                    );
                END")
        };

        foreach (var (name, sql) in statements)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DbInitializer] Migration step '{name}' notice: {ex.Message}");
            }
        }
    }
}
