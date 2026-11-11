# Spesifikasi Desain: Tahap 7 — Fitur Lanjutan

**Tanggal:** 2026-11-11  
**Status:** Approved  
**Penulis:** Antigravity AI & WMS Engineering Team  

---

## 1. Ringkasan Eksekutif
Dokumen ini mendefinisikan arsitektur teknis dan rancangan implementasi untuk **Tahap 7 — Fitur Lanjutan** sesuai dengan `WMS_SPEC.md` (bagian 19, 21, 22, 23, 28, 31) dan `PROMPT_AI_WMS.md`.

Tahap 7 mencakup 7 kapabilitas utama:
1. **Batch/Lot + Kedaluwarsa + FEFO Engine**:
   - Pelacakan batch/lot lengkap dengan tanggal produksi dan tanggal kedaluwarsa.
   - Kebijakan pencegahan pengeluaran batch kedaluwarsa (*expired checkout blocking*) pada transaksi rutin.
   - Algoritma rekomendasi pengambilan stok berbasis **FEFO** (*First-Expired, First-Out*).
2. **Barcode & QR Generator + Scanner Input Helper**:
   - Pembuatan Barcode (Code128) dan QR Code berbasis SVG/vektor menggunakan `ZXing.Net`.
   - Komponen input scan cepat yang mendukung *keyboard-wedge scanner* dengan fallback pencarian manual dan preview barang sebelum konfirmasi (tanpa klaim hardware khusus sebelum diuji).
3. **Mesin Impor CSV/XLSX**:
   - Wizard impor 3 tahap: Upload $\rightarrow$ Preview & Validasi Per Baris $\rightarrow$ Eksekusi Transaksional.
   - Mendukung impor Master Barang dan Saldo Awal/Stok Masuk dengan deteksi duplikasi dan validasi referensi database.
4. **Putaway & Picking Subsystem**:
   - *Putaway*: Saran otomatis penempatan lokasi rak berdasarkan riwayat lokasi produk, zona kategori, atau ketersediaan rak kosong.
   - *Picking*: Lembar kerja pengambilan barang (*picking list*) terurut berdasarkan lorong/rak dan batch rekomendasi FEFO.
5. **Sales Order (SO) + Reservasi Stok Atomik**:
   - Alur dokumen: `Draft` $\rightarrow$ `Confirmed` (stok *reserved*) $\rightarrow$ `Picking` $\rightarrow$ `Dispatched` (stok *deducted* & ledger posted) atau `Cancelled` (stok *released*).
   - Menggunakan model **Soft Reservation** pada level Produk & Gudang dengan alokasi batch spesifik saat proses picking.
6. **REST API Eksternal (Mesin-ke-Mesin)**:
   - Endpoint terproteksi `/api/v1/...` untuk kueri produk, pengecekan saldo stok real-time, dan pengiriman Sales Order.
   - Autentikasi berbasis **API Key** (`X-API-Key`) dengan hash SHA-256, permission scoping, dan dokumentasi OpenAPI/Swagger.
7. **Backup & Ekspor Sistem**:
   - Ekspor snapshot data relasional lengkap ke dalam format arsip JSON terstruktur dengan checksum SHA-256, hak akses eksklusif Super Admin, dan audit log lengkap.

---

## 2. Arsitektur & Struktur Proyek

```
src/
├── Wms.Domain/
│   ├── Entities/
│   │   ├── Inventory/
│   │   │   └── Batch.cs
│   │   ├── Orders/
│   │   │   ├── SalesOrder.cs
│   │   │   └── SalesOrderLine.cs
│   │   └── System/
│   │       ├── ApiKey.cs
│   │       └── SystemBackupJob.cs
│   └── Enums/
│       ├── SalesOrderStatus.cs
│       └── ApiKeyScope.cs
├── Wms.Application/
│   └── Features/
│       ├── Batches/
│       │   ├── IBatchService.cs
│       │   ├── IFefoService.cs
│       │   └── BatchDtos.cs
│       ├── Barcode/
│       │   ├── IBarcodeService.cs
│       │   └── BarcodeDtos.cs
│       ├── Imports/
│       │   ├── IDataImportService.cs
│       │   └── ImportDtos.cs
│       ├── WarehouseOperations/
│       │   ├── IPutawayService.cs
│       │   ├── IPickingService.cs
│       │   └── WarehouseOperationDtos.cs
│       ├── SalesOrders/
│       │   ├── ISalesOrderService.cs
│       │   └── SalesOrderDtos.cs
│       ├── ApiKeys/
│       │   ├── IApiKeyService.cs
│       │   └── ApiKeyDtos.cs
│       └── Backup/
│           ├── ISystemBackupService.cs
│           └── BackupDtos.cs
├── Wms.Infrastructure/
│   ├── Persistence/
│   │   └── Configurations/
│   │       ├── BatchConfiguration.cs
│   │       ├── SalesOrderConfiguration.cs
│   │       ├── ApiKeyConfiguration.cs
│   │       └── SystemBackupJobConfiguration.cs
│   ├── Services/
│   │   ├── Batches/
│   │   │   ├── BatchService.cs
│   │   │   └── FefoService.cs
│   │   ├── Barcode/
│   │   │   └── ZxingBarcodeService.cs
│   │   ├── Imports/
│   │   │   └── DataImportService.cs
│   │   ├── WarehouseOperations/
│   │   │   ├── PutawayService.cs
│   │   │   └── PickingService.cs
│   │   ├── SalesOrders/
│   │   │   └── SalesOrderService.cs
│   │   ├── ApiKeys/
│   │   │   └── ApiKeyService.cs
│   │   └── Backup/
│   │       └── SystemBackupService.cs
│   └── DependencyInjection.cs
└── Wms.Web/
    ├── Authentication/
    │   └── ApiKeyAuthenticationHandler.cs
    ├── Controllers/
    │   └── Api/
    │       └── v1/
    │           ├── ProductsApiController.cs
    │           ├── InventoryApiController.cs
    │           └── SalesOrdersApiController.cs
    ├── Components/
    │   ├── Common/
    │   │   ├── BarcodeScanInput.razor
    │   │   └── BarcodeDisplayDialog.razor
    │   └── Pages/
    │       ├── Batches/
    │       │   └── BatchesIndex.razor
    │       ├── Imports/
    │       │   └── ImportWizard.razor
    │       ├── SalesOrders/
    │       │   ├── SalesOrdersIndex.razor
    │       │   ├── SalesOrderDetail.razor
    │       │   └── PickingListDialog.razor
    │       └── Settings/
    │           ├── ApiKeysManagement.razor
    │           └── SystemBackupDialog.razor
    └── Program.cs
```

---

## 3. Desain Model Data & Database Constraints

### 3.1 Entitas `Batch` (`batches`)
```csharp
public class Batch : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsBlocked { get; set; } = false;
    public string? BlockReason { get; set; }

    public Product Product { get; set; } = null!;
}
```
*Index & Constraints:*
- Unique index: `(product_id, batch_number)`.
- Index: `expiry_date` untuk efisiensi kueri FEFO.

### 3.2 Entitas `SalesOrder` & `SalesOrderLine`
```csharp
public class SalesOrder : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty; // SO-YYYYMM-NNNNNN
    public Guid CustomerId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveryDate { get; set; }
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }

    public Guid? ConfirmedById { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public Guid? DispatchedById { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }

    public Customer Customer { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
}

public class SalesOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SalesOrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityDispatched { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
```

### 3.3 Entitas `ApiKey` (`api_keys`)
```csharp
public class ApiKey : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty; // e.g. "wms_live_..."
    public string KeyHash { get; set; } = string.Empty;   // SHA-256 hash
    public string Scopes { get; set; } = string.Empty;    // Comma-separated scopes
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAtUtc { get; set; }
}
```

---

## 4. Spesifikasi Logika Bisnis & Algoritma

### 4.1 Algoritma FEFO & Pencegahan Expired
- **Kueri Alokasi FEFO**:
  ```sql
  SELECT ib.*, b.expiry_date
  FROM inventory_balances ib
  LEFT JOIN batches b ON b.product_id = ib.product_id AND b.batch_number = ib.batch_number
  WHERE ib.product_id = @ProductId 
    AND ib.warehouse_id = @WarehouseId
    AND (ib.on_hand - ib.reserved) > 0
    AND (b.is_blocked IS NULL OR b.is_blocked = false)
    AND (b.expiry_date IS NULL OR b.expiry_date >= CURRENT_DATE)
  ORDER BY b.expiry_date ASC NULLS LAST, ib.created_at_utc ASC;
  ```
- **Expired Checkout Blocking**:
  Jika suatu batch memiliki `ExpiryDate < Today`, sistem secara otomatis menolak alokasi untuk `StockOut` dan `SalesOrder`. Batch hanya dapat diproses melalui alur `StockIncident` (status `quarantine`/`damaged`/`pemusnahan`).

### 4.2 Alur Reservasi Stok Sales Order (Soft Reservation)
1. **Konfirmasi (`ConfirmAsync`)**:
   - Untuk setiap baris SO, periksa ketersediaan: `InventoryBalance.Available (OnHand - Reserved) >= QuantityOrdered`.
   - Update `InventoryBalance.Reserved += QuantityOrdered`.
   - Update `SalesOrderLine.QuantityReserved = QuantityOrdered`.
   - Status SO beralih menjadi `Confirmed`. Dilakukan dalam satu transaksi atomik.
2. **Picking (`GeneratePickingListAsync`)**:
   - Memanggil `IFefoService` untuk mengalokasikan batch terurut tanggal kedaluwarsa terdekat dan lokasi rak spesifik.
3. **Pengiriman (`DispatchAsync`)**:
   - Memotong kuantitas stok: `InventoryBalance.OnHand -= Quantity` dan `InventoryBalance.Reserved -= Quantity`.
   - Menghasilkan record immutable `StockMovement` (Direction: `Out`, MovementType: `SalesOrderIssue`, SourceDocumentType: `SalesOrder`).
   - Status SO beralih menjadi `Dispatched`.
4. **Pembatalan (`CancelAsync`)**:
   - Jika dibatalkan saat status `Confirmed` atau `Picking`, lepas reservasi: `InventoryBalance.Reserved -= QuantityReserved`.
   - Status SO beralih menjadi `Cancelled`.

### 4.3 Alur Impor CSV/XLSX
- **Tahap 1 (Parse & Validasi)**:
  - Membaca baris tanpa melakukan mutasi database.
  - Memeriksa header wajib.
  - Cek duplikasi di internal file impor.
  - Cek keberadaan master di database (Gudang, Kategori, Satuan, Lokasi).
  - Mengembalikan DTO hasil validasi: `TotalRows`, `ValidRowsCount`, `ErrorRowsCount`, dan daftar rincian baris bermasalah beserta nomor baris dan pesan error.
- **Tahap 2 (Eksekusi Transaksional)**:
  - Menggunakan transaksi EF Core (`IDbContextTransaction`).
  - Bila ada error fatal pada baris yang dieksekusi, seluruh transaksi di-rollback secara utuh.
  - Mencatat aksi impor ke dalam `IAuditLogService`.

### 4.4 Barcode & QR Code Engine
- Menggunakan `ZXing.Net` (BarcodeWriter) untuk merender barcode 1D (Code128) dan QR Code.
- Output berupa format SVG atau base64 PNG data-URI yang ringan dan siap cetak.
- Komponen `BarcodeScanInput.razor`:
  - Menggunakan keyboard event listener (menangkap karakter dan Enter key terminator).
  - Mode input manual fallback: tombol cari dengan dialog autocomplete.
  - Preview informasi barang (SKU, Nama, Satuan, Saldo Saat Ini) sebelum ditambahkan ke transaksi.

### 4.5 External REST API
- Prefix rute: `/api/v1/`.
- Autentikasi: Header `X-API-Key`. Filter memeriksa hash kunci terhadap tabel `api_keys`.
- Format respons standar:
  ```json
  {
    "success": true,
    "data": { ... },
    "error": null,
    "timestampUtc": "2026-11-11T02:55:00Z"
  }
  ```
- Error handling seragam mengikuti standar RFC 7807 (`ProblemDetails`).

### 4.6 Backup & Ekspor Sistem
- Service mengekspor seluruh tabel konfigurasi, master, dan riwayat transaksi ke dalam file arsip `.json` yang terstruktur dan rapi.
- Menghitung SHA-256 hash checksum dari payload backup untuk verifikasi integritas.
- Mencatat aksi backup ke audit log dengan level informasi detail.

---

## 5. Rencana Pengujian
1. **Unit Tests (`Wms.Domain.Tests` & `Wms.Application.Tests`)**:
   - `FefoServiceTests`: Memastikan urutan batch yang dialokasikan benar-benar berdasarkan tanggal kedaluwarsa terdekat.
   - `BatchExpiryValidationTests`: Memastikan batch kedaluwarsa ditolak dari pengeluaran biasa.
   - `SalesOrderReservationTests`: Memastikan kuantitas `Reserved` bertambah saat konfirmasi dan terlepas saat pembatalan.
   - `DataImportServiceTests`: Menguji validasi CSV/XLSX terhadap format salah, duplikasi, dan referensi asing tidak valid.
   - `BarcodeServiceTests`: Menguji pembentukan format barcode dan QR code valid.
2. **Integration Tests (`Wms.Integration.Tests`)**:
   - `SalesOrderEndToEndTests`: Alur Draft $\rightarrow$ Confirmed $\rightarrow$ Picking $\rightarrow$ Dispatch pada database PostgreSQL nyata dengan verifikasi saldo ledger dan audit log.
   - `ApiKeyAuthenticationTests`: Menguji penolakan API key tidak valid dan keberhasilan request dengan API key terdaftar.
   - `DataImportIntegrationTests`: Pengujian impor master barang dan saldo awal dalam transaksi nyata.
   - `SystemBackupIntegrationTests`: Verifikasi integritas data hasil ekspor backup.

