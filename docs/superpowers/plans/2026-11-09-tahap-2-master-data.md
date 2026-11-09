# Tahap 2 — Master Data Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mengimplementasikan seluruh modul Master Data (Pengguna, Role, Gudang & Lokasi, Kategori, Satuan, Barang, Pemasok, Pelanggan) dengan operasi CRUD end-to-end, validasi FluentValidation, pagination/sorting/filter server-side, unique index, soft delete, dan pembatasan multi-gudang sesuai `WMS_SPEC.md` dan `PROMPT_AI_WMS.md`.

**Architecture:** Clean Architecture modular monolith:
1. `Wms.Domain`: Entitas Master Data (`Warehouse`, `WarehouseLocation`, `Category`, `Unit`, `Product`, `Supplier`, `Customer`), enums, tracking method, soft delete interface.
2. `Wms.Infrastructure`: Konfigurasi EF Core (snake_case, unique indexes, RESTRICT on delete, global query filter per user warehouse & soft delete), migrasi EF Core, seeder master data demo.
3. `Wms.Application`: Service interfaces, DTOs, query filter models, validasi FluentValidation untuk setiap entitas master, server-side pagination dengan `PaginatedList<T>`.
4. `Wms.Web`: Blazor UI berbasis MudBlazor (MudTable server-side data, dialog Add/Edit, chip status aktif/nonaktif, konfirmasi nonaktifkan).
5. `Wms.Domain.Tests` & `Wms.Application.Tests`: Unit test validasi DTO, entity invariant, unique constraint logic, dan soft delete.

**Tech Stack:** .NET 8 LTS, C# 12, ASP.NET Core Blazor Web App (Interactive Server), MudBlazor, EF Core + Npgsql (PostgreSQL), FluentValidation, xUnit, FluentAssertions.

**Spec:** [WMS_SPEC.md](../../../WMS_SPEC.md) (Bagian 4, 7, 8, 17, 19, 20, 25, 28, 31) dan [PROMPT_AI_WMS.md](../../../PROMPT_AI_WMS.md) (Tahap 2).

## Global Constraints
- Target Framework: `net8.0`, nullable reference types aktif, no compiler warnings (`-warnaserror`).
- Tabel database `snake_case`, timestamp UTC (`timestamptz`), locale `id-ID`, timezone `Asia/Jakarta`.
- SKU dan barcode unik (case-insensitive SKU, partial unique index untuk barcode non-null).
- Kode gudang, lokasi (unik per gudang), kode pemasok, kode pelanggan unik.
- Tidak ada hard delete / hapus permanen: gunakan `is_active` soft delete jika data berelasi / memiliki referensi.
- Pembatasan gudang per pengguna: pengguna yang dibatasi ke gudang tertentu hanya dapat melihat data gudang tersebut melalui query filter atau authorization logic.
- UI: MudBlazor responsif, dialog konfirmasi untuk aksi destruktif/nonaktif, loading state, empty state, error feedback.

---

### Task 1: Domain Entities & Enums for Master Data

**Files:**
- Create: `src/Wms.Domain/Entities/Master/Warehouse.cs`
- Create: `src/Wms.Domain/Entities/Master/WarehouseLocation.cs`
- Create: `src/Wms.Domain/Entities/Master/Category.cs`
- Create: `src/Wms.Domain/Entities/Master/Unit.cs`
- Create: `src/Wms.Domain/Entities/Master/Product.cs`
- Create: `src/Wms.Domain/Entities/Master/Supplier.cs`
- Create: `src/Wms.Domain/Entities/Master/Customer.cs`
- Create: `src/Wms.Domain/Enums/TrackingMethod.cs`
- Test: `tests/Wms.Domain.Tests/Entities/MasterEntitiesTests.cs`

**Interfaces:**
- Produces: Entitas master data yang mewarisi `AuditableEntity` dan mengimplementasikan `ISoftDeletable`.

- [ ] **Step 1: Write failing unit test for master entity invariants**
- [ ] **Step 2: Implement TrackingMethod enum and master entity classes**
- [ ] **Step 3: Run unit tests to verify they pass**

---

### Task 2: Infrastructure EF Core Configuration, Global Query Filter & Migrations

**Files:**
- Modify: `src/Wms.Application/Common/Interfaces/IWmsDbContext.cs`
- Modify: `src/Wms.Infrastructure/Persistence/WmsDbContext.cs`
- Create: `src/Wms.Infrastructure/Persistence/Configurations/MasterEntityConfigurations.cs`
- Create: EF Core Migration `AddMasterDataTables`
- Modify: `src/Wms.Infrastructure/Persistence/DatabaseSeeder.cs`
- Test: `tests/Wms.Domain.Tests/Common/AuditableEntityTests.cs`

**Interfaces:**
- Consumes: Entitas `Wms.Domain.Entities.Master.*`, `IWmsDbContext`.
- Produces: `DbSet<Product>`, `DbSet<Warehouse>`, `DbSet<WarehouseLocation>`, `DbSet<Category>`, `DbSet<Unit>`, `DbSet<Supplier>`, `DbSet<Customer>`, tabel PostgreSQL `snake_case`, constraint unique index, FK restrict.

- [ ] **Step 1: Add master DbSets to `IWmsDbContext` and `WmsDbContext`**
- [ ] **Step 2: Configure entity mapping (snake_case, unique indexes, relationships, FK delete behavior RESTRICT, soft delete filter)**
- [ ] **Step 3: Generate EF Core Migration for Master Data**
- [ ] **Step 4: Update `DatabaseSeeder.cs` with realistic demo master data (Gudang Utama, Gudang Transit, Kategori Elektronik & Aksesoris, Satuan Pcs/Box, Supplier, Customer, Produk)**

---

### Task 3: Application DTOs, Validators, and CRUD Services for Master Data

**Files:**
- Create: `src/Wms.Application/Common/Models/QueryParameters.cs`
- Create: `src/Wms.Application/Features/Master/Warehouses/` (DTOs, Validator, `IWarehouseService`, `WarehouseService`)
- Create: `src/Wms.Application/Features/Master/Categories/` (DTOs, Validator, `ICategoryService`, `CategoryService`)
- Create: `src/Wms.Application/Features/Master/Units/` (DTOs, Validator, `IUnitService`, `UnitService`)
- Create: `src/Wms.Application/Features/Master/Products/` (DTOs, Validator, `IProductService`, `ProductService`)
- Create: `src/Wms.Application/Features/Master/Suppliers/` (DTOs, Validator, `ISupplierService`, `SupplierService`)
- Create: `src/Wms.Application/Features/Master/Customers/` (DTOs, Validator, `ICustomerService`, `CustomerService`)
- Create: `src/Wms.Application/Features/Admin/Users/` (DTOs, Validator, `IUserService`, `UserService`)
- Modify: `src/Wms.Application/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/Features/Master/ProductValidatorTests.cs`
- Test: `tests/Wms.Application.Tests/Features/Master/WarehouseValidatorTests.cs`

**Interfaces:**
- Consumes: `IWmsDbContext`, `IClock`, `IUserContext`.
- Produces: CRUD operations with server-side filter, search, sort, pagination (`PaginatedList<T>`), uniqueness check, and soft-delete toggle.

- [ ] **Step 1: Write failing validator tests (duplicate SKU, invalid email/phone, negative min/max stock)**
- [ ] **Step 2: Implement Master DTOs and FluentValidation rules**
- [ ] **Step 3: Implement CRUD services with server-side pagination, search, sort, and soft-delete**
- [ ] **Step 4: Register services in `DependencyInjection.cs`**
- [ ] **Step 5: Run tests and verify they pass**

---

### Task 4: Master Data Blazor UI (MudBlazor Tables & CRUD Dialogs)

**Files:**
- Create: `src/Wms.Web/Components/Pages/Master/Products.razor`
- Create: `src/Wms.Web/Components/Pages/Master/ProductDialog.razor`
- Create: `src/Wms.Web/Components/Pages/Master/Categories.razor`
- Create: `src/Wms.Web/Components/Pages/Master/Units.razor`
- Create: `src/Wms.Web/Components/Pages/Master/Warehouses.razor`
- Create: `src/Wms.Web/Components/Pages/Master/WarehouseDialog.razor`
- Create: `src/Wms.Web/Components/Pages/Master/WarehouseLocationsDialog.razor`
- Create: `src/Wms.Web/Components/Pages/Master/Suppliers.razor`
- Create: `src/Wms.Web/Components/Pages/Master/Customers.razor`
- Create: `src/Wms.Web/Components/Pages/Admin/Users.razor`
- Create: `src/Wms.Web/Components/Pages/Admin/UserDialog.razor`
- Modify: `src/Wms.Web/Components/Layout/NavMenu.razor`
- Modify: `src/Wms.Web/Program.cs`

**Interfaces:**
- Consumes: Master CRUD services from `Wms.Application`.
- Produces: Halaman UI lengkap untuk Data Barang, Kategori, Satuan, Gudang & Rak, Pemasok, Pelanggan, dan Manajemen Pengguna.

- [ ] **Step 1: Implement Category and Unit management pages (inline/dialog add/edit, active toggle)**
- [ ] **Step 2: Implement Warehouse & Location management (warehouse card/table, location list inside warehouse dialog)**
- [ ] **Step 3: Implement Supplier & Customer management pages**
- [ ] **Step 4: Implement Product management page with server search, category filter, tracking method, min stock alerts**
- [ ] **Step 5: Implement User & Role management page (assign role, warehouse access restriction, password reset/lockout toggle)**
- [ ] **Step 6: Update `NavMenu.razor` to wire all routes and authorization checks**

---

### Task 5: Build, Test, Format, and Gate Verification

**Files:**
- Run verification suite: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`
- Update: `docs/decisions.md` (record Master Data indexing, warehouse restriction, and soft delete decisions)

- [ ] **Step 1: Run `dotnet build --no-restore -warnaserror`**
- [ ] **Step 2: Run all unit and integration tests**
- [ ] **Step 3: Run `dotnet format`**
- [ ] **Step 4: Generate Tahap 2 Completion Report according to `PROMPT_AI_WMS.md` format**
