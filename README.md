# Enterprise Warehouse Management System (WMS)

Sistem Manajemen Pergudangan (WMS) modern, handal, dan berkinerja tinggi yang dibangun dengan **.NET 8 (C#)**, **Blazor Server (MudBlazor)**, dan **PostgreSQL (Supabase)**. Dirancang dengan prinsip Clean Architecture, Domain-Driven Design (DDD), append-only stock ledger, valuasi moving average otomatis, dan kepatuhan audit ketat.

---

## Fitur Utama

- **Master Data Terpadu**: Gudang, Lokasi/Rak, Kategori, Satuan, Produk dengan validasi SKU & Barcode unik global.
- **Buku Besar Persediaan Append-Only**: Pencatatan mutasi stok immutable (`stock_movements`) tanpa operasi DELETE, didukung penolakan stok negatif tegas di level database dan ORM.
- **Valuasi Persediaan Rata-Rata Bergerak (Moving Average Valuation)**: Perhitungan HPP / moving average otomatis setiap penerimaan barang masuk dan pengeluaran barang keluar.
- **Workflow Operasional Lengkap**:
  - Purchase Order (PO) dengan approval bertingkat dan penerimaan parsial (Partial Receipt).
  - Transfer Stok Antar-Gudang dengan status `in_transit` tanpa duplikasi saldo.
  - Stock Opname dengan snapshot saldo, input recount fisik, hitung varians nilai rupiah, pemisahan tugas (Separation of Duties), dan posting otomatis.
  - Penanganan Kejadian Hilang / Rusak (Stock Incident) dan Retur Barang.
  - Sales Order (SO) dengan soft reservation stok dan alokasi lot/batch berbasis FEFO (First-Expired, First-Out).
- **Keamanan & Otorisasi Granular (RBAC)**:
  - Role bawaan: `SuperAdmin`, `Supervisor`, `AdminGudang`, `PetugasGudang`, dan `Auditor`.
  - Pembatasan akses multi-gudang (Warehouse Boundary) otomatis via EF Core Global Query Filter.
  - Pencegahan Self-Approval pada seluruh alur persetujuan dokumen.
- **Notifikasi & Ekspor Data**:
  - Notifikasi in-app real-time dengan deduplikasi.
  - Ekspor laporan berkinerja tinggi ke format **CSV**, **Microsoft Excel (.xlsx via ClosedXML)**, dan **PDF (via QuestPDF)**.
  - Background jobs scheduler via Quartz.NET.

---

## Struktur Solusi (Clean Architecture)

```
WMS/
├── src/
│   ├── Wms.Domain/            # Entities, Enums, Value Objects, Core Business Invariants
│   ├── Wms.Application/       # DTOs, CQRS/Service Interfaces, Business Logic, Validators
│   ├── Wms.Infrastructure/    # EF Core DbContext, Repositories, Background Jobs, Reporting
│   └── Wms.Web/               # Blazor Server UI, Controllers, Components, Authentication
├── tests/
│   ├── Wms.Domain.Tests/      # Unit testing domain entities & invariants
│   ├── Wms.Application.Tests/ # Unit testing validators, models, UI states
│   └── Wms.Integration.Tests/ # Integration testing end-to-end workflows
└── docs/                      # Dokumentasi teknis, keputusan arsitektur, dan panduan deployment
```

---

## Panduan Cepat Menjalankan Sistem (Quickstart)

### 1. Prasyarat
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL 15/16 (Lokal atau [Supabase](https://supabase.com))

### 2. Konfigurasi Database & Environment
Salin file template `.env.example` menjadi `.env.local`:
```bash
cp .env.example .env.local
```
Sesuaikan `ConnectionStrings__DefaultConnection` dengan kredensial PostgreSQL / Supabase Anda:
```bash
ConnectionStrings__DefaultConnection="Host=db.xxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=xxx;SSL Mode=Require;Trust Server Certificate=true;"
```

### 3. Build & Jalankan Migrasi
```bash
dotnet restore
dotnet build --no-restore -warnaserror
cd src/Wms.Web
dotnet ef database update --project ../Wms.Infrastructure
```

### 4. Jalankan Aplikasi
```bash
dotnet run --project src/Wms.Web
```
Buka browser di `http://localhost:5000` (atau URL yang tertera di konsol).

### 5. Akun Bawaan untuk Pengujian
| Username | Password | Role | Keterangan |
|---|---|---|---|
| `admin` | `Admin123!` | `SuperAdmin` | Akses penuh & konfigurasi sistem |
| `supervisor` | `Admin123!` | `Supervisor` | Approval PO, Opname, Transfer, & Insiden |
| `staff` | `Admin123!` | `AdminGudang` | Input transaksi masuk/keluar & operasional |
| `auditor` | `Admin123!` | `Auditor` | Akses baca laporan & audit trail |

---

## Verifikasi dan Pengujian (Testing Suite)

Jalankan test suite lengkap untuk memvalidasi seluruh integritas sistem:
```bash
# Jalankan unit test Domain
dotnet test tests/Wms.Domain.Tests/Wms.Domain.Tests.csproj --no-build

# Jalankan unit test Application
dotnet test tests/Wms.Application.Tests/Wms.Application.Tests.csproj --no-build

# Jalankan integration test
dotnet test tests/Wms.Integration.Tests/Wms.Integration.Tests.csproj --no-build
```

Total **120 tests** mencakup 13 kategori kritis:
1. Autentikasi dan RBAC
2. Pencegahan Duplikasi SKU & Barcode
3. Mutasi Barang Masuk & Keluar
4. Penolakan Tegas Stok Negatif
5. Idempotency Key pada Posting Transaksi
6. Pembatalan/Koreksi dengan Ledger & Audit Log
7. Penerimaan Parsial PO Bertahap
8. Transfer Antar-Gudang Tanpa Duplikasi Saldo
9. Stock Opname, Perhitungan Varians Nilai, & Approval
10. Valuasi Rata-Rata Bergerak (Moving Average)
11. Filter dan Ekspor Laporan (CSV, Excel, PDF)
12. Pembatasan Batas Akses Multi-Gudang (Global Query Filter)
13. Konsistensi Penanganan State UI (Loading, Empty, Error, Success)

---

## Dokumentasi Lengkap

Untuk panduan mendalam mengenai arsitektur, deployment, batasan paket Supabase, dan strategi backup, silakan baca:
- [Panduan Deployment & Operasional](docs/DEPLOYMENT_AND_OPERATIONS.md)
- [Log Keputusan Teknis & Arsitektur](docs/decisions.md)
- [Spesifikasi Teknis Detail (WMS_SPEC.md)](WMS_SPEC.md)
- [Instruksi Tahapan (PROMPT_AI_WMS.md)](PROMPT_AI_WMS.md)

---
*Dikembangkan dengan standar rekayasa perangkat lunak perusahaan untuk performa, keandalan, dan integritas audit persediaan.*

