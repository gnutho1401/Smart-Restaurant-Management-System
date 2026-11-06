# AI-Powered Smart Restaurant Management System

> **Học phần:** PRN212 — Advanced .NET Platform & C# Programming (Fall 2026)  
> **Khoa Kỹ thuật Phần mềm — Đại học FPT**  
> **Nhóm thực hiện:** Group 6  

---

## 👥 Thành viên nhóm thực hiện (Group 6)

| STT | Họ và Tên | MSSV | Vai Trò Kỹ Thuật Chính | Đóng Góp |
|:---:|---|:---:|---|:---:|
| 1 | **Hồ Thăng Tùng** | **DE190479** | Project Leader, Kiến trúc sư hệ thống, MVVM Architecture, Order & Billing Services, Concurrency Control, Git Tracking | 34% |
| 2 | **Lương Đức Tín** | **DE170539** | Database Architect, CSDL 3NF, EF Core 8 Fluent API, Bất biến BOM trừ kho nguyên liệu, Data Seeding | 33% |
| 3 | **Phan Trần Hoàng Bảo** | **DE190695** | WPF UI & AI/DevOps Specialist, Màn hình cảm ứng (POS, Floor Plan, KDS, Menu), LM Studio REST API, CI/CD Pipeline & xUnit | 33% |

---

## 🌟 Tổng Quan Dự Án

**AI-Powered Smart Restaurant Management System** là giải pháp phần mềm quản trị nhà hàng toàn diện trên nền tảng **WPF (.NET 8 LTS)**, áp dụng mô hình kiến trúc **MVVM** và cơ sở dữ liệu quan hệ **Entity Framework Core 8**. Dự án tích hợp trí tuệ nhân tạo cục bộ (**Local LLM: Mistral-7B-Instruct GGUF**) thông qua **LM Studio** chạy ngoại tuyến trên cổng `localhost:1234/v1`, đảm bảo $0 chi phí API đám mây và bảo mật 100% dữ liệu tài chính nội bộ.

### Các phân hệ cốt lõi:
1. **Sơ đồ bàn tương tác (Dynamic Floor Plan):** Lưới bàn cảm ứng trực quan với 4 trạng thái vận hành (*Available, Occupied, Reserved, Cleaning*) tuân thủ máy trạng thái hữu hạn DFA.
2. **Màn hình gọi món POS cảm ứng (Touch POS Ordering):** Lọc danh mục, tìm kiếm nhanh LINQ, ghi chú cảnh báo dị ứng thực phẩm cho bếp, tính tổng tiền tức thời.
3. **Màn hình hiển thị bếp thông minh (Kitchen Display System - KDS):** Hàng đợi vé món số hóa với viền màu cảnh báo theo thời gian thực (Xanh < 10 phút, Vàng 10-20 phút, Đỏ > 20 phút) chạy nền với `PeriodicTimer`.
4. **Quản lý Thực đơn & Định lượng BOM (Menu & Recipe BOM):** Danh mục món ăn và định lượng nguyên liệu thô tiêu hao cấu thành món.
5. **Khấu trừ kho BOM tự động & Khóa lạc quan (Inventory Deduction):** Trừ kho nguyên tử khi xác nhận đơn hàng, rollback khi thiếu kho, khóa lạc quan `[ConcurrencyCheck]`.
6. **Quyết toán Hóa đơn & Thuế VAT 8% (Billing & Statutory VAT):** Áp dụng thuế VAT 8% luật định, chiết khấu voucher, thanh toán đa phương thức (Tiền mặt, Thẻ, VietQR), in hóa đơn qua WPF `PrintDialog`.
7. **Trí tuệ Nhân tạo Cục bộ (Local GenAI via LM Studio):** 6 tính năng AI (tư vấn món, sinh mô tả ẩm thực, phân tích doanh thu, ma trận BCG, lưu lượng giờ cao điểm, dự báo nhập kho tuần) kết hợp Context Grounding triệt tiêu ảo giác ($< 1.8\%$).
8. **Quy trình CI/CD & Kiểm thử tự động:** 32 unit tests xUnit và pipeline GitHub Actions tự động hóa đo lường độ bao phủ nhánh $\ge 84.6\%$ (vượt Quality Gate 70%).

---

## 🏗️ Cấu Trúc Giải Pháp (Solution Architecture)

```
SmartRestaurant-Management-System/
├── .github/workflows/ci.yml         # GitHub Actions CI/CD Pipeline
├── SmartRestaurant.Core/             # Domain Entities, Enums, Interfaces, Invariants
├── SmartRestaurant.Data/             # EF Core 8 DbContext, Fluent API, Seed Data
├── SmartRestaurant.Services/         # Business Logic (Table, Order, BOM, Billing, KDS)
├── SmartRestaurant.AIEngine/         # Local GenAI Client, Context Grounding
├── SmartRestaurant.WPF/              # Touch UI, MVVM ViewModels, Commands, Converters
├── SmartRestaurant.Tests/            # xUnit Test Suite (32 Unit Tests)
├── SmartRestaurant.sln               # Solution master file
└── README.md                         # Project documentation
```

---

## 🚀 Hướng Dẫn Cài Đặt & Khởi Chạy

### Yêu cầu tiên quyết:
- **Hệ điều hành:** Windows 10/11 (64-bit).
- **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- **Hệ quản trị CSDL:** SQL Server 2022 (LocalDB hoặc Express) hoặc In-Memory Database cho môi trường kiểm thử.
- **Local AI Engine (Tùy chọn):** [LM Studio](https://lmstudio.ai/) với model `mistral-7b-instruct-v0.2.Q4_K_M.gguf` bật local server tại `http://localhost:1234`.

### Biên dịch & Khởi chạy:
```bash
# Clone repository
git clone https://github.com/gnutho1401/Smart-Restaurant-Management-System.git
cd Smart-Restaurant-Management-System

# Restore dependencies & Build solution
dotnet restore
dotnet build --configuration Release

# Chạy bộ kiểm thử tự động
dotnet test

# Chạy ứng dụng giao diện WPF
dotnet run --project SmartRestaurant.WPF/SmartRestaurant.WPF.csproj
```

