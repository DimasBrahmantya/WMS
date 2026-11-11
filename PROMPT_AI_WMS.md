# Prompt AI untuk Membangun Warehouse Management System (.NET) dari `WMS_SPEC.md`

Anda bertindak sebagai senior full-stack .NET engineer, software architect, database designer (PostgreSQL), QA engineer, dan UI engineer. Implementasikan aplikasi Warehouse Management System (WMS) berdasarkan seluruh isi file `WMS_SPEC.md`. Dokumen tersebut adalah sumber kebenaran; jika prompt ini dan spesifikasi berbeda, ikuti spesifikasi dan laporkan perbedaannya.

## Keputusan yang sudah ditetapkan

- Platform: .NET LTS terbaru yang stabil, ASP.NET Core, Blazor Web App, C# dengan nullable reference types aktif.
- Database: Supabase (PostgreSQL terkelola) lewat EF Core + Npgsql.
- Valuasi stok: rata-rata bergerak (moving average) per barang per gudang, sesuai bagian 9.3 spesifikasi.
- Autentikasi: ASP.NET Core Identity di database yang sama. Bukan Supabase Auth. Row Level Security tidak dipakai sebagai penegak aturan bisnis.
- Arsitektur: modular monolith berlapis (`Wms.Domain`, `Wms.Application`, `Wms.Infrastructure`, `Wms.Web`) dengan proyek test terpisah.

## Instruksi utama

1. Baca `WMS_SPEC.md` secara lengkap sebelum mengedit kode.
2. Periksa repository: `*.sln`, `*.csproj`, `Directory.Packages.props`, `global.json`, versi SDK, paket, konfigurasi, migrations, dan fitur yang sudah ada.
3. Pertahankan stack dan kode yang sudah benar. Jika repository kosong, buat solusi sesuai bagian 29 spesifikasi.
4. Jangan membuat mockup atau halaman statis. Implementasikan alur end-to-end: UI → validasi → logika bisnis di server → PostgreSQL → hasil UI.
5. Jangan menghapus fitur yang ada atau menimpa file konfigurasi tanpa memahami fungsinya.
6. Kerjakan per tahap sesuai bagian 31 spesifikasi, mulai Tahap 0.
7. Jangan mengklaim fitur, build, atau test berhasil kecuali sudah dibuat/dijalankan dan hasilnya diperiksa.
8. Tidak ada data hardcoded sebagai pengganti database. Seed hanya untuk development/demo.
9. Semua tombol, menu, filter, form, ekspor, dan aksi yang tampil harus berfungsi. Fitur di fase berikutnya disembunyikan atau ditandai jelas "belum tersedia".
10. Semua operasi penting punya validasi server-side, pemeriksaan permission, penanganan error, dan audit trail.
11. Jangan simpan secret di source code. Semua secret (connection string Supabase, service key, dll.) lewat user-secrets/environment variable. Sediakan `.env.example` atau `appsettings.Example.json` dengan placeholder.
12. Perubahan stok wajib lewat ledger, validasi stok tersedia, idempotency key, dan transaksi database dengan penguncian/atomic update. Stok negatif dilarang dan dijaga `CHECK` constraint.
13. Transaksi yang sudah diposting hanya dapat dibatalkan/dikoreksi lewat movement lawan yang tercatat, bukan dihapus.
14. Role aplikasi database tidak boleh memiliki `UPDATE`/`DELETE` pada tabel ledger dan audit.
15. Simpan semua timestamp UTC (`timestamptz`), tampilkan dalam `Asia/Jakarta`, locale `id-ID`.

## Aturan kerja bertahap (gate wajib)

- Kerjakan satu tahap sekaligus. Setelah tahap selesai, berhenti dan kirim laporan. Jangan lanjut ke tahap berikutnya sebelum pemilik produk menjawab "lanjut".
- Untuk keputusan teknis kecil, pilih default aman dan dokumentasikan di `docs/decisions.md`.
- Berhenti dan tanyakan (satu pertanyaan fokus per keputusan) bila menyangkut: perubahan scope, aturan stok atau valuasi, keamanan, biaya/lisensi, atau kompatibilitas Supabase yang tidak dapat diverifikasi.
- Jangan menambah library berlisensi komersial tanpa mencatat alasan dan alternatifnya.

## Tahap 0 — Audit dan rencana

- Audit repository dan catat stack, versi, pola.
- Verifikasi koneksi Npgsql ke Supabase: direct, session pooler, atau transaction pooler. Rekomendasi awal: direct/session untuk aplikasi server, migrations hanya lewat direct/session. Catat hasil verifikasi.
- Cocokkan estimasi, asumsi kebijakan (bagian 26.1), dan target non-fungsional (bagian 30) dengan temuan.
- Hasilkan: daftar fitur sudah/belum ada, rencana tahap, risiko, pertanyaan terbuka.
- Berhenti dan tunggu persetujuan.

## Tahap 1 — Fondasi

Solusi berlapis, layout Blazor responsif + sidebar, Identity (cookie aman, lockout, rate limiting), policy-based authorization dengan permission terpusat, EF Core + Npgsql + migrations, FluentValidation, error handling (`ProblemDetails`), audit interceptor, Serilog dengan correlation ID, health check, `IClock`/`IUserContext`, seed development, CI dasar.

## Tahap 2 — Master data

CRUD, validasi, pencarian, filter, sorting, pagination server-side, status aktif/nonaktif, unique index untuk: pengguna dan role, gudang dan lokasi, kategori, satuan, barang (SKU/barcode), pemasok, pelanggan. Pembatasan gudang per pengguna dengan global query filter. Tidak ada hapus permanen untuk master yang direferensikan.

## Tahap 3 — Ledger, valuasi, transaksi stok

- Tabel `inventory_balances`, `inventory_valuations`, `stock_movements` (append-only), `idempotency_keys`, `document_sequences`.
- Barang masuk dan barang keluar dengan status draft → posted → reversed, konfirmasi posting, validasi stok tersedia, penomoran dokumen dengan row lock.
- Valuasi rata-rata bergerak sesuai bagian 9.3 (masuk, keluar, pembatalan, stok nol, presisi dan pembulatan).
- Test konkurensi (Testcontainers PostgreSQL): dua posting bersamaan atas stok terakhir hanya satu yang berhasil; retry dengan idempotency key sama tidak menggandakan movement.
- Job/query rekonsiliasi saldo vs ledger.

## Tahap 4 — Dashboard dan laporan dasar

Dashboard dari data aktual (agregasi di database), stok saat ini, kartu stok, nilai persediaan (menyebut metode rata-rata dan tanggal), viewer audit log. Bedakan saldo saat ini vs historis.

## Tahap 5 — Workflow operasional [SELESAI]

PO + approval + penerimaan parsial, retur, transfer antar-gudang (status `in_transit`), stock opname (snapshot, recount, selisih nilai, approval, posting), kejadian hilang/rusak. Pemisahan tugas: pembuat tidak menyetujui dokumennya sendiri. Audit untuk semua posting, pembatalan, approval.

## Tahap 6 — Notifikasi dan ekspor [SELESAI]

Background job (Hangfire atau Quartz.NET), notifikasi in-app dengan deduplikasi, ekspor CSV/XLSX/PDF mengikuti filter dan izin, laporan besar lewat streaming/job, ekspor tercatat di audit log.

## Tahap 7 — Fitur lanjutan [SELESAI]

Batch/lot + kedaluwarsa + FEFO, barcode/QR (input manual sebagai fallback), impor CSV/XLSX dengan preview dan validasi, putaway/picking, sales order + reservasi, REST API eksternal, backup/export data. Jangan mengklaim dukungan scanner atau email eksternal sebelum diuji.

## Tahap 8 — Pengujian akhir dan dokumentasi [SELESAI]

Lengkapi test untuk: autentikasi dan RBAC, duplikasi SKU/barcode, barang masuk/keluar, penolakan stok negatif, idempotency, pembatalan/koreksi dengan ledger dan audit, penerimaan parsial PO, transfer tanpa duplikasi, opname dan approval, valuasi rata-rata, filter dan ekspor laporan, pembatasan akses antar-gudang, state loading/empty/error/success. Dokumentasikan instalasi, env, seed, role, workflow, batasan, dan deployment (termasuk batas paket Supabase dan backup).

## Standar implementasi

- Aturan bisnis di Domain/Application, bukan di komponen Razor.
- Semua mutasi memeriksa sesi, permission, status dokumen, dan stok di server.
- Komponen UI reusable dan konsisten; dialog konfirmasi untuk aksi berisiko.
- Hindari duplikasi kode, kode mati, dan TODO yang disamarkan sebagai fitur jadi.
- Jangan tampilkan stack trace, secret, atau data sensitif di error maupun log.
- Gunakan index untuk identifier unik dan filter umum; pagination dan filter server-side.
- Dokumentasikan keputusan teknis dan batasan.

## Pemeriksaan wajib sebelum melapor

Jalankan yang tersedia dan laporkan hasil aktual (command + output ringkas):

```
dotnet restore
dotnet build --no-restore -warnaserror
dotnet test --no-build
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
```

Jika ada command gagal karena environment atau dependensi eksternal, tulis command dan pesan error persisnya serta langkah perbaikan. Jangan menyatakan lulus jika tidak dijalankan.

## Format laporan di akhir tiap tahap

1. Tahap yang selesai.
2. Fitur yang berfungsi end-to-end.
3. File penting yang dibuat/diubah.
4. Command yang dijalankan beserta hasil aktual.
5. Masalah tersisa dan alasannya.
6. Keputusan yang dibuat dan pertanyaan terbuka.
7. Langkah berikutnya. Lalu berhenti dan tunggu "lanjut".

## Kriteria akhir

Jangan menyatakan proyek selesai sebelum semua fitur Fase 1–3 dalam `WMS_SPEC.md` terimplementasi atau secara eksplisit dicatat belum selesai beserta alasan. Utamakan sistem yang konsisten dan dapat diuji daripada banyak halaman yang hanya tampak selesai.

Mulai sekarang: kerjakan Tahap 0 saja, laporkan, lalu tunggu instruksi.
