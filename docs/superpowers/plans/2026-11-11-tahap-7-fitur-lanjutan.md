# Tahap 7 — Fitur Lanjutan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mengimplementasikan Batch/Lot tracking + Kedaluwarsa + FEFO, Barcode/QR generator dan scanner helper dengan fallback manual, Impor CSV/XLSX dengan preview & validasi baris-demi-baris, Putaway & Picking, Sales Order + Soft Reservation atomik, REST API Eksternal dengan API Key, dan Backup/Export data sistem.

**Architecture:** Clean Architecture Modular Monolith: Domain entities (`Batch`, `SalesOrder`, `SalesOrderLine`, `ApiKey`), Application contracts/DTOs (`IBatchService`, `IFefoService`, `IBarcodeService`, `IDataImportService`, `IPutawayService`, `IPickingService`, `ISalesOrderService`, `IApiKeyService`, `ISystemBackupService`), Infrastructure implementations (ZXing.Net, EF Core DbContext, transaction handling, CsvHelper/ClosedXML import), dan Blazor UI components + REST API Controllers.

**Tech Stack:** .NET 8, C# 12, EF Core 8 (PostgreSQL), ZXing.Net, CsvHelper, ClosedXML, MudBlazor, xUnit, FluentAssertions, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-11-11-tahap-7-fitur-lanjutan-design.md`

## Global Constraints
- Framework: .NET 8.0, C# 12.
- Database: PostgreSQL via EF Core (snake_case naming convention).
- Barcode/QR: Menggunakan `ZXing.Net` untuk generate SVG / PNG label. Tidak mengklaim dukungan scanner fisik sebelum diuji (gunakan input keyboard-wedge dengan fallback manual).
- Stock & Reservation: Reservasi stok (`Reserved`) memotong `Available` pada `InventoryBalance`. Transaksi mutasi atomik, idempotency, dan audit log lengkap.
- Expired Check: Batch kedaluwarsa diblokir dari pengeluaran biasa (`StockOut`, `SalesOrder`).
- REST API: Autentikasi via `X-API-Key` dengan SHA-256 hash di database.
- Impor Data: Wizard 3-step (Upload -> Preview/Validasi per baris -> Commit Transaksional).

---

### Task 1: Domain Entities, Enums, EF Core Mapping & DbSets

**Files:**
- Create: `src/Wms.Domain/Entities/Inventory/Batch.cs`
- Create: `src/Wms.Domain/Entities/Orders/SalesOrder.cs`
- Create: `src/Wms.Domain/Entities/Orders/SalesOrderLine.cs`
- Create: `src/Wms.Domain/Entities/System/ApiKey.cs`
- Create: `src/Wms.Domain/Enums/SalesOrderStatus.cs`
- Create: `src/Wms.Domain/Enums/ApiKeyScope.cs`
- Create: `src/Wms.Infrastructure/Persistence/Configurations/BatchConfiguration.cs`
- Create: `src/Wms.Infrastructure/Persistence/Configurations/SalesOrderConfiguration.cs`
- Create: `src/Wms.Infrastructure/Persistence/Configurations/ApiKeyConfiguration.cs`
- Modify: `src/Wms.Infrastructure/Persistence/IWmsDbContext.cs`
- Modify: `src/Wms.Infrastructure/Persistence/WmsDbContext.cs`
- Modify: `src/Wms.Domain/Authorization/Permissions.cs`
- Test: `tests/Wms.Domain.Tests/Entities/Tahap7EntitiesTests.cs`

- [ ] **Step 1: Write unit tests for domain entities creation and constraints**
- [ ] **Step 2: Create domain entities (`Batch`, `SalesOrder`, `SalesOrderLine`, `ApiKey`) and enums**
- [ ] **Step 3: Update `Permissions.cs` with new Tahap 7 permissions**
- [ ] **Step 4: Create EF Core entity configurations and register DbSets in `IWmsDbContext` & `WmsDbContext`**
- [ ] **Step 5: Run tests and ensure solution builds cleanly (`-nr:false -m:1`)**

---

### Task 2: Batch, Expiry & FEFO Allocation Service

**Files:**
- Create: `src/Wms.Application/Features/Batches/IBatchService.cs`
- Create: `src/Wms.Application/Features/Batches/IFefoService.cs`
- Create: `src/Wms.Application/Features/Batches/BatchDtos.cs`
- Create: `src/Wms.Infrastructure/Services/Batches/BatchService.cs`
- Create: `src/Wms.Infrastructure/Services/Batches/FefoService.cs`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Modify: `src/Wms.Infrastructure/Services/Stock/StockPostingService.cs` (tambahkan pengecekan expired batch blocking)
- Test: `tests/Wms.Application.Tests/Batches/FefoServiceTests.cs`

- [ ] **Step 1: Write unit tests for FEFO sorting and expired batch blocking**
- [ ] **Step 2: Define `IBatchService`, `IFefoService`, and DTOs in `Wms.Application`**
- [ ] **Step 3: Implement `BatchService` and `FefoService` in `Wms.Infrastructure`**
- [ ] **Step 4: Integrate expired batch blocking into `StockPostingService.cs`**
- [ ] **Step 5: Register services in DI and verify tests pass**

---

### Task 3: Barcode & QR Code Engine + UI Scanner Input Component

**Files:**
- Modify: `src/Wms.Infrastructure/Wms.Infrastructure.csproj` (tambahkan paket `ZXing.Net`)
- Create: `src/Wms.Application/Features/Barcode/IBarcodeService.cs`
- Create: `src/Wms.Application/Features/Barcode/BarcodeDtos.cs`
- Create: `src/Wms.Infrastructure/Services/Barcode/ZxingBarcodeService.cs`
- Create: `src/Wms.Web/Components/Common/BarcodeScanInput.razor`
- Create: `src/Wms.Web/Components/Common/BarcodeDisplayDialog.razor`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/Barcode/BarcodeServiceTests.cs`

- [ ] **Step 1: Write unit tests for Barcode & QR code generation**
- [ ] **Step 2: Add `ZXing.Net` and `ZXing.Net.Bindings.ImageSharp.V2` or SkiaSharp/SVG writer to `Wms.Infrastructure.csproj`**
- [ ] **Step 3: Implement `ZxingBarcodeService` (output format SVG Data-URI)**
- [ ] **Step 4: Create `BarcodeScanInput.razor` (keyboard-wedge input, search fallback dialog, product info preview)**
- [ ] **Step 5: Create `BarcodeDisplayDialog.razor` (modal print label)**
- [ ] **Step 6: Run tests and verify SVG rendering**

---

### Task 4: Putaway & Picking Subsystem

**Files:**
- Create: `src/Wms.Application/Features/WarehouseOperations/IPutawayService.cs`
- Create: `src/Wms.Application/Features/WarehouseOperations/IPickingService.cs`
- Create: `src/Wms.Application/Features/WarehouseOperations/WarehouseOperationDtos.cs`
- Create: `src/Wms.Infrastructure/Services/WarehouseOperations/PutawayService.cs`
- Create: `src/Wms.Infrastructure/Services/WarehouseOperations/PickingService.cs`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/WarehouseOperations/PutawayAndPickingTests.cs`

- [ ] **Step 1: Write unit tests for putaway suggestion algorithm and picking list generation**
- [ ] **Step 2: Define interfaces and DTOs in `Wms.Application`**
- [ ] **Step 3: Implement `PutawayService` (prioritas: rak produk sama -> zona kategori -> rak kosong)**
- [ ] **Step 4: Implement `PickingService` (alokasi batch FEFO dan rute urut rak lokasi)**
- [ ] **Step 5: Register in DI and run tests**

---

### Task 5: Sales Order & Atomic Soft Reservation Workflow

**Files:**
- Create: `src/Wms.Application/Features/SalesOrders/ISalesOrderService.cs`
- Create: `src/Wms.Application/Features/SalesOrders/SalesOrderDtos.cs`
- Create: `src/Wms.Infrastructure/Services/SalesOrders/SalesOrderService.cs`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/SalesOrders/SalesOrderServiceTests.cs`

- [ ] **Step 1: Write unit tests for Sales Order lifecycle (Draft -> Confirmed [Reserve] -> Picking -> Dispatched [Deduct] -> Cancelled [Release])**
- [ ] **Step 2: Implement `ISalesOrderService` interface and DTOs**
- [ ] **Step 3: Implement `SalesOrderService` with atomic reservation & ledger posting on dispatch**
- [ ] **Step 4: Register in DI and run tests**

---

### Task 6: CSV/XLSX Import Engine with Preview & Row-by-Row Validation

**Files:**
- Create: `src/Wms.Application/Features/Imports/IDataImportService.cs`
- Create: `src/Wms.Application/Features/Imports/ImportDtos.cs`
- Create: `src/Wms.Infrastructure/Services/Imports/DataImportService.cs`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/Imports/DataImportServiceTests.cs`

- [ ] **Step 1: Write unit tests for CSV/XLSX parsing, format validation, and duplicate detection**
- [ ] **Step 2: Define `IDataImportService` with separate validation and commit steps**
- [ ] **Step 3: Implement `DataImportService` using `CsvHelper` and `ClosedXML`**
- [ ] **Step 4: Add transactional commit with `IAuditLogService` recording**
- [ ] **Step 5: Register in DI and run tests**

---

### Task 7: External REST API & API Key Security

**Files:**
- Create: `src/Wms.Application/Features/ApiKeys/IApiKeyService.cs`
- Create: `src/Wms.Application/Features/ApiKeys/ApiKeyDtos.cs`
- Create: `src/Wms.Infrastructure/Services/ApiKeys/ApiKeyService.cs`
- Create: `src/Wms.Web/Authentication/ApiKeyAuthenticationHandler.cs`
- Create: `src/Wms.Web/Controllers/Api/v1/ProductsApiController.cs`
- Create: `src/Wms.Web/Controllers/Api/v1/InventoryApiController.cs`
- Create: `src/Wms.Web/Controllers/Api/v1/SalesOrdersApiController.cs`
- Modify: `src/Wms.Web/Program.cs`
- Test: `tests/Wms.Application.Tests/ApiKeys/ApiKeyServiceTests.cs`

- [ ] **Step 1: Write unit tests for API Key creation, validation, and SHA-256 hashing**
- [ ] **Step 2: Implement `IApiKeyService` and `ApiKeyService`**
- [ ] **Step 3: Implement `ApiKeyAuthenticationHandler` for `X-API-Key` authentication scheme**
- [ ] **Step 4: Implement REST API v1 controllers (`ProductsApiController`, `InventoryApiController`, `SalesOrdersApiController`)**
- [ ] **Step 5: Configure authentication and endpoints in `Program.cs` and verify tests pass**

---

### Task 8: System Backup & Data Export Service

**Files:**
- Create: `src/Wms.Application/Features/Backup/ISystemBackupService.cs`
- Create: `src/Wms.Application/Features/Backup/BackupDtos.cs`
- Create: `src/Wms.Infrastructure/Services/Backup/SystemBackupService.cs`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/Backup/SystemBackupServiceTests.cs`

- [ ] **Step 1: Write unit tests for full data snapshot export and checksum computation**
- [ ] **Step 2: Define `ISystemBackupService` and DTOs**
- [ ] **Step 3: Implement `SystemBackupService` (JSON archive of master, config, and operational data)**
- [ ] **Step 4: Register in DI and verify tests pass**

---

### Task 9: Blazor UI Integration

**Files:**
- Create: `src/Wms.Web/Components/Pages/SalesOrders/SalesOrdersIndex.razor`
- Create: `src/Wms.Web/Components/Pages/SalesOrders/SalesOrderDetail.razor`
- Create: `src/Wms.Web/Components/Pages/SalesOrders/PickingListDialog.razor`
- Create: `src/Wms.Web/Components/Pages/Batches/BatchesIndex.razor`
- Create: `src/Wms.Web/Components/Pages/Imports/ImportWizard.razor`
- Create: `src/Wms.Web/Components/Pages/Settings/ApiKeysManagement.razor`
- Create: `src/Wms.Web/Components/Pages/Settings/SystemBackupDialog.razor`
- Modify: `src/Wms.Web/Components/Layout/NavMenu.razor`

- [ ] **Step 1: Create `BatchesIndex.razor` with near-expiry alerts and batch blocking toggle**
- [ ] **Step 2: Create `SalesOrdersIndex.razor` and `SalesOrderDetail.razor` with confirmation, reservation badges, and action buttons**
- [ ] **Step 3: Create `PickingListDialog.razor` displaying route and FEFO recommended batches**
- [ ] **Step 4: Create `ImportWizard.razor` (3-step wizard: Upload, Preview errors table, Commit)**
- [ ] **Step 5: Create `ApiKeysManagement.razor` and `SystemBackupDialog.razor` in Settings**
- [ ] **Step 6: Update `NavMenu.razor` with links to Sales Orders, Batches, and Data Import**

---

### Task 10: Integration Tests & Mandatory Checks

**Files:**
- Create: `tests/Wms.Integration.Tests/SalesOrders/SalesOrderIntegrationTests.cs`
- Create: `tests/Wms.Integration.Tests/Api/ApiKeyApiIntegrationTests.cs`
- Create: `tests/Wms.Integration.Tests/Imports/DataImportIntegrationTests.cs`
- Create: `tests/Wms.Integration.Tests/Backup/SystemBackupIntegrationTests.cs`

- [ ] **Step 1: Run integration test for Sales Order soft reservation & dispatch ledger posting**
- [ ] **Step 2: Run integration test for REST API with API Key authentication**
- [ ] **Step 3: Run integration test for Data Import transactional rollback on failure**
- [ ] **Step 4: Run integration test for System Backup integrity**
- [ ] **Step 5: Execute mandatory quality checks:**
  - `dotnet restore`
  - `dotnet build --no-restore -warnaserror -nr:false -m:1`
  - `dotnet test --no-build`
  - `dotnet format --verify-no-changes`
- [ ] **Step 6: Provide comprehensive report to user and await product owner gate approval**

