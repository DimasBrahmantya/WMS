# Spesifikasi Desain: Tahap 6 — Notifikasi dan Ekspor

**Tanggal:** 2026-11-11  
**Status:** Approved  
**Penulis:** Antigravity AI & WMS Engineering Team  

---

## 1. Ringkasan Eksekutif
Dokumen ini mendefinisikan arsitektur teknis dan rancangan implementasi untuk **Tahap 6 — Notifikasi dan Ekspor** sesuai dengan `WMS_SPEC.md` (bagian 16, 23, 24, 26) dan `PROMPT_AI_WMS.md`.

Tahap 6 berfokus pada:
1. **Background Job Engine**: Menggunakan **Quartz.NET** yang terintegrasi langsung dengan .NET Generic Host / DI tanpa dependensi dashboard eksternal.
2. **Pusat Notifikasi Dalam Aplikasi (In-App Notification Center)**:
   - Dukungan deduplikasi cerdas (*one active notification per condition per entity*).
   - Auto-resolusi (*recovery detection*) saat kondisi anomali telah pulih kembali.
   - Antarmuka pengguna: Indikator lonceng & badge jumlah belum dibaca pada AppBar serta halaman terdedikasi `/notifications` dengan filter komprehensif.
3. **Mesin Ekspor Multi-Format**:
   - Mendukung format **CSV** (`CsvHelper`), **XLSX** (`ClosedXML`), dan **PDF** (`QuestPDF` dengan Community License).
   - Strategi dual-mode: *Streaming langsung* untuk data kecil/sedang ($\le 1.000$ baris) dan *Background Job asinkron* (`ExportJob`) untuk kumpulan data besar atau atas permintaan pengguna.
   - File ekspor latar belakang disimpan di direktori terisolasi `App_Data/Exports` dengan batas kedaluwarsa 24 jam dan pembersihan otomatis.
4. **Pencatatan Audit Trail**:
   - Setiap operasi ekspor (baik langsung maupun via background job) dan setiap temuan anomali rekonsiliasi dicatat ke dalam tabel `audit_logs` melalui `IAuditLogService`.

---

## 2. Arsitektur & Struktur Proyek

Sistem mengikuti pola *Clean Architecture Modular Monolith* yang konsisten dengan tahap-tahap sebelumnya:

```
src/
├── Wms.Domain/
│   ├── Entities/
│   │   ├── Notifications/
│   │   │   └── AppNotification.cs
│   │   └── Exports/
│   │       └── ExportJob.cs
│   └── Enums/
│       ├── NotificationCategory.cs
│       ├── NotificationStatus.cs
│       ├── ExportFormat.cs
│       └── ExportJobStatus.cs
├── Wms.Application/
│   ├── Features/
│   │   ├── Notifications/
│   │   │   ├── INotificationService.cs
│   │   │   └── NotificationDtos.cs
│   │   └── Exports/
│   │       ├── IExportService.cs
│   │       ├── IExportEngine.cs
│   │       └── ExportDtos.cs
│   └── Common/
│       └── Interfaces/
│           └── IBackgroundJobScheduler.cs
├── Wms.Infrastructure/
│   ├── Persistence/
│   │   └── Configurations/
│   │       ├── AppNotificationConfiguration.cs
│   │       └── ExportJobConfiguration.cs
│   ├── BackgroundJobs/
│   │   ├── StockAlertJob.cs
│   │   ├── OverduePurchaseOrderJob.cs
│   │   ├── ReconciliationJob.cs
│   │   ├── BackgroundExportWorkerJob.cs
│   │   └── CleanupExportFilesJob.cs
│   ├── Services/
│   │   ├── Notifications/
│   │   │   └── NotificationService.cs
│   │   └── Exports/
│   │       ├── ExportService.cs
│   │       ├── Formatters/
│   │       │   ├── CsvReportFormatter.cs
│   │       │   ├── ExcelReportFormatter.cs
│   │       │   └── PdfReportFormatter.cs
│   │       └── FileStorage/
│   │           └── LocalExportFileStorage.cs
│   └── DependencyInjection.cs
└── Wms.Web/
    ├── Components/
    │   ├── Layout/
    │   │   ├── NotificationBell.razor
    │   │   └── MainLayout.razor (diperbarui)
    │   └── Pages/
    │       ├── Notifications/
    │       │   └── NotificationsIndex.razor
    │       └── Reports/
    │           ├── Dialogs/
    │           │   └── ExportDialog.razor
    │           ├── StockReport.razor (diperbarui tombol ekspor)
    │           ├── StockCardReport.razor (diperbarui tombol ekspor)
    │           └── ValuationReport.razor (diperbarui tombol ekspor)
    └── Controllers/
        └── ExportsDownloadController.cs
```

---

## 3. Desain Model Data & Domain Entities

### 3.1. `AppNotification`
Menyimpan riwayat dan status notifikasi untuk pengguna atau peran tertentu.
```csharp
namespace Wms.Domain.Entities.Notifications;

public class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; } // null jika disiarkan ke role tertentu
    public string? TargetRole { get; set; } // misalnya "SuperAdmin", "AdminGudang"
    public NotificationCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; } // tautan cepat ke dokumen/halaman terkait
    public string DedupKey { get; set; } = string.Empty; // e.g. "LowStock:WH-01:SKU-100"
    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
```

**Index Database:**
- `IX_app_notifications_user_status` pada `(user_id, status)`
- `IX_app_notifications_dedup_active` pada `(dedup_key, status)` dengan predikat `status != 'Resolved'`

### 3.2. `ExportJob`
Melacak permintaan ekspor laporan besar yang diproses di latar belakang.
```csharp
namespace Wms.Domain.Entities.Exports;

public class ExportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByUserName { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty; // StockReport, StockCard, ValuationReport, AuditLog
    public ExportFormat Format { get; set; }
    public string FilterPayloadJson { get; set; } = "{}";
    public ExportJobStatus Status { get; set; } = ExportJobStatus.Pending;
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public int RowCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(24);
}
```

### 3.3. Enums
- **`NotificationCategory`**: `StockAlert`, `PurchaseOrder`, `Approval`, `Reconciliation`, `SystemExport`, `General`.
- **`NotificationStatus`**: `Unread`, `Read`, `Resolved`.
- **`ExportFormat`**: `Csv`, `Xlsx`, `Pdf`.
- **`ExportJobStatus`**: `Pending`, `Processing`, `Completed`, `Failed`, `Expired`.

---

## 4. Mekanisme Deduplikasi & Resolusi Notifikasi

1. **Aturan Deduplikasi**:
   - Satu entitas dan satu kondisi hanya memiliki **maksimum 1 notifikasi aktif** (`Status != Resolved`).
   - Format standar `DedupKey`:
     - Stok Minimum: `LowStock:{WarehouseId}:{ItemId}`
     - Stok Habis: `OutOfStock:{WarehouseId}:{ItemId}`
     - PO Terlambat: `OverduePo:{PurchaseOrderId}`
     - Rekonsiliasi Selisih: `ReconDiff:{WarehouseId}:{ItemId}`
     - Persetujuan Tertunda: `PendingApproval:{DocumentType}:{DocumentId}`
   - Saat sebuah job atau event memicu penerbitan notifikasi:
     - Sistem mengecek apakah ada record di `app_notifications` dengan `DedupKey` yang sama dan `Status != NotificationStatus.Resolved`.
     - Jika sudah ada, sistem tidak menambah baris baru (*deduped*).
2. **Aturan Resolusi (Kondisi Pulih)**:
   - Ketika kondisi anomali telah tertangani (misalnya stok barang bertambah sehingga berada di atas stok minimum, atau saldo telah direkonsiliasi):
     - Background job atau service terkait memanggil `ResolveNotificationByDedupKeyAsync(dedupKey)`.
     - Status notifikasi diubah menjadi `Resolved` dan `ResolvedAtUtc` diisi waktu saat ini.
   - Jika di masa mendatang kondisi tersebut terjadi kembali, baris baru dengan `DedupKey` yang sama dapat dibuat kembali secara sah.

---

## 5. Mesin Ekspor Multi-Format & Alur Pemrosesan

### 5.1. Spesifikasi Format
1. **CSV (Comma-Separated Values)**:
   - Menggunakan pustaka `CsvHelper`.
   - Baris header kapital, pemisah koma, string dengan spasi/koma di-escape sesuai RFC 4180.
   - Paginasi streaming (buffer batch) agar memori tetap konstan pada laporan jutaan baris.
2. **XLSX (Microsoft Excel)**:
   - Menggunakan pustaka `ClosedXML`.
   - Header berwarna tema (*navy blue*, teks putih, bold).
   - Format sel: Tanggal `yyyy-MM-dd HH:mm`, angka kuantitas `#,##0.00##`, nilai uang `Rp #,##0.00`.
   - Auto-fit kolom dan baris total di baris paling bawah.
3. **PDF (Portable Document Format)**:
   - Menggunakan pustaka `QuestPDF` dengan lisensi komunitas: `QuestPDF.Settings.License = LicenseType.Community;`.
   - Kop dokumen standar WMS Nusantara memuat: Judul Laporan, Filter yang Digunakan, Tanggal Cetak, dan Nama Pengguna.
   - Tabel responsif dengan paging, zebra striping, dan nomor halaman "Halaman X dari Y".

### 5.2. Alur Ekspor Langsung vs Latar Belakang
1. **Ekspor Langsung ($\le 1.000$ baris)**:
   - Dieksekusi secara sinkron di controller/service.
   - Menghasilkan `byte[]` dan mengembalikan respon langsung atau stream unduh via JavaScript Interop.
   - Menulis audit log: `Action: "Export"`, `EntityType: "Report"`, `Details: { Format, RowCount, Filter }`.
2. **Ekspor Latar Belakang ($> 1.000$ baris atau Opsi Manual)**:
   - Pengguna memilih "Jalankan di Latar Belakang" atau sistem mendeteksi perkiraan baris $> 1.000$.
   - Sistem mencatat `ExportJob` dengan status `Pending`.
   - Background worker (Quartz.NET) mengeksekusi job:
     1. Menarik data secara batch dari database.
     2. Menulis file ke disk di `App_Data/Exports/{jobId}.{ext}`.
     3. Memperbarui record `ExportJob` menjadi `Completed`, mencatat `RowCount` dan `FileSizeBytes`.
     4. Menerbitkan `AppNotification` ke pengguna peminta:
        - Judul: "Laporan Siap Diunduh"
        - Pesan: "Ekspor {ReportType} ({Format}) telah selesai ({RowCount} baris)."
        - Link: "/api/exports/download/{jobId}"
3. **Endpoint Unduhan Aman**:
   - `GET /api/exports/download/{jobId}`
   - Memvalidasi autentikasi pengguna: Hanya pemilik tiket atau user dengan peran `SuperAdmin` yang dapat mengunduh.
   - Mengembalikan `FileStreamResult` dengan `Content-Disposition: attachment`.
   - Mencatat akses unduhan ke `audit_logs`.

---

## 6. Background Jobs (Quartz.NET)

1. **`StockAlertJob`** (Jadwal: Setiap 15 Menit)
   - Memeriksa `inventory_balances` di mana `quantity_on_hand <= item.minimum_stock_level`.
   - Jika stok $\le 0$: Menerbitkan notifikasi `OutOfStock`.
   - Jika $0 < \text{stok} \le \text{minimum}$: Menerbitkan notifikasi `LowStock`.
   - Menandai `Resolved` untuk item yang sebelumnya memiliki notifikasi aktif tetapi kini stoknya sudah di atas batas minimum.
2. **`OverduePurchaseOrderJob`** (Jadwal: Setiap 1 Jam)
   - Mencari PO dengan status `Ordered` atau `PartiallyReceived` dengan `ExpectedDeliveryDate < DateTime.UtcNow`.
   - Menerbitkan notifikasi `PurchaseOrder` dengan judul peringatan keterlambatan penerimaan.
3. **`ReconciliationJob`** (Jadwal: Harian pada 01:00 UTC)
   - Menghitung agregat mutasi pada `stock_movements` per barang dan gudang, lalu membandingkannya dengan `inventory_balances.quantity_on_hand`.
   - Jika terdeteksi selisih kuantitas:
     - Menerbitkan notifikasi berkategori `Reconciliation` kepada peran `SuperAdmin`.
     - Mencatat anomali ke tabel `audit_logs` dengan tingkat keparahan tinggi.
4. **`CleanupExportFilesJob`** (Jadwal: Harian pada 02:00 UTC)
   - Menghapus file fisik ekspor yang telah berusia $> 24$ jam.
   - Mengubah status `ExportJob` yang terkait menjadi `Expired`.

---

## 7. Rencana Pengujian

1. **Unit Testing (`Wms.Application.Tests` & `Wms.Domain.Tests`)**:
   - Test deduplikasi `NotificationService`: Memanggil publish dengan `DedupKey` yang sama dua kali hanya menghasilkan 1 baris aktif.
   - Test resolusi `NotificationService`: Memanggil resolve mengubah status menjadi `Resolved`.
   - Test formatters `CsvReportFormatter`, `ExcelReportFormatter`, dan `PdfReportFormatter` menghasilkan byte array yang valid dan tidak korup.
2. **Integration Testing (`Wms.Integration.Tests`)**:
   - Test end-to-end pemrosesan `ExportJob`: Membuat job, menjalankan worker, memverifikasi file fisik terbuat di folder ekspor, dan memverifikasi endpoint download menghasilkan file dengan status 200 OK.
   - Test `ReconciliationJob`: Menyisipkan ketidaksesuaian saldo buatan, menjalankan job, memverifikasi notifikasi dan audit log terbentuk.
   - Test otorisasi download: Pengguna lain tanpa hak akses tidak dapat mengunduh file ekspor milik pengguna berbeda (403 Forbidden).

---

## 8. Lisensi Pustaka Pihak Ketiga
- `CsvHelper`: MS-PL / Apache-2.0 (Open Source, aman untuk komersial).
- `ClosedXML`: MIT License (Open Source, aman untuk komersial).
- `QuestPDF`: Community License (Gratis untuk organisasi/individu dengan pendapatan tahunan di bawah \$1M USD; konfigurasi eksplisit diset pada aplikasi).
- `Quartz.NET`: Apache-2.0 (Open Source, aman untuk komersial).

---

Dokumen spesifikasi ini siap ditindaklanjuti ke rencana implementasi task-by-task (*Implementation Plan*).

