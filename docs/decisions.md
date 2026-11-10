# Log Keputusan Teknis dan Arsitektur (Architectural Decision Log)

Dokumen ini mencatat keputusan arsitektur, teknis, dan verifikasi fondasi yang diambil selama pengembangan Warehouse Management System (WMS), sesuai dengan [WMS_SPEC.md](../WMS_SPEC.md) dan [PROMPT_AI_WMS.md](../PROMPT_AI_WMS.md).

---

## Keputusan 1: Pemilihan Target Runtime .NET

- **Status**: Diputuskan (Tahap 0)
- **Konteks**: 
  - Mesin pengembangan memiliki SDK .NET `8.0.415` dan `10.0.402`.
  - Spesifikasi mewajibkan: `.NET LTS terbaru yang stabil, C# dengan nullable reference types aktif`.
- **Keputusan**:
  - Menggunakan target framework **.NET 8.0 (LTS)** (`net8.0`).
  - Mengunci versi SDK menggunakan `global.json` ke versi .NET 8 LTS (`8.0.415` dengan rollForward `latestFeature`) untuk menjamin kompatibilitas menyeluruh dengan library ekosistem seperti MudBlazor, Npgsql 8.x, EF Core 8.x, Testcontainers, dan Serilog.
- **Konsekuensi**: Stabilitas ekosistem terjamin, tidak ada breaking changes dari runtime non-LTS/preview.

---

## Keputusan 2: Strategi Koneksi PostgreSQL (Supabase) & Npgsql

- **Status**: Terverifikasi & Aktif (Tahap 0)
- **Konteks**:
  - Supabase menyediakan 3 metode koneksi: Direct Connection (5432), Session Pooler (5432), dan Transaction Pooler (6543 via PgBouncer/Supavisor).
  - Npgsql secara default memanfaatkan *prepared statements*, advisory locks, dan fitur koneksi PostgreSQL standar.
  - EF Core Migrations membutuhkan DDL locks dan transient table operations yang tidak didukung secara stabil di Transaction Pooler.
- **Keputusan**:
  1. **Aplikasi Server (Runtime ASP.NET Core)**: Menggunakan **Direct Connection** atau **Session Pooler** (Port 5432).
  2. **EF Core Migrations**: **Wajib** dijalankan melalui koneksi langsung / Session Pooler (Port 5432), bukan Transaction Pooler (Port 6543).
  3. Konfigurasi connection string dan secret disimpan via `appsettings.Development.json` (lokal/user-secrets) dan environment variable (production). Disediakan template `appsettings.Example.json` dan `.env.example`.
- **Hasil Verifikasi Lapangan (Live Test)**:
  - Koneksi Npgsql langsung ke `db.nurxlhkrixnqvnocqbop.supabase.co:5432` dengan database `postgres` dan SSL mode `Require` berhasil diverifikasi aktif (`SELECT current_database(), version();`). Database merespon PostgreSQL Supabase dengan latency normal.
- **Konsekuensi**: Mencegah kegagalan migrasi dan prepared statement caching error pada Npgsql.

---

## Keputusan 3: Konfirmasi Kebijakan Bisnis Default (Bagian 26.1)

- **Status**: Ditetapkan sebagai asumsi baseline (Tahap 0)
- **Daftar Kebijakan**:
  1. *Penyesuaian stok dari stock opname*: Wajib persetujuan Supervisor.
  2. *Purchase Order (PO)*: Nilai di atas Rp10.000.000,- wajib persetujuan Manajer.
  3. *Barang keluar bertipe hilang/rusak/buang*: Selalu membutuhkan persetujuan.
  4. *Barang masuk & keluar rutin*: Tanpa persetujuan (langsung diposting oleh role yang berwenang).
  5. *Pembatalan transaksi terposting*: Wajib persetujuan Supervisor dan alasan tertulis (menghasilkan movement lawan bertaut).
  6. *Pemisahan Tugas (Separation of Duties)*: Pembuat dokumen diblokir dari menyetujui dokumen yang dibuatnya sendiri.

---

## Keputusan 4: Desain Konsistensi Stok dan Concurrency

- **Status**: Diputuskan (Tahap 0)
- **Keputusan**:
  1. **Append-Only Ledger**: Tabel `stock_movements` bersifat *append-only*.
  2. **Materialized Balances**: Tabel `inventory_balances` diperbarui secara atomik dalam transaksi database yang sama dengan ledger.
  3. **Pencegahan Stok Negatif**: Database constraint `CHECK (on_hand >= 0)` dan atomic update `WHERE on_hand - @qty >= 0`.
  4. **Pencegahan Deadlock**: Urutan penguncian deterministik pada dokumen multi-item (urut `product_id` kemudian `location_id`).
  5. **Idempotency**: Setiap operasi mutasi menyertakan `idempotency_key` unik yang divalidasi di tabel `idempotency_keys`.

---

## Keputusan 5: Pembatasan Gudang Per Pengguna via Global Query Filter EF Core

- **Status**: Diputuskan & Diimplementasikan (Tahap 2)
- **Konteks**:
  - Bagian 4.5 & 20 spesifikasi: *Pengguna dapat dibatasi ke gudang tertentu... Pembatasan gudang per pengguna diterapkan lewat EF Core global query filter dan pemeriksaan eksplisit pada mutasi.*
  - EF Core mengevaluasi closure sub-expressions pada runtime. Jika sub-property dievaluasi saat user context bernilai null (misal design-time migration atau background job tanpa HTTP context), dapat memicu `NullReferenceException`.
- **Keputusan**:
  - `WmsDbContext` mengekspos properti aman `IsUserRestrictedToWarehouses` dan `UserAllowedWarehouseIds` yang mengevaluasi `IUserContext`.
  - Global query filter pada `Warehouse` dan `WarehouseLocation`:
    - `entity.HasQueryFilter(w => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(w.Id));`
    - `entity.HasQueryFilter(l => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(l.WarehouseId));`
  - Peran `SuperAdmin` dan pengguna tanpa pembatasan gudang (`AllowedWarehouseIds.Count == 0`) otomatis melihat seluruh gudang.
  - Pengguna dengan pembatasan gudang hanya dapat mengakses dan melihat gudang serta lokasi rak yang ditugaskan kepadanya.
  - Operasi administrasi sistem dapat memanfaatkan `.IgnoreQueryFilters()` secara eksplisit bila diperlukan.

---

## Keputusan 6: Strategi Soft Delete dan Pencegahan Hapus Permanen Data Tereferensi

- **Status**: Diputuskan & Diimplementasikan (Tahap 2)
- **Konteks**:
  - Bagian 7 & 8 spesifikasi & instruksi Tahap 2: *Tidak ada hapus permanen untuk master yang direferensikan. Strategi nonaktif master: soft delete (`is_active`).*
  - Halaman master data membutuhkan visibilitas data nonaktif untuk keperluan audit, filter status, dan reaktivasi akun/entitas.
- **Keputusan**:
  - Status aktif/nonaktif dikelola via field `IsActive` di antarmuka `ISoftDeletable`.
  - Pada query master data, `IsActive` bertindak sebagai parameter filter fleksibel (`bool? isActive`: `null` = semua, `true` = aktif, `false` = nonaktif).
  - Sebelum menghapus data master (Kategori, Satuan, Gudang, Lokasi, Barang, Pengguna, Peran), service melakukan pemeriksaan referensi bisnis (`AnyAsync` pada entitas terkait/riwayat).
  - Jika data masih direferensikan, penghapusan permanen ditolak dengan pesan kesalahan bisnis yang jelas (misalnya: *"Kategori tidak dapat dihapus permanen karena masih digunakan oleh produk. Silakan nonaktifkan status kategori ini."*).
  - Foreign key constraint di PostgreSQL menggunakan `ON DELETE RESTRICT` sebagai perlindungan lapis database tambahan.

---

## Keputusan 7: Valuasi Persediaan Rata-Rata Bergerak (Moving Average) Sesuai Bagian 9.3

- **Status**: Diputuskan & Diimplementasikan (Tahap 3)
- **Konteks**:
  - Bagian 9.3 spesifikasi WMS: Biaya rata-rata bergerak dihitung per barang per gudang (bukan per batch), disimpan di tabel `inventory_valuations` (`on_hand_qty`, `on_hand_value`, `avg_cost`, `last_cost`).
  - Aturan:
    - Masuk: `avg_cost_baru = (on_hand_value + qty_masuk * biaya_satuan) / (on_hand_qty + qty_masuk)`.
    - Keluar: dinilai pada `avg_cost` saat posting; `avg_cost` tidak berubah.
    - Nol stok: saat `on_hand_qty` mencapai 0, `on_hand_value` dijamin tepat 0 (selisih pembulatan dinolkan).
    - Reversal masuk: mengurangi saldo dan menghitung ulang `avg_cost`, menolak jika stok menjadi negatif.
    - Reversal keluar: mengembalikan stok pada unit cost saat keluar dan menghitung ulang `avg_cost`.
    - Presisi: `numeric(18,4)`, pembulatan `MidpointRounding.AwayFromZero`.
- **Keputusan**:
  - Logika diisolasi dalam `StockPostingService` di bawah transaksi database atomik.
  - Setiap perhitungan divalidasi dengan unit test menyeluruh untuk memastikan kepatuhan formula matematika.

---

## Keputusan 8: Append-Only Stock Movements Ledger dan Penguncian Deterministik

- **Status**: Diputuskan & Diimplementasikan (Tahap 3)
- **Konteks**:
  - Bagian 9.2, 9.4, dan 28: Tabel `stock_movements` bersifat *append-only*. Perubahan status dan pembatalan hanya mencatat movement lawan bertaut (`reversed_by_movement_id`).
  - Dokumen multi-baris berisiko memicu deadlock pada transaksi konkuren jika urutan lock tidak beraturan.
- **Keputusan**:
  - Perlindungan ganda append-only:
    1. Di ORM (`WmsDbContext.SaveChangesAsync`): melempar `InvalidOperationException` jika terdeteksi `EntityState.Deleted` atau modifikasi kolom finansial/kuantitas pada `StockMovement`.
    2. Di database: Foreign key bertaut dan check constraint `quantity > 0`.
  - Pencegahan deadlock: Baris barang dalam dokumen selalu diurutkan secara deterministik (`OrderBy(ProductId).ThenBy(WarehouseLocationId)`) sebelum row-locking / eksekusi mutasi.

---

## Keputusan 9: Idempotency Key dan Document Sequences

- **Status**: Diputuskan & Diimplementasikan (Tahap 3)
- **Konteks**:
  - Jaringan atau double click pada tombol posting dapat menyebabkan double submission.
  - Penomoran dokumen harus unik dan sequential per periode `{PREFIX}-{YYYYMM}-{NNNNNN}`.
- **Keputusan**:
  - `IdempotencyService` menyimpan payload hasil eksekusi di tabel `idempotency_keys` dengan unique index `(user_id, operation, key)`. Pemanggilan ulang dengan key yang sama mengembalikan respon tersimpan tanpa menggandakan movement.
  - `DocumentSequenceService` mengelola counter nomor di tabel `document_sequences` per prefix dan periode (YYYYMM) dalam transaksi dengan penguncian aman.

---

## Keputusan 10: Mekanisme Rekonsiliasi Saldo vs Ledger

- **Status**: Diputuskan & Diimplementasikan (Tahap 3)
- **Konteks**:
  - Menjamin integritas konsistensi saldo fisik (`inventory_balances`) terhadap akumulasi kronologis pergerakan (`stock_movements`).
- **Keputusan**:
  - Layanan `StockReconciliationService` membandingkan `balance.OnHand` dengan `SUM(CASE WHEN In THEN Qty ELSE -Qty END)` untuk seluruh kombinasi barang, gudang, lokasi, dan status stok.
  - Menghasilkan laporan ketidakkonsistenan secara otomatis untuk kebutuhan audit dan health monitoring persediaan.
