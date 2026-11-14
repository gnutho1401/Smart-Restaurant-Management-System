USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SmartRestaurantDb')
BEGIN
    CREATE DATABASE SmartRestaurantDb;
    PRINT N'Database [SmartRestaurantDb] created successfully.';
END
ELSE
BEGIN
    PRINT N'Database [SmartRestaurantDb] already exists.';
END
GO

USE SmartRestaurantDb;
GO

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NOT NULL DROP TABLE dbo.AuditLogs;
IF OBJECT_ID(N'dbo.WorkShifts', N'U') IS NOT NULL DROP TABLE dbo.WorkShifts;
IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL DROP TABLE dbo.Customers;
IF OBJECT_ID(N'dbo.Payments', N'U') IS NOT NULL DROP TABLE dbo.Payments;
IF OBJECT_ID(N'dbo.Invoices', N'U') IS NOT NULL DROP TABLE dbo.Invoices;
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL DROP TABLE dbo.OrderItems;
IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID(N'dbo.Recipes', N'U') IS NOT NULL DROP TABLE dbo.Recipes;
IF OBJECT_ID(N'dbo.Ingredients', N'U') IS NOT NULL DROP TABLE dbo.Ingredients;
IF OBJECT_ID(N'dbo.MenuItems', N'U') IS NOT NULL DROP TABLE dbo.MenuItems;
IF OBJECT_ID(N'dbo.MenuCategories', N'U') IS NOT NULL DROP TABLE dbo.MenuCategories;
IF OBJECT_ID(N'dbo.DiningTables', N'U') IS NOT NULL DROP TABLE dbo.DiningTables;
IF OBJECT_ID(N'dbo.DiningAreas', N'U') IS NOT NULL DROP TABLE dbo.DiningAreas;
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL DROP TABLE dbo.Users;
GO

CREATE TABLE dbo.DiningAreas (
    AreaId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiningAreas PRIMARY KEY CLUSTERED,
    AreaName NVARCHAR(50) NOT NULL,
    FloorNumber INT NOT NULL CONSTRAINT DF_DiningAreas_FloorNumber DEFAULT (1)
);
GO

CREATE TABLE dbo.DiningTables (
    TableId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiningTables PRIMARY KEY CLUSTERED,
    AreaId INT NOT NULL CONSTRAINT FK_DiningTables_DiningAreas FOREIGN KEY REFERENCES dbo.DiningAreas(AreaId),
    TableName NVARCHAR(50) NOT NULL,
    Capacity INT NOT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT CK_DiningTables_Status CHECK (Status IN (N'Available', N'Occupied', N'Reserved', N'Cleaning')),
    RowVersion ROWVERSION NOT NULL
);
CREATE NONCLUSTERED INDEX IX_DiningTables_AreaId ON dbo.DiningTables(AreaId);
CREATE NONCLUSTERED INDEX IX_DiningTables_Status ON dbo.DiningTables(Status);
GO

CREATE TABLE dbo.MenuCategories (
    CategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MenuCategories PRIMARY KEY CLUSTERED,
    CategoryName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL
);
GO

CREATE TABLE dbo.MenuItems (
    MenuItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MenuItems PRIMARY KEY CLUSTERED,
    CategoryId INT NOT NULL CONSTRAINT FK_MenuItems_MenuCategories FOREIGN KEY REFERENCES dbo.MenuCategories(CategoryId),
    Name NVARCHAR(100) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    Description NVARCHAR(500) NULL,
    ImageUrl NVARCHAR(255) NULL,
    IsAvailable BIT NOT NULL CONSTRAINT DF_MenuItems_IsAvailable DEFAULT (1)
);
CREATE NONCLUSTERED INDEX IX_MenuItems_CategoryId ON dbo.MenuItems(CategoryId);
CREATE NONCLUSTERED INDEX IX_MenuItems_IsAvailable ON dbo.MenuItems(IsAvailable);
GO

CREATE TABLE dbo.Ingredients (
    IngredientId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ingredients PRIMARY KEY CLUSTERED,
    Name NVARCHAR(100) NOT NULL,
    Unit NVARCHAR(20) NOT NULL,
    CurrentStock DECIMAL(18,2) NOT NULL,
    MinStock DECIMAL(18,2) NOT NULL,
    CostPerUnit DECIMAL(18,2) NOT NULL CONSTRAINT DF_Ingredients_CostPerUnit DEFAULT (0),
    RowVersion ROWVERSION NOT NULL
);
GO

CREATE TABLE dbo.Recipes (
    RecipeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Recipes PRIMARY KEY CLUSTERED,
    MenuItemId INT NOT NULL CONSTRAINT FK_Recipes_MenuItems FOREIGN KEY REFERENCES dbo.MenuItems(MenuItemId) ON DELETE CASCADE,
    IngredientId INT NOT NULL CONSTRAINT FK_Recipes_Ingredients FOREIGN KEY REFERENCES dbo.Ingredients(IngredientId) ON DELETE CASCADE,
    QuantityRequired DECIMAL(18,2) NOT NULL,
    CONSTRAINT UQ_Recipes_MenuItem_Ingredient UNIQUE (MenuItemId, IngredientId)
);
CREATE NONCLUSTERED INDEX IX_Recipes_MenuItemId ON dbo.Recipes(MenuItemId);
CREATE NONCLUSTERED INDEX IX_Recipes_IngredientId ON dbo.Recipes(IngredientId);
GO

CREATE TABLE dbo.Users (
    UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY CLUSTERED,
    Username NVARCHAR(50) NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Role NVARCHAR(30) NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN (N'Admin', N'StoreManager', N'Cashier', N'HeadChef'))
);
GO

CREATE TABLE dbo.Orders (
    OrderId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED,
    TableId INT NOT NULL CONSTRAINT FK_Orders_DiningTables FOREIGN KEY REFERENCES dbo.DiningTables(TableId),
    CreatedByUserId INT NOT NULL CONSTRAINT DF_Orders_CreatedByUserId DEFAULT (1),
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT (SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Orders_Status DEFAULT (N'Active')
        CONSTRAINT CK_Orders_Status CHECK (Status IN (N'Active', N'Completed', N'Cancelled')),
    SubTotal DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_SubTotal DEFAULT (0),
    DiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_DiscountAmount DEFAULT (0),
    FinalTotal DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_FinalTotal DEFAULT (0)
);
CREATE NONCLUSTERED INDEX IX_Orders_TableId ON dbo.Orders(TableId);
CREATE NONCLUSTERED INDEX IX_Orders_Status ON dbo.Orders(Status);
CREATE NONCLUSTERED INDEX IX_Orders_CreatedAt ON dbo.Orders(CreatedAt);
GO

CREATE TABLE dbo.OrderItems (
    OrderItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY CLUSTERED,
    OrderId INT NOT NULL CONSTRAINT FK_OrderItems_Orders FOREIGN KEY REFERENCES dbo.Orders(OrderId) ON DELETE CASCADE,
    MenuItemId INT NOT NULL CONSTRAINT FK_OrderItems_MenuItems FOREIGN KEY REFERENCES dbo.MenuItems(MenuItemId),
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    CookingStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_OrderItems_CookingStatus DEFAULT (N'Pending')
        CONSTRAINT CK_OrderItems_CookingStatus CHECK (CookingStatus IN (N'Pending', N'Cooking', N'Ready', N'Served')),
    Note NVARCHAR(250) NULL,
    IsCancelled BIT NOT NULL CONSTRAINT DF_OrderItems_IsCancelled DEFAULT (0),
    CancelReason NVARCHAR(250) NULL,
    CancelledAt DATETIME2 NULL
);
CREATE NONCLUSTERED INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
CREATE NONCLUSTERED INDEX IX_OrderItems_CookingStatus ON dbo.OrderItems(CookingStatus);
GO

CREATE TABLE dbo.Invoices (
    InvoiceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY CLUSTERED,
    OrderId INT NOT NULL CONSTRAINT FK_Invoices_Orders FOREIGN KEY REFERENCES dbo.Orders(OrderId),
    InvoiceCode NVARCHAR(50) NOT NULL CONSTRAINT UQ_Invoices_InvoiceCode UNIQUE,
    VatRate DECIMAL(5,4) NOT NULL CONSTRAINT DF_Invoices_VatRate DEFAULT (0.0800),
    VatAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Invoices_VatAmount DEFAULT (0),
    TotalDue DECIMAL(18,2) NOT NULL,
    IssuedAt DATETIME2 NOT NULL CONSTRAINT DF_Invoices_IssuedAt DEFAULT (SYSUTCDATETIME())
);
CREATE NONCLUSTERED INDEX IX_Invoices_OrderId ON dbo.Invoices(OrderId);
GO

CREATE TABLE dbo.Payments (
    PaymentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY CLUSTERED,
    InvoiceId INT NOT NULL CONSTRAINT FK_Payments_Invoices FOREIGN KEY REFERENCES dbo.Invoices(InvoiceId) ON DELETE CASCADE,
    Method NVARCHAR(30) NOT NULL CONSTRAINT CK_Payments_Method CHECK (Method IN (N'Cash', N'CreditCard', N'BankTransferQR')),
    AmountPaid DECIMAL(18,2) NOT NULL,
    PaidAt DATETIME2 NOT NULL CONSTRAINT DF_Payments_PaidAt DEFAULT (SYSUTCDATETIME()),
    IdempotencyKey NVARCHAR(100) NULL
);
CREATE NONCLUSTERED INDEX IX_Payments_InvoiceId ON dbo.Payments(InvoiceId);
GO

CREATE TABLE dbo.Customers (
    CustomerId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED,
    PhoneNumber NVARCHAR(20) NOT NULL CONSTRAINT UQ_Customers_PhoneNumber UNIQUE,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NULL,
    Tier NVARCHAR(20) NOT NULL CONSTRAINT DF_Customers_Tier DEFAULT (N'Bronze'),
    LoyaltyPoints INT NOT NULL CONSTRAINT DF_Customers_LoyaltyPoints DEFAULT (0),
    TotalSpent DECIMAL(18,2) NOT NULL CONSTRAINT DF_Customers_TotalSpent DEFAULT (0),
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastVisitAt DATETIME2 NULL
);
CREATE NONCLUSTERED INDEX IX_Customers_PhoneNumber ON dbo.Customers(PhoneNumber);
GO

CREATE TABLE dbo.WorkShifts (
    ShiftId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkShifts PRIMARY KEY CLUSTERED,
    UserId INT NOT NULL CONSTRAINT FK_WorkShifts_Users FOREIGN KEY REFERENCES dbo.Users(UserId),
    StartTime DATETIME2 NOT NULL CONSTRAINT DF_WorkShifts_StartTime DEFAULT (SYSUTCDATETIME()),
    EndTime DATETIME2 NULL,
    OpeningFloat DECIMAL(18,2) NOT NULL CONSTRAINT DF_WorkShifts_OpeningFloat DEFAULT (0),
    CashSales DECIMAL(18,2) NOT NULL CONSTRAINT DF_WorkShifts_CashSales DEFAULT (0),
    CardSales DECIMAL(18,2) NOT NULL CONSTRAINT DF_WorkShifts_CardSales DEFAULT (0),
    QrSales DECIMAL(18,2) NOT NULL CONSTRAINT DF_WorkShifts_QrSales DEFAULT (0),
    TotalSales DECIMAL(18,2) NOT NULL CONSTRAINT DF_WorkShifts_TotalSales DEFAULT (0),
    ActualCashEnd DECIMAL(18,2) NOT NULL CONSTRAINT DF_WorkShifts_ActualCashEnd DEFAULT (0),
    CashVariance DECIMAL(18,2) NOT NULL CONSTRAINT DF_WorkShifts_CashVariance DEFAULT (0),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WorkShifts_Status DEFAULT (N'Open'),
    Notes NVARCHAR(500) NULL
);
CREATE NONCLUSTERED INDEX IX_WorkShifts_UserId ON dbo.WorkShifts(UserId);
GO

CREATE TABLE dbo.AuditLogs (
    AuditLogId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED,
    Timestamp DATETIME2 NOT NULL CONSTRAINT DF_AuditLogs_Timestamp DEFAULT (SYSUTCDATETIME()),
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
CREATE NONCLUSTERED INDEX IX_AuditLogs_Timestamp ON dbo.AuditLogs(Timestamp);
GO

PRINT N'Seeding initial data into SmartRestaurantDb...';

SET IDENTITY_INSERT dbo.DiningAreas ON;
INSERT INTO dbo.DiningAreas (AreaId, AreaName, FloorNumber) VALUES
(1, N'Sảnh Chính (Main Dining)', 1),
(2, N'Phòng VIP Hoàng Gia', 2),
(3, N'Khu Vực Sân Vườn (Terrace)', 1);
SET IDENTITY_INSERT dbo.DiningAreas OFF;
GO

SET IDENTITY_INSERT dbo.DiningTables ON;
INSERT INTO dbo.DiningTables (TableId, AreaId, TableName, Capacity, Status) VALUES
(1, 1, N'T-01', 4, N'Available'),
(2, 1, N'T-02', 2, N'Available'),
(3, 1, N'T-03', 6, N'Occupied'),
(4, 1, N'T-04', 4, N'Available'),
(5, 1, N'T-05', 8, N'Available'),
(6, 2, N'VIP-01', 10, N'Reserved'),
(7, 2, N'VIP-02', 6, N'Cleaning'),
(8, 2, N'VIP-03', 4, N'Available'),
(9, 3, N'O-01', 4, N'Available'),
(10, 3, N'O-02', 2, N'Available');
SET IDENTITY_INSERT dbo.DiningTables OFF;
GO

SET IDENTITY_INSERT dbo.MenuCategories ON;
INSERT INTO dbo.MenuCategories (CategoryId, CategoryName, Description) VALUES
(1, N'Khai Vị (Appetizers)', N'Các món khai vị thanh tao, kích thích vị giác.'),
(2, N'Món Chính (Main Courses)', N'Món ăn thượng hạng chế biến từ nguyên liệu cao cấp nhập khẩu.'),
(3, N'Tráng Miệng (Desserts)', N'Bánh ngọt và món tráng miệng phong cách Âu tinh tế.'),
(4, N'Đồ Uống (Beverages)', N'Rượu vang hảo hạng, cocktail và thức uống trà thanh nhiệt.');
SET IDENTITY_INSERT dbo.MenuCategories OFF;
GO

SET IDENTITY_INSERT dbo.Ingredients ON;
INSERT INTO dbo.Ingredients (IngredientId, Name, Unit, CurrentStock, MinStock, CostPerUnit) VALUES
(1, N'Thịt Bò Wagyu A5', N'g', 5000.00, 1000.00, 1500.00),
(2, N'Nấm Truffle Đen', N'g', 1000.00, 200.00, 2500.00),
(3, N'Tôm Hùm Alaska', N'g', 8000.00, 1500.00, 1200.00),
(4, N'Cá Hồi Na Uy Tươi', N'g', 6000.00, 1200.00, 950.00),
(5, N'Sốt Phô Mai Ý', N'ml', 4000.00, 800.00, 300.00),
(6, N'Kem Tươi Whipping Cream', N'ml', 5000.00, 1000.00, 200.00),
(7, N'Sô-cô-la Đen Bỉ', N'g', 3000.00, 500.00, 600.00),
(8, N'Rượu Vang Đỏ Cabernet', N'ml', 10000.00, 2000.00, 250.00),
(9, N'Trà Ô Long Thượng Hạng', N'g', 2000.00, 300.00, 500.00),
(10, N'Khoai Tây Vàng Đà Lạt', N'g', 15000.00, 3000.00, 50.00),
(11, N'Dầu Ô-liu Nguyên Chất', N'ml', 5000.00, 1000.00, 150.00);
SET IDENTITY_INSERT dbo.Ingredients OFF;
GO

SET IDENTITY_INSERT dbo.MenuItems ON;
INSERT INTO dbo.MenuItems (MenuItemId, CategoryId, Name, Price, Description, IsAvailable) VALUES
(1, 2, N'Bò Wagyu Áp Chảo Sốt Tiêu Đen', 450000.00, N'Thịt bò Wagyu A5 mềm mọng tan chảy trên đầu lưỡi, phục vụ kèm khoai tây nghiền và sốt tiêu đen đậm đà.', 1),
(2, 1, N'Súp Kem Nấm Truffle Hoàng Gia', 185000.00, N'Súp kem béo ngậy nấu từ nấm truffle đen thượng hạng và whipping cream thơm lừng.', 1),
(3, 2, N'Tôm Hùm Nướng Phô Mai Đút Lò', 680000.00, N'Tôm hùm Alaska tươi sống phủ lớp sốt phô mai Ý nướng vàng óng thơm ngậy.', 1),
(4, 2, N'Cá Hồi Na Uy Áp Chảo Dầu Ô-liu', 320000.00, N'Phi lê cá hồi Na Uy da giòn rụm, thịt hồng ngọt tự nhiên áp chảo cùng dầu ô-liu nguyên chất.', 1),
(5, 3, N'Bánh Mousse Sô-cô-la Bỉ', 95000.00, N'Mousse socola mịn màng chế tác từ 70% sô-cô-la nguyên chất từ vương quốc Bỉ.', 1),
(6, 4, N'Rượu Vang Đỏ Cabernet Sauvignon', 250000.00, N'Ly vang đỏ thượng hạng niên vụ 2020 hương vị quả mọng và gỗ sồi tinh tế.', 1),
(7, 4, N'Trà Ô Long Sen Vàng', 55000.00, N'Trà ô long ướp hạt sen bùi béo thanh nhiệt, ngọt ngào dễ chịu.', 1);
SET IDENTITY_INSERT dbo.MenuItems OFF;
GO

SET IDENTITY_INSERT dbo.Recipes ON;
INSERT INTO dbo.Recipes (RecipeId, MenuItemId, IngredientId, QuantityRequired) VALUES

(1, 1, 1, 250.00),
(2, 1, 10, 100.00),
(3, 1, 11, 15.00),

(4, 2, 2, 50.00),
(5, 2, 6, 100.00),

(6, 3, 3, 400.00),
(7, 3, 5, 80.00),

(8, 4, 4, 200.00),
(9, 4, 11, 15.00),

(10, 5, 7, 80.00),
(11, 5, 6, 50.00),

(12, 6, 8, 150.00),

(13, 7, 9, 15.00);
SET IDENTITY_INSERT dbo.Recipes OFF;
GO

SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (UserId, Username, PasswordHash, FullName, Role) VALUES
(1, N'admin', N'q4+PCo11EIoVoDVN+mQ1YQ==:UGIzHYa3oMu8Sm8Cl5yFQl4s62phRYPSNGXhQaknn9E=', N'Hồ Thăng Tùng (Admin)', N'Admin'),
(2, N'manager', N'43QlLzbmZWPrcJrM8yMawg==:5hc7gd11002E8yFg12LuuEV1orxiCtHqOq/ErmrgVFw=', N'Lương Đức Tín (Quản Lý)', N'StoreManager'),
(3, N'cashier', N'7R4iS7eyCu+dAGMF7FP2pg==:Z4slefUr6yoYAcz9FiymY5YHyuupKvpdgvdcRoVH9Gs=', N'Phan Trần Hoàng Bảo (Thu Ngân)', N'Cashier'),
(4, N'chef', N'T8VIkLPUJNaqS/qTVqVH+Q==:nO68qiJ0lcfw0nnvnKqjk7M/B6kghaqOd2ikwDCC9lM=', N'Nguyễn Văn Bếp Trưởng (Bếp)', N'HeadChef');
SET IDENTITY_INSERT dbo.Users OFF;
GO

SET IDENTITY_INSERT dbo.Orders ON;
INSERT INTO dbo.Orders (OrderId, TableId, CreatedByUserId, CreatedAt, Status, SubTotal, DiscountAmount, FinalTotal) VALUES
(1, 3, 1, DATEADD(MINUTE, -25, SYSUTCDATETIME()), N'Active', 635000.00, 0, 685800.00);
SET IDENTITY_INSERT dbo.Orders OFF;
GO

SET IDENTITY_INSERT dbo.OrderItems ON;
INSERT INTO dbo.OrderItems (OrderItemId, OrderId, MenuItemId, Quantity, UnitPrice, CookingStatus, Note) VALUES
(1, 1, 1, 1, 450000.00, N'Cooking', N'Chín vừa (Medium-rare)'),
(2, 1, 2, 1, 185000.00, N'Ready', N'Ít tiêu');
SET IDENTITY_INSERT dbo.OrderItems OFF;
GO

SET IDENTITY_INSERT dbo.Customers ON;
INSERT INTO dbo.Customers (CustomerId, PhoneNumber, FullName, Email, Tier, LoyaltyPoints, TotalSpent) VALUES
(1, N'0901234567', N'Đặng Hoàng Giang (VIP)', N'giang.dh@gmail.com', N'Gold', 650, 15800000.00),
(2, N'0987654321', N'Nguyễn Thị Mai Lan', N'lan.ntm@gmail.com', N'Silver', 220, 4500000.00),
(3, N'0912345678', N'Trần Quốc Hưng', N'hung.tq@gmail.com', N'Diamond', 1250, 38500000.00);
SET IDENTITY_INSERT dbo.Customers OFF;
GO

PRINT N'===================================================================';
PRINT N'  SMARTRESTAURANT DATABASE CREATION & SEEDING COMPLETED 100%!';
PRINT N'===================================================================';
GO
