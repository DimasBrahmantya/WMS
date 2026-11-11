# Tahap 6 — Notifikasi dan Ekspor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mengimplementasikan seluruh sistem Notifikasi In-App dengan Deduplikasi, Background Job (Quartz.NET), Ekspor Multi-Format (CSV, XLSX, PDF) langsung maupun via job latar belakang untuk data besar, penyimpanan terisolasi dengan auto-cleanup, serta audit log untuk setiap ekspor dan anomali rekonsiliasi.

**Architecture:** Clean Architecture Modular Monolith: Domain entities (`AppNotification`, `ExportJob`), Application contracts/DTOs (`INotificationService`, `IExportService`), Infrastructure implementations (Quartz.NET hosted jobs, `CsvHelper`, `ClosedXML`, `QuestPDF`, `LocalExportFileStorage`, EF Core DbContext), dan Blazor UI components (`NotificationBell`, `NotificationsIndex`, `ExportDialog`, `ExportsDownloadController`).

**Tech Stack:** .NET 8, C# 12, EF Core 8 (PostgreSQL), Quartz.NET, CsvHelper, ClosedXML, QuestPDF, MudBlazor, xUnit, FluentAssertions, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-11-11-tahap-6-notifikasi-dan-ekspor-design.md`

## Global Constraints
- Framework: .NET 8.0.
- Database: PostgreSQL (snake_case via EF Core Npgsql).
- Background Job: Quartz.NET dengan `AddQuartz` dan `AddQuartzHostedService`.
- Notification Deduplication: Maksimum 1 notifikasi aktif per `DedupKey`, auto-resolve saat kondisi pulih.
- Ekspor: Dual-mode (streaming langsung $\le 1.000$ baris, background job $> 1.000$ baris atau manual).
- QuestPDF License: Community License `QuestPDF.Settings.License = LicenseType.Community;`.
- Audit Log: Semua aktivitas ekspor dicatat ke `IAuditLogService`.

---

### Task 1: NuGet Packages, Domain Entities, Enums, & EF Core Mapping

**Files:**
- Modify: `src/Wms.Infrastructure/Wms.Infrastructure.csproj`
- Create: `src/Wms.Domain/Enums/NotificationCategory.cs`
- Create: `src/Wms.Domain/Enums/NotificationStatus.cs`
- Create: `src/Wms.Domain/Enums/ExportFormat.cs`
- Create: `src/Wms.Domain/Enums/ExportJobStatus.cs`
- Create: `src/Wms.Domain/Entities/Notifications/AppNotification.cs`
- Create: `src/Wms.Domain/Entities/Exports/ExportJob.cs`
- Create: `src/Wms.Infrastructure/Persistence/Configurations/NotificationAndExportConfigurations.cs`
- Modify: `src/Wms.Infrastructure/Persistence/IWmsDbContext.cs`
- Modify: `src/Wms.Infrastructure/Persistence/WmsDbContext.cs`
- Test: `tests/Wms.Domain.Tests/Entities/NotificationAndExportEntitiesTests.cs`

- [ ] **Step 1: Write domain entity tests**
- [ ] **Step 2: Add packages to `Wms.Infrastructure.csproj` (Quartz, CsvHelper, ClosedXML, QuestPDF)**
- [ ] **Step 3: Create enums and domain entities in `Wms.Domain`**
- [ ] **Step 4: Configure EF Core mapping in `Wms.Infrastructure` and register DbSets**
- [ ] **Step 5: Run tests to verify domain and mapping compile cleanly**

---

### Task 2: In-App Notification Service with Deduplication & Auto-Resolution

**Files:**
- Create: `src/Wms.Application/Features/Notifications/INotificationService.cs`
- Create: `src/Wms.Application/Features/Notifications/NotificationDtos.cs`
- Create: `src/Wms.Infrastructure/Services/Notifications/NotificationService.cs`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/Notifications/NotificationServiceTests.cs`

- [ ] **Step 1: Write unit tests for notification creation, deduplication, and resolution**
- [ ] **Step 2: Define `INotificationService` interface and DTOs in `Wms.Application`**
- [ ] **Step 3: Implement `NotificationService` in `Wms.Infrastructure`**
- [ ] **Step 4: Register service in DI**
- [ ] **Step 5: Run tests and ensure 100% pass**

---

### Task 3: Multi-Format Export Engine (CSV, XLSX, PDF) & Audit Integration

**Files:**
- Create: `src/Wms.Application/Features/Exports/IExportService.cs`
- Create: `src/Wms.Application/Features/Exports/ExportDtos.cs`
- Create: `src/Wms.Infrastructure/Services/Exports/Formatters/CsvReportFormatter.cs`
- Create: `src/Wms.Infrastructure/Services/Exports/Formatters/ExcelReportFormatter.cs`
- Create: `src/Wms.Infrastructure/Services/Exports/Formatters/PdfReportFormatter.cs`
- Create: `src/Wms.Infrastructure/Services/Exports/FileStorage/LocalExportFileStorage.cs`
- Create: `src/Wms.Infrastructure/Services/Exports/ExportService.cs`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/Exports/ExportFormattersTests.cs`

- [ ] **Step 1: Write unit tests for CSV, Excel, and PDF formatters**
- [ ] **Step 2: Implement formatters using CsvHelper, ClosedXML, and QuestPDF**
- [ ] **Step 3: Implement `LocalExportFileStorage` (App_Data/Exports) with retention check**
- [ ] **Step 4: Implement `ExportService` supporting direct memory stream & job queuing**
- [ ] **Step 5: Integrate `IAuditLogService` on every export operation**
- [ ] **Step 6: Run tests and verify formatting outputs**

---

### Task 4: Quartz.NET Background Jobs

**Files:**
- Create: `src/Wms.Infrastructure/BackgroundJobs/StockAlertJob.cs`
- Create: `src/Wms.Infrastructure/BackgroundJobs/OverduePurchaseOrderJob.cs`
- Create: `src/Wms.Infrastructure/BackgroundJobs/ReconciliationJob.cs`
- Create: `src/Wms.Infrastructure/BackgroundJobs/BackgroundExportWorkerJob.cs`
- Create: `src/Wms.Infrastructure/BackgroundJobs/CleanupExportFilesJob.cs`
- Modify: `src/Wms.Infrastructure/DependencyInjection.cs`
- Test: `tests/Wms.Application.Tests/BackgroundJobs/BackgroundJobsLogicTests.cs`

- [ ] **Step 1: Write unit tests for background job execution logic**
- [ ] **Step 2: Implement `StockAlertJob` (scans low/out of stock, generates deduplicated notifications, auto-resolves when restocked)**
- [ ] **Step 3: Implement `OverduePurchaseOrderJob` (scans delayed POs)**
- [ ] **Step 4: Implement `ReconciliationJob` (verifies balance vs movements sum, triggers alerts & audit log)**
- [ ] **Step 5: Implement `BackgroundExportWorkerJob` & `CleanupExportFilesJob`**
- [ ] **Step 6: Configure Quartz.NET schedules in `DependencyInjection.cs`**
- [ ] **Step 7: Run tests to verify job logic**

---

### Task 5: Web UI & Controllers

**Files:**
- Create: `src/Wms.Web/Controllers/ExportsDownloadController.cs`
- Create: `src/Wms.Web/Components/Layout/NotificationBell.razor`
- Modify: `src/Wms.Web/Components/Layout/MainLayout.razor`
- Modify: `src/Wms.Web/Components/Layout/NavMenu.razor`
- Create: `src/Wms.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- Create: `src/Wms.Web/Components/Pages/Reports/Dialogs/ExportDialog.razor`
- Modify: `src/Wms.Web/Components/Pages/Reports/StockReport.razor`
- Modify: `src/Wms.Web/Components/Pages/Reports/StockCardReport.razor`
- Modify: `src/Wms.Web/Components/Pages/Reports/ValuationReport.razor`
- Modify: `src/Wms.Web/Program.cs`

- [ ] **Step 1: Implement `ExportsDownloadController` with secure authorization and audit log**
- [ ] **Step 2: Create `NotificationBell` with unread badge and dropdown menu**
- [ ] **Step 3: Integrate bell into `MainLayout.razor` and update `NavMenu.razor` (link to `/notifications`)**
- [ ] **Step 4: Create `/notifications` page with filtering, pagination, and mark-as-read**
- [ ] **Step 5: Create `ExportDialog` component and integrate into Stock, Stock Card, and Valuation reports**
- [ ] **Step 6: Map controllers in `Program.cs` and test UI build**

---

### Task 6: Integration Tests & Mandatory Checks

**Files:**
- Create: `tests/Wms.Integration.Tests/Notifications/NotificationIntegrationTests.cs`
- Create: `tests/Wms.Integration.Tests/Exports/ExportIntegrationTests.cs`
- Create: `tests/Wms.Integration.Tests/BackgroundJobs/ReconciliationJobTests.cs`

- [ ] **Step 1: Write integration tests for notification deduplication with real DB**
- [ ] **Step 2: Write integration tests for reconciliation anomaly detection & audit log**
- [ ] **Step 3: Write integration tests for export generation & download endpoint**
- [ ] **Step 4: Run all mandatory checks:**
  - `dotnet restore`
  - `dotnet build --no-restore -warnaserror`
  - `dotnet test --no-build`
  - `dotnet format --verify-no-changes`
- [ ] **Step 5: Provide comprehensive final report matching required checklist**
