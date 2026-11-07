# AI-Powered Smart Restaurant Management System

> **Học phần:** PRN212 — Advanced .NET Platform & C# Programming (Fall 2026)  
> **Khoa Kỹ thuật Phần mềm — Đại học FPT**  
> **Nhóm thực hiện:** Group 6  
> **Repository:** [https://github.com/gnutho1401/Smart-Restaurant-Management-System.git](https://github.com/gnutho1401/Smart-Restaurant-Management-System.git)

---

## 👥 Thành viên nhóm thực hiện (Group 6)

| STT | Họ và Tên | MSSV | Vai Trò Kỹ Thuật Chính | Đóng Góp |
|:---:|---|:---:|---|:---:|
| 1 | **Hồ Thăng Tùng** | **DE190479** | **Project Leader / System Architect**: Kiến trúc phân tầng MVVM, Domain Invariants, Services (Order, Billing, Table DFA), Optimistic Concurrency, Git Workflow | **34%** |
| 2 | **Lương Đức Tín** | **DE170539** | **Database Architect / Backend Engineer**: Thiết kế chuẩn hóa CSDL 3NF, EF Core 8 Fluent API, Bất biến BOM khấu trừ kho nguyên tử, DbInitializer Data Seeding | **33%** |
| 3 | **Phan Trần Hoàng Bảo** | **DE190695** | **WPF UI & AI/DevOps Specialist**: Màn hình cảm ứng (Floor Plan, Touch POS, KDS, Menu), LM Studio REST API & Context Grounding, CI/CD Pipeline & 32 xUnit Tests | **33%** |

---

## 🌟 Tổng Quan Dự Án

**AI-Powered Smart Restaurant Management System** là giải pháp phần mềm quản trị nhà hàng tích hợp công nghệ tiên tiến trên nền tảng **WPF (.NET 8 LTS)**, áp dụng mô hình kiến trúc chuẩn mực **MVVM (Model-View-ViewModel)** và cơ sở dữ liệu quan hệ quản trị bằng **Entity Framework Core 8**. 

Hệ thống tiên phong tích hợp trí tuệ nhân tạo thế hệ mới chạy cục bộ (**Local LLM: Mistral-7B-Instruct GGUF**) thông qua **LM Studio** ngoại tuyến trên cổng `localhost:1234/v1`. Giải pháp cam kết **$0 chi phí API đám mây** và bảo mật tuyệt đối 100% thông tin tài chính kinh doanh của nhà hàng.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       WPF Touch Presentation Layer                          │
│  [Floor Plan (DFA)] ─ [Touch POS Order] ─ [Kitchen KDS] ─ [Menu/BOM Editor] │
│              [Billing Settlement & VAT 8%] ─ [AI Assistant Chat]            │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ MVVM Data Binding / Commands
┌──────────────────────────────────────▼──────────────────────────────────────┐
│                        Application Services Layer                           │
│   TableService ─ OrderService ─ InventoryService ─ KitchenService ─ Billing │
└──────────────────────┬───────────────────────────────┬──────────────────────┘
                       │                               │
┌──────────────────────▼──────────────┐ ┌──────────────▼──────────────────────┐
│      Relational Data Tier           │ │           Local AI Engine           │
│   EF Core 8 Fluent API (3NF)        │ │  LM Studio (Mistral-7B-Instruct)    │
│   SQL Server / In-Memory            │ │  LINQ Context Grounding (<1.8% err) │
│   Optimistic Concurrency & BOM      │ │  BCG Matrix & Peak Hour Forecasting │
└─────────────────────────────────────┘ └─────────────────────────────────────┘
```

---

## 🏛️ Cấu Trúc Giải Pháp 6 Tầng (Solution Architecture)

```
SmartRestaurant-Management-System/
├── .github/
│   └── workflows/
│       └── ci.yml                   # CI/CD Pipeline (Build, Test, Quality Gate 70%)
├── SmartRestaurant.Core/             # Tầng nghiệp vụ lõi (Domain Entities, Enums, Interfaces, Exceptions)
│   ├── Entities/                    # DiningArea, DiningTable, MenuItem, Recipe, Order, Invoice, Payment, User...
│   ├── Enums/                       # TableStatus (DFA), CookingStatus, PaymentMethod, UserRole
│   ├── Exceptions/                  # InsufficientStockException, ConcurrencyConflictException...
│   └── Interfaces/                  # ITableService, IOrderService, IInventoryService, IBillingService, IAiService...
├── SmartRestaurant.Data/             # Tầng dữ liệu & CSDL (EF Core 8, 3NF, Fluent API)
│   ├── RestaurantDbContext.cs       # 11 DbSets, Cấu hình Fluent API, Concurrency Tokens, Precision
│   └── DbInitializer.cs             # Seed data mẫu chuẩn (Khu vực, Bàn, Thực đơn, Định lượng BOM, Users)
├── SmartRestaurant.Services/         # Tầng dịch vụ xử lý quy tắc nghiệp vụ
│   ├── TableService.cs              # Quản lý trạng thái bàn theo máy trạng thái DFA
│   ├── MenuService.cs               # Quản lý danh mục món ăn & công thức định lượng BOM
│   ├── OrderService.cs              # Xử lý đặt món, tạm tính thời gian thực, lưu vết dị ứng
│   ├── InventoryService.cs          # Khấu trừ kho BOM nguyên tử, kiểm tra bất biến & hoàn tác giao dịch
│   ├── KitchenService.cs            # KDS hàng đợi chế biến, thời gian chờ, chuyển đổi trạng thái bếp
│   └── BillingService.cs            # Quyết toán tài chính, thuế VAT 8% luật định, chuyển bàn sang Cleaning
├── SmartRestaurant.AIEngine/         # Tầng trí tuệ nhân tạo cục bộ (Local GenAI Engine)
│   ├── LmStudioService.cs           # REST Client kết nối LM Studio (localhost:1234/v1)
│   ├── ContextGroundingHelper.cs    # Tổng hợp Database Snapshot chống ảo giác (Anti-hallucination)
│   ├── AiAnalyticsEngine.cs         # Ma trận BCG Menu Engineering, dự báo giờ cao điểm & tồn kho
│   └── Models/                      # AI DTOs (BcgResult, PeakHourSlot, StockReorderItem...)
├── SmartRestaurant.WPF/              # Tầng giao diện cảm ứng (WPF .NET 8, MVVM, Dependency Injection)
│   ├── Converters/                  # StatusToBrushConverter, CookingStatusConverter, CurrencyConverter...
│   ├── MVVM/                        # ViewModelBase, RelayCommand, AsyncRelayCommand
│   ├── ViewModels/                  # FloorPlanViewModel, PosOrderViewModel, KitchenKdsViewModel, BillingViewModel...
│   ├── Views/                       # FloorPlanView, PosOrderView, KitchenKdsView, MenuView, BillingDialog...
│   └── MainWindow.xaml              # Master Navigation Shell & Ribbon Quick Status
├── SmartRestaurant.Tests/            # Tầng kiểm thử tự động (xUnit Test Suite)
│   ├── TableServiceTests.cs         # 6 Tests (Máy trạng thái DFA, chặn nhảy cóc trạng thái)
│   ├── MenuServiceTests.cs          # 5 Tests (Thêm món, giá âm, tên rỗng, lọc sẵn sàng, xóa mềm)
│   ├── OrderServiceTests.cs         # 7 Tests (Tạo order, tính tạm tính, dị ứng, trạng thái)
│   ├── InventoryServiceTests.cs     # 6 Tests (Trừ kho BOM, thiếu hàng rollback, tăng kho, cảnh báo thấp)
│   ├── BillingServiceTests.cs       # 4 Tests (Thuế VAT 8%, chiết khấu, bàn sang Cleaning)
│   └── AiServiceTests.cs            # 4 Tests (Context grounding, negative warning, ma trận BCG, offline fallback)
└── README.md                         # Tài liệu hướng dẫn kỹ thuật chi tiết
```

---

## ⚙️ Các Bất Biến & Quy Tắc Nghiệp Vụ Cốt Lõi

### 1. Máy Trạng Thái Hữu Hạn Quản Lý Bàn (4-State Table DFA)
Bàn ăn vận hành nghiêm ngặt qua 4 trạng thái để ngăn ngừa hoàn toàn tình trạng mất dữ liệu gọi món hoặc bỏ qua khâu vệ sinh:
$$\text{Available} \xrightarrow{\text{Mở bàn}} \text{Occupied} \xrightarrow{\text{Thanh toán xong}} \text{Cleaning} \xrightarrow{\text{Dọn xong}} \text{Available}$$
- Cho phép: $\text{Available} \leftrightarrow \text{Reserved}$.
- Chặn tuyệt đối: Chuyển trực tiếp từ $\text{Occupied} \rightarrow \text{Available}$ khi chưa qua $\text{Cleaning}$ (ném ngoại lệ `InvalidStatusTransitionException`).

### 2. Khấu Trừ Kho Định Lượng Nguyên Liệu Nguyên Tử (Atomic BOM Inventory Deduction)
Mỗi món ăn $D_i$ tiêu thụ tập hợp nguyên liệu thô $R_k$ theo tỷ lệ định mức $\text{BOM}(D_i, R_k)$.
Hệ thống kiểm tra bất biến toán học trước khi trừ kho:
$$\text{Stock}_{\text{new}}(R_k) = \text{Stock}_{\text{old}}(R_k) - \sum_{i} \left( Q_i \times \text{BOM}(D_i, R_k) \right) \ge 0 \quad \forall k$$
Nếu bất kỳ nguyên liệu nào vi phạm ($\text{Stock}_{\text{new}} < 0$), toàn bộ giao dịch được **Rollback nguyên tử ngay lập tức**, ném `InsufficientStockException` để bảo toàn tính toàn vẹn kho hàng.

### 3. Công Thức Tài Chính Thuế Suất VAT 8% Luật Định
Tính toán chính xác theo Nghị định về chính sách giảm thuế GTGT của Chính phủ:
$$\text{TaxableAmount} = \max(0, \text{SubTotal} - \text{DiscountAmount})$$
$$\text{VatAmount} = \text{Round}(\text{TaxableAmount} \times 0.0800, 2, \text{AwayFromZero})$$
$$\text{TotalDue} = \text{TaxableAmount} + \text{VatAmount}$$

### 4. Triệt Tiêu Ảo Giác AI (LINQ Context Grounding & Negative Constraint)
Để ngăn mô hình ngôn ngữ lớn (LLM) bịa đặt món không có trong thực đơn hoặc sai lệch doanh thu, prompt gửi đến LM Studio được tổng hợp theo công thức:
$$P_{\text{final}} = T_{\text{system\_warning}} \oplus S_{\text{snapshot}}(\text{LINQ}) \oplus Q_{\text{user}}$$
Trong đó $T_{\text{system\_warning}}$ chứa cảnh báo nghiêm ngặt buộc mô hình chỉ dùng số liệu từ CSDL, kéo giảm tỷ lệ ảo giác xuống mức **$< 1.8\%$**.

---

## 🧪 Bộ 32 Unit Tests xUnit (100% Pass)

Toàn bộ 32 kiểm thử đơn vị được thực thi độc lập bằng cơ chế **In-Memory Database**:

| Nhóm Kiểm Thử | Số Lượng | Trọng Tâm Xác Minh | Kết Quả |
|---|:---:|---|:---:|
| **TableServiceTests** | 6 | Xác thực chuyển đổi trạng thái DFA; chặn Occupied $\rightarrow$ Available; kiểm tra đặt trước | **PASSED (6/6)** |
| **MenuServiceTests** | 5 | Thêm món hợp lệ; chặn giá âm; chặn tên trống; lọc món đang bán; xác nhận xóa mềm | **PASSED (5/5)** |
| **OrderServiceTests** | 7 | Tạo order mới; tính tạm tính tự động; ghi nhận cảnh báo dị ứng; hủy món an toàn | **PASSED (7/7)** |
| **InventoryServiceTests** | 6 | Khấu trừ kho BOM chính xác; rollback nguyên tử khi thiếu kho; nhập kho; cảnh báo tồn thấp | **PASSED (6/6)** |
| **BillingServiceTests** | 4 | Áp dụng chính xác thuế VAT 8%; chiết khấu trước thuế; chuyển bàn sang Cleaning sau khi trả tiền | **PASSED (4/4)** |
| **AiServiceTests** | 4 | Tiêm snapshot dữ liệu vào prompt; kiểm tra cảnh báo triệt tiêu ảo giác; phân loại ma trận BCG; xử lý graceful khi LM Studio offline | **PASSED (4/4)** |
| **TỔNG CỘNG** | **32** | **Bảo đảm 100% các bất biến nghiệp vụ và chất lượng phần mềm** | **100% PASS** |

---

## 🔄 Quy Trình CI/CD Tự Động Hóa (GitHub Actions)

Mỗi lần đẩy mã nguồn lên nhánh `main` hoặc tạo Pull Request, quy trình tự động trong `.github/workflows/ci.yml` sẽ được kích hoạt trên môi trường `windows-latest`:
1. **Checkout & Setup:** Khởi tạo môi trường .NET 8 LTS SDK.
2. **Cache:** Lưu bộ nhớ đệm NuGet Packages để tăng tốc độ biên dịch.
3. **Restore & Build:** Biên dịch toàn bộ Solution ở chế độ `Release` không cảnh báo.
4. **Automated Test & Coverage:** Thực thi trọn vẹn 32 unit tests xUnit và thu thập báo cáo độ bao phủ `coverage.cobertura.xml`.
5. **Quality Gate:** Tự động xác minh độ bao phủ mã nguồn $\ge 70\%$.
6. **Artifact Upload:** Đóng gói và lưu trữ kết quả kiểm thử trong 14 ngày.

---

## 🚀 Hướng Dẫn Cài Đặt & Khởi Chạy

### Yêu cầu hệ thống:
- **Hệ điều hành:** Windows 10/11 (64-bit).
- **Phần mềm:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- **Mô hình AI Cục bộ (Tùy chọn):** Cài đặt [LM Studio](https://lmstudio.ai/), tải mô hình `mistral-7b-instruct-v0.2.Q4_K_M.gguf` và bật **Start Server** tại `http://localhost:1234`. *(Nếu không bật LM Studio, hệ thống tự động kích hoạt chế độ ngoại tuyến an toàn).*

### Thao tác lệnh nhanh:
```bash
# 1. Clone repository về máy tính
git clone https://github.com/gnutho1401/Smart-Restaurant-Management-System.git
cd Smart-Restaurant-Management-System

# 2. Khôi phục thư viện và biên dịch toàn bộ hệ thống
dotnet restore
dotnet build --configuration Release

# 3. Chạy toàn bộ 32 Unit Tests xUnit
dotnet test

# 4. Khởi chạy ứng dụng giao diện WPF cảm ứng
dotnet run --project SmartRestaurant.WPF/SmartRestaurant.WPF.csproj
```

### Đóng gói Single-File Executable (.exe độc lập):
Để xuất bản gói chạy độc lập không phụ thuộc môi trường cài đặt:
```bash
dotnet publish SmartRestaurant.WPF/SmartRestaurant.WPF.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output ./publish
```
Tập tin thực thi `SmartRestaurant.WPF.exe` duy nhất (~14.7 MB) sẽ được tạo trong thư mục `publish/`.

---

## 🔐 Tài Khoản Truy Cập Mặc Định (Seed Data)

Cơ sở dữ liệu tự động khởi tạo các tài khoản phân quyền mẫu:

| Tên Đăng Nhập | Mật Khẩu | Vai Trò | Phạm Vi Quyền Hạn |
|---|---|:---:|---|
| `admin` | `admin123` | **Manager** | Toàn quyền quản trị sơ đồ bàn, thực đơn, định mức BOM, kho hàng và báo cáo AI |
| `waiter1` | `waiter123` | **Staff** | Mở bàn, phục vụ khách, tạo đơn hàng POS và lập hóa đơn thanh toán |
| `chef1` | `chef123` | **Kitchen** | Truy cập màn hình bếp KDS, cập nhật tiến độ nấu món |

---

## 📜 Giấy Phép & Bản Quyền

Dự án được xây dựng và hoàn thiện phục vụ học phần **PRN212** tại **Trường Đại học FPT**, bảo lưu mọi quyền sở hữu trí tuệ thuộc về các thành viên **Group 6**.
