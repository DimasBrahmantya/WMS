# Tahap 5 — Workflow Operasional Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mengimplementasikan seluruh modul Workflow Operasional (Purchase Order + approval + penerimaan parsial, Transfer Antar-Gudang dengan status `in_transit`, Stock Opname dengan snapshot/recount/selisih nilai/approval/posting, Retur Barang ke pemasok dan dari pelanggan, Barang Hilang/Rusak/Kedaluwarsa/Karantina, Pemisahan Tugas / Separation of Duties, dan Audit Trail menyeluruh) sesuai dengan `WMS_SPEC.md` (bagian 4, 9, 10, 11, 12, 13, 14, 15, 18, 26) dan `PROMPT_AI_WMS.md` (Tahap 5).

**Architecture:** Clean Architecture modular monolith:
1. `Wms.Domain`:
   - Enums: `PurchaseOrderStatus`, `StockTransferStatus`, `StockOpnameStatus`, `StockIncidentStatus`, `StockIncidentCategory`, `StockReturnType`, `StockReturnStatus`. Penambahan `MovementType` (`PurchaseReturn`, `CustomerReturn`, `StockIncident`) dan `SourceDocumentType` (`PurchaseOrder`, `StockReturn`, `StockOpname`).
   - Entities:
     - `PurchaseOrder`, `PurchaseOrderLine`
     - `StockTransfer`, `StockTransferLine`
     - `StockOpnameSession`, `StockOpnameLine`
     - `StockReturn`, `StockReturnLine`
     - `StockIncident`, `StockIncidentLine`
2. `Wms.Infrastructure`:
   - Konfigurasi EF Core mapping (PostgreSQL `snake_case`, unique document numbering, foreign keys, cascade delete on lines, precision `numeric(18,4)`).
   - Registrasi di `IWmsDbContext` dan `WmsDbContext`.
   - Update `DocumentSequenceService` untuk prefix `PO-`, `TRF-`, `OPN-`, `RET-`, `INC-`.
   - Implementasi Application Services:
     - `PurchaseOrderService` (termasuk workflow approval, validation, auto-status, dan penerimaan parsial via `GoodsReceipt`)
     - `StockTransferService` (pengiriman `in_transit` memotong stok asal, penerimaan di gudang tujuan menambah stok & menghitung ulang moving average)
     - `StockOpnameService` (snapshot cut-off saldo & moving average, input count & recount, kalkulasi selisih qty & nilai, approval, posting atomik ke ledger `StockAdjustment`)
     - `StockReturnService` (retur ke pemasok mengurangi stok, retur pelanggan menambah stok ke `Available` atau `Damaged`/`Quarantine`)
     - `StockIncidentService` (laporan hilang/rusak/expired, investigasi, approval, posting pengurangan stok hilang atau reposisi status stok ke Damaged/Quarantine)
   - Penegakan aturan **Pemisahan Tugas (Separation of Duties)**:
     `if (document.CreatedByUserId == currentUserId) return Result.Failure("Pembuat dokumen tidak diizinkan menyetujui dokumen buatannya sendiri (Pemisahan Tugas).");`
   - Integrasi `IAuditLogService` pada setiap operasi create, submit approval, approve, reject, cancel, receive, dan post.
3. `Wms.Application`:
   - DTOs, query filter models, FluentValidation validators untuk kelima workflow.
   - Contract interfaces untuk masing-masing service.
4. `Wms.Web`:
   - Pembaruan `NavMenu.razor` mengaktifkan menu operasional:
     - `/operations/purchase-orders` (Purchase Order)
     - `/operations/transfers` (Transfer Antar-Gudang)
     - `/operations/opname` (Stock Opname)
     - `/operations/incidents` (Barang Hilang & Rusak)
     - `/operations/returns` (Retur Barang)
   - Komponen UI MudBlazor responsif untuk masing-masing modul: tabel server-side, modal tambah/detail/recount/receive, chip status berwarna, dialog konfirmasi aksi approval/posting, dan pesan feedback validasi SoD.
5. `Wms.Integration.Tests` & `Wms.Domain.Tests`:
   - Pengujian terisolasi untuk SoD (creator vs approver).
   - Pengujian penerimaan parsial PO dan sisa kuantitas.
   - Pengujian transfer antar-gudang (status `in_transit` dan penambahan di tujuan).
   - Pengujian stock opname (snapshot, selisih kuantitas & nilai, posting ledger).
   - Pengujian retur dan incident (hilang vs rusak).
   - Verifikasi pencatatan audit log untuk setiap aksi approval dan posting.

---

## Breakdown Task Rinci

### Task 1: Domain Enums & Entities for Tahap 5 Workflows
**Files:**
- Create: `src/Wms.Domain/Enums/PurchaseOrderStatus.cs`
- Create: `src/Wms.Domain/Enums/StockTransferStatus.cs`
- Create: `src/Wms.Domain/Enums/StockOpnameStatus.cs`
- Create: `src/Wms.Domain/Enums/StockIncidentStatus.cs`
- Create: `src/Wms.Domain/Enums/StockIncidentCategory.cs`
- Create: `src/Wms.Domain/Enums/StockReturnType.cs`
- Create: `src/Wms.Domain/Enums/StockReturnStatus.cs`
- Modify: `src/Wms.Domain/Enums/MovementType.cs` (tambah PurchaseReturn, CustomerReturn, StockIncident)
- Modify: `src/Wms.Domain/Enums/SourceDocumentType.cs` (tambah PurchaseOrder, StockReturn, StockOpname)
- Create: `src/Wms.Domain/Entities/Transactions/PurchaseOrder.cs` & `PurchaseOrderLine.cs`
- Create: `src/Wms.Domain/Entities/Transactions/StockTransfer.cs` & `StockTransferLine.cs`
- Create: `src/Wms.Domain/Entities/Transactions/StockOpnameSession.cs` & `StockOpnameLine.cs`
- Create: `src/Wms.Domain/Entities/Transactions/StockReturn.cs` & `StockReturnLine.cs`
- Create: `src/Wms.Domain/Entities/Transactions/StockIncident.cs` & `StockIncidentLine.cs`
- Modify: `src/Wms.Domain/Entities/Transactions/GoodsReceipt.cs` & `GoodsReceiptLine.cs` (tambah opsional `PurchaseOrderId`, `PurchaseOrderLineId`)
- Test: `tests/Wms.Domain.Tests/Entities/OperationalWorkflowEntitiesTests.cs`

- [ ] **Step 1: Write unit tests verifying operational domain entities invariants and initial states**
- [ ] **Step 2: Implement domain enums and entity classes**
- [ ] **Step 3: Run unit tests to confirm domain entities pass**

---

### Task 2: Infrastructure EF Core Configuration, DbContext & Migrations
**Files:**
- Modify: `src/Wms.Application/Common/Interfaces/IWmsDbContext.cs`
- Modify: `src/Wms.Infrastructure/Persistence/WmsDbContext.cs`
- Create: `src/Wms.Infrastructure/Persistence/Configurations/OperationalWorkflowConfigurations.cs`
- Update document sequences in `DocumentSequenceService.cs` (`PO`, `TRF`, `OPN`, `RET`, `INC`)

- [ ] **Step 1: Add new DbSets to `IWmsDbContext` and `WmsDbContext`**
- [ ] **Step 2: Configure entity mapping (snake_case, primary keys, relationships, numeric precision, decimal 18,4)**
- [ ] **Step 3: Update `DocumentSequenceService` with sequence types for Tahap 5**
- [ ] **Step 4: Verify build succeeds**

---

### Task 3: Separation of Duty (SoD) Guard & Audit Log Integration
**Files:**
- Create: `src/Wms.Application/Common/Exceptions/SeparationOfDutiesException.cs` (or standard validation guard)
- Create helper / rule: `SeparationOfDutiesGuard` untuk memvalidasi `CreatedByUserId != CurrentUserId` pada seluruh aksi approval
- Verify integration with `IAuditLogService`

- [ ] **Step 1: Write unit tests verifying SoD logic rejects self-approval and allows distinct approver**
- [ ] **Step 2: Implement reusable SoD guard and audit log helper for approvals and postings**

---

### Task 4: Purchase Order Module (PO, Approval, Partial Receipt)
**Files:**
- Create: `src/Wms.Application/Features/Operations/PurchaseOrders/PurchaseOrderDtos.cs`
- Create: `src/Wms.Application/Features/Operations/PurchaseOrders/PurchaseOrderValidators.cs`
- Create: `src/Wms.Application/Features/Operations/PurchaseOrders/IPurchaseOrderService.cs`
- Create: `src/Wms.Infrastructure/Services/Operations/PurchaseOrderService.cs`
- Modify: `src/Wms.Infrastructure/Services/Stock/GoodsReceiptService.cs` (update remaining PO qty & trigger `PartiallyReceived` / `Received`)
- Modify: `src/Wms.Infrastructure/Services/Stock/StockPostingService.cs` (sync PO line received qty on GR posting and rollback on reversal)
- Test: `tests/Wms.Integration.Tests/Operations/PurchaseOrderTests.cs`

- [ ] **Step 1: Write integration tests for PO creation, SoD approval validation, and partial receipt calculations**
- [ ] **Step 2: Implement DTOs, FluentValidation validators, and `IPurchaseOrderService`**
- [ ] **Step 3: Implement `PurchaseOrderService` with status transitions, SoD check, and PO-GR linking**
- [ ] **Step 4: Connect `StockPostingService` to update PO line remaining and received quantities atomik**
- [ ] **Step 5: Run tests and verify PO workflow passes**

---

### Task 5: Inter-Warehouse Stock Transfer Module (Status In-Transit & Receipt)
**Files:**
- Create: `src/Wms.Application/Features/Operations/Transfers/StockTransferDtos.cs`
- Create: `src/Wms.Application/Features/Operations/Transfers/StockTransferValidators.cs`
- Create: `src/Wms.Application/Features/Operations/Transfers/IStockTransferService.cs`
- Create: `src/Wms.Infrastructure/Services/Operations/StockTransferService.cs`
- Test: `tests/Wms.Integration.Tests/Operations/StockTransferTests.cs`

- [ ] **Step 1: Write integration tests for Stock Transfer (dispatch out to `InTransit`, receipt at destination, SoD on approval)**
- [ ] **Step 2: Implement DTOs, FluentValidation validators, and `IStockTransferService`**
- [ ] **Step 3: Implement `StockTransferService` (handling `TransferOut` ledger movement, `InTransit` tracking, and `TransferIn` destination valuation)**
- [ ] **Step 4: Run tests and verify Transfer workflow passes**

---

### Task 6: Stock Opname Module (Snapshot, Recount, Variance, Approval, Posting)
**Files:**
- Create: `src/Wms.Application/Features/Operations/Opname/StockOpnameDtos.cs`
- Create: `src/Wms.Application/Features/Operations/Opname/StockOpnameValidators.cs`
- Create: `src/Wms.Application/Features/Operations/Opname/IStockOpnameService.cs`
- Create: `src/Wms.Infrastructure/Services/Operations/StockOpnameService.cs`
- Test: `tests/Wms.Integration.Tests/Operations/StockOpnameTests.cs`

- [ ] **Step 1: Write integration tests for Stock Opname (snapshot cut-off, variance qty & value calculation, SoD approval, posting stock adjustment)**
- [ ] **Step 2: Implement DTOs, FluentValidation validators, and `IStockOpnameService`**
- [ ] **Step 3: Implement `StockOpnameService` with snapshot capture, recount, variance valuation, approval, and atomic ledger posting**
- [ ] **Step 4: Run tests and verify Opname workflow passes**

---

### Task 7: Stock Return & Incident Modules (Loss/Damage & Customer/Supplier Returns)
**Files:**
- Create: `src/Wms.Application/Features/Operations/Returns/StockReturnDtos.cs` & `Validators.cs` & `IStockReturnService.cs`
- Create: `src/Wms.Infrastructure/Services/Operations/StockReturnService.cs`
- Create: `src/Wms.Application/Features/Operations/Incidents/StockIncidentDtos.cs` & `Validators.cs` & `IStockIncidentService.cs`
- Create: `src/Wms.Infrastructure/Services/Operations/StockIncidentService.cs`
- Test: `tests/Wms.Integration.Tests/Operations/StockReturnAndIncidentTests.cs`

- [ ] **Step 1: Write integration tests for Returns and Incidents (quarantine status, lost stock deduction, damaged status move, SoD)**
- [ ] **Step 2: Implement DTOs, validators, and service for Stock Returns**
- [ ] **Step 3: Implement DTOs, validators, and service for Stock Incidents**
- [ ] **Step 4: Register all new services in `DependencyInjection.cs`**
- [ ] **Step 5: Run tests and verify Returns and Incidents pass**

---

### Task 8: Web UI (Blazor MudBlazor Pages & NavMenu Update)
**Files:**
- Modify: `src/Wms.Web/Components/Layout/NavMenu.razor` (aktifkan navigasi operasional)
- Create: `src/Wms.Web/Components/Pages/Operations/PurchaseOrders.razor`
- Create: `src/Wms.Web/Components/Pages/Operations/StockTransfers.razor`
- Create: `src/Wms.Web/Components/Pages/Operations/StockOpnames.razor`
- Create: `src/Wms.Web/Components/Pages/Operations/StockIncidents.razor`
- Create: `src/Wms.Web/Components/Pages/Operations/StockReturns.razor`
- Create reusable dialog components for Create/Edit, Detail, Approval (with SoD warning if attempted by creator), and Receiving.

- [ ] **Step 1: Update `NavMenu.razor` to show operational workflow links with appropriate badges and permissions**
- [ ] **Step 2: Implement Purchase Orders Blazor Page & Dialogs**
- [ ] **Step 3: Implement Stock Transfers Blazor Page & Dialogs**
- [ ] **Step 4: Implement Stock Opname Blazor Page & Dialogs**
- [ ] **Step 5: Implement Stock Incidents Blazor Page & Dialogs**
- [ ] **Step 6: Implement Stock Returns Blazor Page & Dialogs**

---

### Task 9: Full Verification, Architectural Decisions Update & Documentation
**Files:**
- Modify: `docs/decisions.md` (Catat Keputusan 8: Pemisahan Tugas / SoD dan Workflow Operasional Tahap 5)
- Modify: `PROMPT_AI_WMS.md` (Tandai Tahap 5 selesai)
- Verify entire test suite passes
