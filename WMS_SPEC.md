# Warehouse Management System (WMS) — Spesifikasi Produk dan Rencana Implementasi (.NET)

Versi: 2.0 (revisi dari versi Next.js + MongoDB)
Dokumen ini adalah sumber kebenaran untuk analisis, desain, implementasi, pengujian, dan dokumentasi. Fitur dianggap selesai hanya jika memenuhi Definisi Selesai (bagian 33).

## 1. Tujuan dan ruang lingkup

Bangun aplikasi web WMS untuk mengelola master barang, stok, pergerakan barang, pemasok, pelanggan, pembelian, penjualan/pengeluaran, lokasi penyimpanan, multi-gudang, stock opname, notifikasi, pengguna, audit log, valuasi stok, serta laporan yang dapat difilter dan diekspor.

### 1.1 Prioritas (MoSCoW) dan fase rilis

| Fase | Prioritas | Isi |
|---|---|---|
| Fase 1 — MVP | Must | Fondasi, autentikasi, RBAC, master data, stock ledger, saldo stok, barang masuk, barang keluar, valuasi rata-rata, dashboard dasar, laporan stok dan kartu stok, audit log |
| Fase 2 — Operasional | Should | Purchase order + penerimaan parsial, retur, transfer antar-gudang, stock opname, barang hilang/rusak, approval, notifikasi, ekspor CSV/XLSX/PDF |
| Fase 3 — Lanjutan | Could | Batch/lot + kedaluwarsa + FEFO, barcode/QR, impor CSV/XLSX, putaway/picking, sales order + reservasi, REST API eksternal, backup/export data |
| Di luar lingkup | Won't (saat ini) | Integrasi akuntansi, integrasi kurir, perangkat keras scanner khusus, aplikasi mobile native, multi-tenant antar-perusahaan, manufaktur/BOM |

Fase berikutnya tidak boleh merusak konsistensi fase sebelumnya. Fitur yang belum masuk fase aktif tidak boleh muncul sebagai menu aktif; tandai jelas "belum tersedia" atau sembunyikan.

### 1.2 Estimasi awal (solo developer, perlu divalidasi setelah Tahap 0)

- Fase 1: 6–8 minggu
- Fase 2: 6–8 minggu
- Fase 3: 6–10 minggu

### 1.3 Risiko utama

- Konsistensi stok pada transaksi bersamaan → mitigasi: constraint database, row lock/atomic update, idempotency key, test konkurensi.
- Scope creep → mitigasi: MoSCoW di atas, perubahan scope dicatat.
- Ketergantungan layanan Supabase (koneksi, pooler, limit paket) → mitigasi: akses data hanya lewat Npgsql/EF Core sehingga dapat dipindah ke PostgreSQL biasa.
- Lisensi library ekspor/PDF → mitigasi: cek lisensi sebelum dipakai, catat di dokumentasi.
- Kompleksitas valuasi rata-rata saat pembatalan/koreksi → mitigasi: aturan eksplisit di bagian 9.3 dan unit test.

## 2. Prinsip dan aturan umum

1. Responsif untuk desktop, tablet, dan ponsel.
2. Semua data operasional tersimpan di database, bukan state UI atau data dummy.
3. Semua perubahan stok melalui transaksi stok tercatat dan dapat ditelusuri (stock ledger).
4. Stok tersedia tidak boleh negatif. Dijaga di logika bisnis dan `CHECK` constraint database.
5. Transaksi stok yang sudah diposting tidak dihapus permanen. Gunakan pembatalan atau transaksi koreksi dengan alasan dan audit trail.
6. Perhitungan stok konsisten dan aman terhadap transaksi bersamaan.
7. Locale default `id-ID`, zona waktu tampilan `Asia/Jakarta`, mata uang default IDR. Simpan semua timestamp dalam UTC, konversi hanya di lapisan tampilan.
8. Setiap halaman punya state loading, empty, error, success, dan konfirmasi yang sesuai.
9. Daftar besar memakai pagination dan filter server-side.
10. Tidak ada tombol atau menu palsu.
11. Validasi di UI dan server. Server adalah otoritas.
12. Password tidak pernah disimpan plaintext.
13. Data seed hanya untuk development/demo.
14. Least privilege.
15. Operasi multi-record memakai transaksi database.
16. Tanggal bisnis (mis. tanggal dokumen) dan timestamp sistem (mis. dibuat pada) dibedakan dan disimpan terpisah.

## 3. Stack teknologi

Gunakan stack berikut kecuali repository sudah memiliki stack mapan. Jangan mengganti stack yang ada tanpa alasan teknis kuat.

| Lapisan | Pilihan |
|---|---|
| Runtime | .NET LTS terbaru yang stabil (verifikasi versi saat Tahap 0), C# dengan nullable reference types aktif |
| Web | ASP.NET Core, Blazor Web App |
| Komponen UI | MudBlazor (alternatif: Radzen), CSS isolation per komponen |
| Database | Supabase (PostgreSQL terkelola) |
| Akses data | Entity Framework Core + Npgsql, migrations EF Core |
| Autentikasi | ASP.NET Core Identity (tabel di PostgreSQL yang sama), cookie aman, lockout |
| Otorisasi | Policy-based authorization + permission terpusat |
| Validasi | FluentValidation untuk input; DataAnnotations hanya bila sederhana |
| Background job | Hangfire (storage PostgreSQL) atau Quartz.NET |
| Logging | Serilog, structured logging |
| Ekspor | CsvHelper (CSV), ClosedXML (XLSX), QuestPDF (PDF). Cek lisensi masing-masing sebelum dipakai |
| Barcode/QR | ZXing.Net (generate), scan via input keyboard-wedge atau kamera (JS interop) |
| Testing | xUnit, FluentAssertions, Testcontainers untuk PostgreSQL, bUnit untuk komponen Blazor, Playwright for .NET untuk e2e |
| Version control | Git |

Catatan:
- Jangan menambah library berbayar/berlisensi komersial tanpa mencatat keputusan dan alternatifnya.
- Hindari upgrade major dependency tanpa kebutuhan.
- Periksa `*.sln`, `*.csproj`, `Directory.Packages.props`, dan `global.json` sebelum mengubah apa pun.

### 3.1 Aturan khusus Supabase

- Supabase dipakai sebagai PostgreSQL terkelola. Logika bisnis, autentikasi, dan otorisasi tetap di aplikasi .NET. Jangan bergantung pada Supabase Auth maupun Row Level Security untuk aturan bisnis inti.
- Connection string dan semua secret hanya lewat environment variable / user-secrets. Jangan pernah menaruh service role key atau connection string di repository, di klien Blazor WebAssembly, atau di log.
- Gunakan koneksi langsung atau session pooler untuk aplikasi server yang berjalan lama. Jika memakai transaction pooler, verifikasi kompatibilitas Npgsql (prepared statement, reset koneksi) pada dokumentasi Supabase terbaru dan catat keputusan.
- Jalankan EF Core migrations lewat koneksi langsung/session, bukan lewat transaction pooler.
- Penyimpanan lampiran (foto, dokumen) memakai Supabase Storage atau storage kompatibel S3 lewat abstraksi `IFileStorage`. Simpan hanya metadata dan path di database. Batasi tipe dan ukuran file, pindai ekstensi dan MIME.
- Atur role database aplikasi dengan hak minimum. Role aplikasi tidak boleh memiliki `UPDATE`/`DELETE` pada tabel ledger dan audit (lihat 9.4 dan 24).
- Dokumentasikan batas paket Supabase (jumlah koneksi, ukuran database, backup) di dokumentasi deployment.

## 4. Peran pengguna dan hak akses

RBAC dengan permission terpusat (kode permission seperti `stock.in.create`, `stock.in.post`, `stock.adjustment.approve`).

### 4.1 Super Admin
- Semua fitur. Kelola pengguna, role, konfigurasi, gudang, master data.
- Lihat audit log. Kelola backup/export bila diizinkan.

### 4.2 Admin Gudang
- Kelola barang, kategori, satuan, pemasok, pelanggan, lokasi rak, transaksi gudang.
- Buat PO dan permintaan transfer. Lakukan stock opname. Lihat laporan operasional.

### 4.3 Petugas Gudang
- Lihat barang dan lokasi. Catat barang masuk/keluar sesuai izin. Pindai barcode/QR bila tersedia.
- Lihat transaksi sendiri atau sesuai izin.
- Tidak boleh mengubah role, konfigurasi sensitif, atau menyetujui transaksi buatan sendiri (pemisahan tugas).

### 4.4 Supervisor/Manajer
- Lihat dashboard dan laporan. Setujui/tolak penyesuaian stok, transfer, PO, dan transaksi yang memerlukan persetujuan.
- Lihat selisih opname dan audit log sesuai izin.
- Tidak boleh menyetujui transaksi yang ia buat sendiri.

### 4.5 Auditor (opsional)
- Akses baca saja ke laporan, transaksi, opname, audit log yang diizinkan.

Aturan penegakan:
- Matriks izin terpusat dalam kode dan seed, dapat diubah Super Admin.
- Pemeriksaan izin dilakukan di setiap handler/service/endpoint, bukan hanya menyembunyikan menu atau tombol.
- Pembatasan gudang per pengguna diterapkan lewat EF Core global query filter dan pemeriksaan eksplisit pada mutasi.

## 5. Navigasi utama

Sidebar responsif (collapse di layar kecil), breadcrumb, judul halaman, pencarian bila relevan, indikator notifikasi. Menu mengikuti role dan fase rilis:

1. Dashboard
2. Data Barang
3. Kategori Barang
4. Satuan Barang
5. Barang Masuk
6. Barang Keluar
7. Retur Barang (Fase 2)
8. Transfer Antar-Gudang (Fase 2)
9. Stock Opname (Fase 2)
10. Barang Hilang dan Rusak (Fase 2)
11. Pemasok
12. Pelanggan
13. Purchase Order (Fase 2)
14. Gudang dan Lokasi Rak
15. Notifikasi (Fase 2)
16. Laporan
17. Pengguna dan Hak Akses
18. Audit Log
19. Pengaturan

## 6. Dashboard

Semua angka dihitung dari database, tanpa hardcode.

- Total SKU aktif.
- Total kuantitas stok per satuan (jangan menjumlahkan satuan berbeda tanpa konteks).
- Total nilai persediaan (rata-rata) per gudang dan keseluruhan.
- Jumlah barang stok rendah dan habis.
- Transaksi barang masuk dan keluar hari ini.
- Jumlah barang hilang/rusak pada rentang terpilih (Fase 2).
- PO menunggu penerimaan/persetujuan (Fase 2).
- Transfer menunggu persetujuan/penerimaan (Fase 2).
- Aktivitas stok terbaru.
- Grafik masuk/keluar per hari/minggu/bulan/tahun.
- Ringkasan stok per gudang/kategori.
- Barang paling sering keluar dan barang tidak bergerak (ambang hari dapat dikonfigurasi).

Fitur: filter rentang tanggal dan gudang, tautan dari kartu/grafik ke daftar terfilter, empty state informatif. Query dashboard harus memakai agregasi di database dan, bila perlu, cache jangka pendek yang terdokumentasi.

## 7. Master data barang

Field minimum:
- ID internal (GUID atau bigint, konsisten di seluruh sistem).
- SKU unik, barcode/QR unik (opsional).
- Nama, deskripsi, kategori, merek (opsional).
- Satuan dasar, foto (opsional).
- Stok minimum, stok maksimum (opsional).
- Metode pelacakan: tanpa batch, batch, batch + kedaluwarsa.
- Status aktif/nonaktif.
- Created by/at, updated by/at.

Fitur:
- Tambah, detail, edit, nonaktifkan.
- Pencarian SKU, barcode, nama, merek.
- Filter kategori, status, stok rendah, gudang. Sorting dan pagination server-side.
- Impor CSV/XLSX dengan preview, validasi per baris, laporan kesalahan (Fase 3).
- Ekspor daftar barang.
- Pembuatan/unduh label barcode/QR (Fase 3).
- Cegah duplikasi SKU/barcode (unique index, case-insensitive untuk SKU).
- Barang yang punya transaksi tidak boleh dihapus; hanya nonaktif.
- Perubahan metode pelacakan batch pada barang yang sudah bertransaksi dibatasi (butuh proses migrasi aman).

## 8. Kategori, satuan, pelanggan

### Kategori
- Nama unik, deskripsi opsional, status aktif.
- Tidak boleh dihapus jika dipakai; pindahkan barang atau nonaktifkan.

### Satuan
- Nama dan singkatan (pcs, box, kg, liter).
- Konversi satuan: tidak didukung pada Fase 1. Jika didukung kemudian, definisikan satuan dasar dan faktor konversi eksplisit tersimpan di database.
- Satuan dasar barang yang sudah bertransaksi tidak dapat diubah.

### Pelanggan
- Kode pelanggan unik, nama, kontak, email, telepon, alamat, nomor pajak opsional, catatan, status aktif.
- Dipakai sebagai tujuan pada barang keluar bertipe penjualan/pengiriman, retur dari pelanggan, dan sales order (Fase 3).
- CRUD dengan soft delete/nonaktif.

## 9. Persediaan, ledger, dan valuasi

### 9.1 Saldo stok
Saldo disimpan per kombinasi barang + gudang + lokasi + batch (bila batch dipakai) + status stok.

- `on_hand`: stok fisik tercatat.
- `reserved`: dialokasikan (aktif mulai Fase 3 bila reservasi diimplementasikan).
- `available = on_hand - reserved`.
- Status stok: `available`, `quarantine`, `damaged`, `in_transit`. Stok non-`available` tidak ikut dihitung sebagai stok tersedia untuk pengeluaran normal.

### 9.2 Stock ledger (pergerakan)
Tabel `stock_movements` bersifat append-only. Setiap record memuat:
- Nomor transaksi unik, tipe pergerakan.
- Barang, gudang, lokasi, batch, status stok.
- Kuantitas positif dan arah (`in`/`out`) eksplisit.
- Saldo sebelum dan sesudah (dicatat dalam transaksi yang sama).
- Biaya satuan dan nilai pergerakan (lihat 9.3).
- Referensi dokumen sumber (tipe + ID), alasan/keterangan.
- Pengguna dan timestamp (UTC).
- Status: `posted`, `reversed`. Pembatalan membuat movement lawan yang menautkan ke movement asal; movement asal tidak diubah isinya selain penanda `reversed_by`.

### 9.3 Valuasi: rata-rata bergerak (moving average)

Aturan:
- Biaya rata-rata dihitung per barang per gudang (tidak per batch). Disimpan di `inventory_valuations` (`on_hand_qty`, `on_hand_value`, `avg_cost`).
- Barang masuk dari pembelian: `avg_cost_baru = (on_hand_value + qty_masuk × biaya_satuan) / (on_hand_qty + qty_masuk)`. Biaya satuan berasal dari harga PO/penerimaan (termasuk biaya tambahan hanya bila kebijakan menetapkannya).
- Barang keluar: dinilai pada `avg_cost` saat posting. `avg_cost` tidak berubah, `on_hand_value` berkurang sebesar `qty × avg_cost`.
- Transfer: keluar dari gudang asal pada `avg_cost` asal; masuk ke gudang tujuan dengan biaya satuan yang sama dengan nilai keluar (rata-rata tujuan dihitung ulang).
- Penyesuaian positif (opname/koreksi): dinilai pada `avg_cost` saat ini. Jika `on_hand_qty = 0`, gunakan `avg_cost` terakhir; jika belum pernah ada, wajib input biaya manual dengan alasan.
- Retur ke pemasok: dinilai pada biaya satuan dokumen asal bila dapat ditelusuri, selain itu pada `avg_cost` saat ini (dicatat di movement).
- Retur dari pelanggan: dinilai pada biaya satuan transaksi keluar asal.
- Pembatalan: movement lawan memakai biaya satuan movement asal. Jika pembatalan akan membuat stok negatif, tolak. `avg_cost` dihitung ulang dari `on_hand_value` dan `on_hand_qty` setelah pembatalan.
- Saat `on_hand_qty` menjadi 0, `on_hand_value` harus 0 (selisih pembulatan dikoreksi lewat movement penyesuaian nilai dengan alasan "pembulatan").
- Presisi: kuantitas `numeric(18,4)`, biaya satuan dan nilai `numeric(18,4)`, mata uang IDR. Aturan pembulatan ditetapkan satu kali dan diuji.
- Laporan nilai persediaan menyebutkan metode (rata-rata bergerak) dan tanggal perhitungan.
- Dokumentasikan bahwa nilai historis direkonstruksi dari ledger (kolom biaya dan nilai per movement) atau snapshot periodik sah.

### 9.4 Konsistensi dan konkurensi
- Semua posting stok berjalan dalam satu transaksi database: kunci baris saldo (`SELECT ... FOR UPDATE` atau `UPDATE ... WHERE on_hand - @qty >= 0` atomik), tulis movement, perbarui saldo dan valuasi, tulis audit.
- Kunci baris diambil dengan urutan deterministik (mis. urut ID barang, lalu lokasi) untuk mencegah deadlock pada dokumen multi-baris.
- `CHECK (on_hand >= 0)` dan `CHECK (reserved >= 0 AND reserved <= on_hand)` pada tabel saldo.
- Optimistic concurrency token (`xmin` atau kolom versi) pada dokumen yang dapat diedit.
- Idempotency: setiap perintah posting membawa `idempotency_key`. Tabel `idempotency_keys` dengan unique constraint (pengguna, operasi, kunci). Retry dengan kunci sama mengembalikan hasil sebelumnya, bukan membuat transaksi baru. Tombol UI nonaktif saat proses berjalan.
- Ledger tidak dapat diubah: cabut `UPDATE`/`DELETE` untuk role aplikasi dan/atau pasang trigger yang menolak perubahan.
- Rekonsiliasi: sediakan job/query terjadwal yang membandingkan saldo dengan jumlah movement; selisih membuat notifikasi dan audit.

## 10. Barang masuk

Sumber: penerimaan dari pemasok/PO, retur dari pelanggan/unit internal, penyesuaian disetujui, transfer masuk.

Field:
- Nomor penerimaan otomatis unik, tanggal dokumen, timestamp posting.
- Gudang dan lokasi tujuan.
- Pemasok, nomor PO bila relevan.
- Daftar baris: barang, kuantitas, satuan, biaya satuan, batch, tanggal kedaluwarsa (bila relevan).
- Lampiran opsional, catatan, pembuat, penerima.
- Status.

Alur status:

| Dari | Ke | Oleh | Syarat |
|---|---|---|---|
| draft | pending_approval | pembuat | Hanya bila kebijakan approval mewajibkan |
| draft / pending_approval | posted | pembuat atau approver | Validasi lolos, konfirmasi, idempotency key |
| draft | cancelled | pembuat | Alasan |
| posted | reversed | sesuai izin | Alasan wajib, membuat movement lawan, audit |

Aturan:
- Kuantitas > 0. Konfirmasi sebelum posting. Stok bertambah tepat satu kali.
- Penerimaan parsial terhadap PO. Cegah penerimaan melebihi kuantitas pesanan tanpa izin khusus.
- Bukti penerimaan dapat dicetak/diekspor.
- Koreksi hanya lewat pembatalan/koreksi tercatat.

## 11. Barang keluar

Tipe: pemakaian internal, penjualan/pengiriman ke pelanggan, transfer keluar, penghapusan stok hilang/rusak setelah persetujuan.

Field: nomor pengeluaran unik, tanggal, gudang dan lokasi sumber, tujuan (pelanggan/departemen), daftar baris, referensi permintaan, alasan, pembuat, approver bila berlaku, status.

Alur status: draft → (pending_approval) → posted; draft → cancelled; posted → reversed (dengan alasan). Aturan sama seperti bagian 10.

Aturan:
- Validasi stok tersedia di gudang/lokasi/batch sumber. Tolak jika melebihi.
- Cegah double submission (idempotency).
- Bukti pengeluaran dapat dicetak. Riwayat dan filter tersedia.
- Biaya pengeluaran memakai rata-rata (9.3).

## 12. Barang hilang dan rusak (Fase 2)

Kategori kejadian: hilang, rusak, kedaluwarsa, karantina/tidak layak pakai, penyebab lain (dapat dikonfigurasi). Jangan menggabungkan hilang dan rusak.

Field: nomor laporan, barang, gudang, lokasi, batch, kuantitas, tanggal ditemukan, kategori dan alasan, lampiran opsional, pelapor, status, catatan pemeriksaan dan tindakan.

Status: reported → under_investigation → approved / rejected → resolved.

Aturan:
- Laporan tidak mengubah stok sebelum disetujui dan diposting.
- Saat disetujui: catat movement eksplisit (pindah ke status `quarantine`/`damaged` atau pengurangan stok untuk hilang).
- Laporan tren hilang/rusak per barang, gudang, penyebab, periode.

## 13. Retur barang (Fase 2)

Dukung retur ke pemasok dan retur dari pelanggan/unit pemakai.

Catat dokumen asal, barang, jumlah, alasan, kondisi, status persetujuan, movement yang dihasilkan. Cegah jumlah retur melebihi jumlah yang dapat diretur dari dokumen asal (tanpa izin khusus). Barang rusak dari retur masuk status `quarantine`/`damaged`, bukan `available`.

Status: draft → pending_approval → approved → posted; draft/pending → rejected/cancelled.

## 14. Transfer antar-gudang (Fase 2)

Alur:
1. Buat permintaan (gudang asal, tujuan, barang, kuantitas, alasan).
2. Persetujuan bila diwajibkan.
3. Kirim: stok keluar dari gudang asal ke status `in_transit`.
4. Gudang tujuan menerima seluruh/sebagian.
5. Selisih/kerusakan dicatat sebagai kejadian.
6. Selesai dengan audit trail.

| Status | Berikutnya | Catatan |
|---|---|---|
| draft | pending_approval, cancelled | |
| pending_approval | approved, rejected | Approver bukan pembuat |
| approved | in_transit, cancelled | Posting pengiriman mengurangi stok asal |
| in_transit | partially_received, received | Stok tujuan bertambah hanya saat penerimaan |
| partially_received | partially_received, received | Sisa tidak terkirim ditangani lewat selisih |

Aturan: gudang asal ≠ tujuan; validasi stok tersedia di asal; cegah penerimaan ganda; jumlah diterima tidak boleh melebihi jumlah dikirim; `in_transit` ikut dalam laporan nilai persediaan sebagai baris terpisah.

## 15. Stock opname (Fase 2)

- Sesi berdasarkan gudang, kategori, lokasi, atau pilihan barang.
- Tentukan cut-off dan petugas. Ambil snapshot saldo sistem saat sesi dimulai.
- Selama sesi aktif pada cakupan itu, transaksi stok baru pada barang tercakup mengikuti kebijakan yang dikonfigurasi (blokir atau izinkan dengan penyesuaian selisih terhadap snapshot). Default: blokir pengeluaran pada lokasi yang sedang dihitung.
- Input hasil hitung, recount, catatan.
- Selisih kuantitas dan nilai (memakai `avg_cost`).
- Persetujuan supervisor; posting penyesuaian ke ledger hanya setelah disetujui.
- Simpan snapshot, hasil hitung, selisih, alasan, pelaksana, pemeriksa, timestamp. Hasil hitung sebelumnya tidak ditimpa.
- Ekspor lembar hitung dan laporan hasil.

Status sesi: draft → counting → review → pending_approval → approved → posted; atau cancelled.

## 16. Notifikasi dan peringatan (Fase 2)

Pusat notifikasi dalam aplikasi untuk: stok di bawah minimum, stok habis, mendekati kedaluwarsa, PO terlambat, transfer/penyesuaian/PO menunggu tindakan, kejadian hilang/rusak menunggu tindak lanjut, kegagalan impor, selisih rekonsiliasi ledger.

- Dibuat oleh background job dan oleh event domain.
- Daftar, filter kategori/status, tandai dibaca, tandai semua dibaca, klik membuka record terkait.
- Deduplikasi: satu notifikasi aktif per kondisi per entitas (kunci dedup), dibuat ulang hanya setelah kondisi pulih lalu terulang.
- Email/push opsional; pusat notifikasi dalam aplikasi tetap wajib berfungsi.

## 17. Pemasok

Kode unik, nama perusahaan, kontak person, email, telepon, alamat, nomor pajak opsional, catatan, status aktif/nonaktif. CRUD dengan soft delete, pencarian dan filter, riwayat PO dan penerimaan, validasi email/telepon wajar, pembatasan tampilan kontak sesuai izin.

## 18. Purchase order (Fase 2)

Field: nomor PO unik, pemasok, tanggal dibuat, perkiraan tiba, gudang tujuan, baris (barang, kuantitas, satuan, harga, pajak/biaya opsional), catatan, lampiran, pembuat, approver, status.

| Status | Berikutnya |
|---|---|
| draft | pending_approval, cancelled |
| pending_approval | approved, rejected |
| approved | ordered, cancelled |
| ordered | partially_received, received, cancelled |
| partially_received | partially_received, received |

Aturan:
- Stok tidak bertambah saat PO dibuat/disetujui; hanya saat penerimaan diposting.
- Sisa kuantitas dan status dihitung otomatis dari penerimaan terposting.
- Cetak/ekspor PO. Perubahan PO yang sudah ordered lewat proses perubahan tercatat.
- Harga PO menjadi biaya satuan untuk valuasi rata-rata saat penerimaan.

## 19. Gudang dan lokasi rak

Gudang: kode unik, nama, alamat, penanggung jawab, status.
Hierarki: Gudang > Area/Zone > Rak > Bin. Kode lokasi unik dalam cakupan gudang.

- Barang ditempatkan di lokasi tertentu. Perpindahan lokasi internal memakai movement.
- Gudang/lokasi nonaktif tidak boleh dipakai transaksi baru.
- Tampilkan saldo per gudang, lokasi, batch.
- Putaway (saran lokasi) dan picking (daftar ambil) masuk Fase 3. Hingga itu, lokasi dipilih manual.

## 20. Multi-gudang

- Semua query stok dan transaksi menyertakan konteks gudang bila relevan.
- Pengguna dapat dibatasi ke gudang tertentu; dashboard dan laporan mengikuti.
- Transfer lewat workflow khusus, bukan edit saldo manual.
- Tidak boleh ada kebocoran data antar-gudang. Uji dengan test otorisasi.

## 21. Batch, lot, kedaluwarsa (Fase 3)

Nomor batch, tanggal produksi, kedaluwarsa, kuantitas per batch, lokasi dan status batch, filter hampir kedaluwarsa/kedaluwarsa, saran FEFO, blokir batch kedaluwarsa dari pengeluaran normal sesuai konfigurasi. Tidak diwajibkan untuk semua barang.

## 22. Barcode dan QR (Fase 3)

SKU tetap identitas utama. Barcode/QR untuk membuka detail dan mempercepat pencatatan masuk/keluar/opname via keyboard-wedge atau kamera (jika didukung perangkat). Input manual selalu tersedia. Tampilkan barang yang cocok sebelum konfirmasi. Jangan mengklaim dukungan perangkat keras yang belum diuji.

## 23. Laporan dan ekspor

Laporan (data aktual database, filter relevan):
- Stok saat ini; stok per gudang/lokasi/batch.
- Nilai persediaan (rata-rata).
- Kartu stok/ledger per barang.
- Barang masuk, keluar, hilang, rusak/kedaluwarsa, retur, transfer.
- PO dan penerimaan, opname dan selisih.
- Stok minimum/habis, paling sering keluar, tidak bergerak.
- Aktivitas pengguna/audit sesuai izin.

Filter: hari ini, kemarin, minggu ini/lalu, bulan ini/lalu, tahun ini, rentang kustom (berdasarkan zona `Asia/Jakarta`, dikonversi ke UTC untuk query); gudang, kategori, barang, pemasok, pelanggan, status, tipe transaksi; pencarian dan pagination.

Ekspor: CSV, XLSX, PDF, dan cetak halaman rapi.

Aturan:
- Ekspor mengikuti filter dan izin aktif. Nama file memuat nama laporan dan tanggal/rentang.
- Total dan jumlah baris konsisten dengan filter.
- Bedakan jelas saldo saat ini dan saldo historis; saldo historis direkonstruksi dari ledger atau snapshot sah.
- Laporan besar diproses secara streaming atau lewat background job dengan unduhan setelah selesai; jangan membebani request web.
- Ekspor dicatat di audit log.

## 24. Audit log

Catat: login berhasil/gagal, perubahan master data, pembuatan/posting/pembatalan/koreksi transaksi, perubahan role/izin, persetujuan/penolakan, impor/ekspor, perubahan pengaturan.

Record minimum: user ID dan identitas, action, entity type dan ID, timestamp UTC, ringkasan before/after yang aman, alasan bila diwajibkan, correlation ID.

Aturan: append-only (hak `UPDATE`/`DELETE` dicabut untuk role aplikasi); tidak menyimpan password, token, atau secret; akses dibatasi; implementasi melalui EF Core `SaveChanges` interceptor dan helper eksplisit untuk event domain; tetapkan kebijakan retensi (default dapat dikonfigurasi, terdokumentasi).

## 25. Pengguna dan autentikasi

- Login/logout aman dengan ASP.NET Core Identity; hash password bawaan Identity (PBKDF2) atau Argon2 lewat library terpelihara.
- Cookie `HttpOnly`, `Secure`, `SameSite` sesuai kebutuhan; anti-forgery aktif.
- Lockout dan rate limiting (middleware rate limiter bawaan ASP.NET Core) pada endpoint login dan reset password.
- Pesan error login tidak membocorkan keberadaan akun.
- Aktif/nonaktif akun; akun nonaktif tidak dapat membuat transaksi dan sesinya dicabut.
- Ubah password; reset password bila email tersedia.
- Seed akun demo hanya untuk development, didokumentasikan, tidak boleh aktif di production. Tidak ada kredensial default di production.

## 26. Pengaturan

Nama perusahaan dan logo; zona waktu dan format tanggal; format nomor transaksi; ambang stok minimum default; kebijakan approval; pengaturan batch/kedaluwarsa; konfigurasi notifikasi; preferensi laporan; satuan dan kategori; gudang aktif; konfigurasi impor/ekspor; kebijakan retensi audit.

### 26.1 Default kebijakan (dapat diubah, dicatat di audit log)

Nilai berikut adalah asumsi awal yang harus dikonfirmasi pemilik produk:

| Kebijakan | Default |
|---|---|
| Penyesuaian stok dari opname | Selalu butuh persetujuan supervisor |
| PO bernilai di atas Rp10.000.000 | Butuh persetujuan manajer |
| Barang keluar bertipe hilang/rusak/buang | Selalu butuh persetujuan |
| Barang masuk/keluar rutin | Tanpa persetujuan |
| Pembatal transaksi terposting | Butuh persetujuan supervisor dan alasan |
| Pembuat tidak boleh menyetujui dokumennya sendiri | Aktif |

### 26.2 Format nomor transaksi

Format default: `{PREFIX}-{YYYYMM}-{NNNNNN}` (mis. `GR-202610-000123`). Urutan dikelola tabel `document_sequences` per prefix dan periode, diambil dalam transaksi dengan row lock agar unik dan tanpa duplikasi. Prefix per tipe: `GR` penerimaan, `GI` pengeluaran, `PO`, `TR` transfer, `RT` retur, `SO` opname, `IN` kejadian.

## 27. UX/UI

- Profesional, bersih, konsisten; prioritaskan kecepatan kerja petugas gudang.
- Sidebar collapse; tipografi jelas, spacing konsisten, kontras aksesibel.
- Tabel dengan pencarian, filter, sorting, pagination server-side, status badge, aksi jelas.
- Form berkelompok, label eksplisit, validasi inline, ringkasan error.
- Dialog konfirmasi untuk posting, pembatalan, nonaktif, dan perubahan berisiko.
- Snackbar/toast untuk hasil operasi, tanpa menggantikan validasi penting.
- Empty state menjelaskan langkah berikutnya; skeleton untuk loading.
- Aksesibilitas keyboard, fokus terlihat, ARIA label.
- Blazor: pilih render mode secara sadar (default Interactive Server untuk aplikasi internal; WebAssembly/Auto hanya bila ada alasan), tangani reconnect circuit dengan pesan yang jelas, jangan menaruh secret atau akses data langsung di komponen klien.

## 28. Model data (PostgreSQL)

Nama tabel `snake_case`; sesuaikan dengan konvensi proyek.

- Identitas: `users`, `roles`, `user_roles`, `role_permissions`, `user_warehouses`
- Master: `warehouses`, `warehouse_locations`, `categories`, `units`, `products`, `suppliers`, `customers`
- Transaksi: `purchase_orders`, `purchase_order_lines`, `goods_receipts`, `goods_receipt_lines`, `stock_outs`, `stock_out_lines`, `returns`, `return_lines`, `stock_transfers`, `stock_transfer_lines`, `stock_incidents`
- Persediaan: `inventory_balances`, `inventory_valuations`, `stock_movements`, `batches`
- Opname: `stocktake_sessions`, `stocktake_lines`
- Sistem: `notifications`, `audit_logs`, `app_settings`, `document_sequences`, `idempotency_keys`, `attachments`, `background_job_runs`

Kriteria desain:
- Primary key konsisten; foreign key dengan perilaku hapus `RESTRICT` untuk data bertransaksi.
- Unique index: SKU, barcode, kode gudang, kode supplier, kode pelanggan, nomor dokumen, (pengguna, operasi, idempotency key).
- Index untuk filter umum: barang, gudang, tanggal, status, kategori, pemasok, nomor transaksi; indeks komposit untuk `stock_movements` (barang, gudang, waktu).
- Partial unique index untuk SKU/barcode aktif bila perlu.
- `CHECK` constraint untuk kuantitas dan status; `numeric` untuk kuantitas dan uang.
- Timestamp `timestamptz` (UTC).
- Saldo adalah materialized balance diperbarui atomik dalam transaksi yang sama dengan ledger; konsistensinya dibuktikan test dan job rekonsiliasi.
- Strategi nonaktif master: soft delete (`is_active`), tidak ada hapus permanen untuk data yang direferensikan.
- Migrations EF Core dalam repository; migrasi destruktif butuh catatan rollback.

## 29. Arsitektur dan struktur kode

Modular monolith berlapis (Clean Architecture ringan):

```
src/
  Wms.Domain          // entitas, value object, aturan bisnis murni, domain event
  Wms.Application     // use case/service, DTO, validator, interface (IFileStorage, IClock, dll.)
  Wms.Infrastructure  // EF Core, repository bila perlu, Identity, storage, jobs, export
  Wms.Web             // Blazor Web App, endpoint, komponen UI, DI composition root
tests/
  Wms.Domain.Tests
  Wms.Application.Tests
  Wms.Integration.Tests   // Testcontainers PostgreSQL
  Wms.E2E.Tests           // Playwright for .NET
docs/
```

Aturan:
- Aturan bisnis inti ada di Domain/Application, bukan di komponen Razor.
- Setiap mutasi memvalidasi input, sesi, izin, status dokumen, dan ketersediaan stok di server.
- Hindari library mediator/mapper berlisensi komersial tanpa keputusan tercatat; handler dan mapping eksplisit sudah cukup.
- Error ditangani konsisten (Result/exception terpetakan), tanpa membocorkan stack trace ke pengguna; gunakan `ProblemDetails` untuk endpoint HTTP.
- Konfigurasi lewat `IConfiguration`, user-secrets (development), environment variable (production). Sediakan `.env.example`/`appsettings.Example.json` tanpa rahasia asli.
- `IClock` (waktu) dan `IUserContext` diinjeksikan agar dapat diuji.
- Health check endpoint untuk database dan job.
- Dokumentasi setup lokal, migrasi, seed, test, build, dan deployment.

## 30. Persyaratan non-fungsional

| Aspek | Target awal (konfirmasi setelah Tahap 0) |
|---|---|
| Skala data | Hingga 50.000 SKU, 10 gudang, 5 juta baris ledger tanpa degradasi berarti |
| Performa | Halaman daftar < 2 detik (p95) pada skala di atas; posting transaksi < 1 detik untuk dokumen hingga 100 baris |
| Konkurensi | Minimal 50 pengguna aktif bersamaan |
| Ketersediaan | Target 99% jam kerja; maintenance terjadwal diumumkan |
| Backup | Backup database harian dan uji restore berkala; dokumentasikan RPO/RTO (usulan RPO ≤ 24 jam, RTO ≤ 4 jam) |
| Keamanan | OWASP ASVS dasar: input validation, anti-CSRF, header keamanan, dependency scanning (`dotnet list package --vulnerable`) |
| Observabilitas | Log terstruktur dengan correlation ID, health check, metrik dasar |
| Retensi | Ledger dan audit disimpan tanpa batas (default); lampiran sesuai kebijakan |
| Aksesibilitas | Navigasi keyboard dan kontras sesuai WCAG 2.1 AA untuk alur utama |
| Browser | Dua versi terakhir Chrome, Edge, Safari, Firefox |
| Bahasa | Bahasa Indonesia; teks disimpan dalam resource agar dapat dilokalisasi |

## 31. Rencana implementasi bertahap

### Tahap 0 — Audit repository dan perencanaan
- Periksa struktur solusi, versi .NET SDK, paket, pola kode, konfigurasi Supabase.
- Verifikasi kompatibilitas koneksi Npgsql dengan Supabase (direct/session/pooler) dan catat keputusan.
- Ringkas temuan, konfirmasi estimasi dan asumsi (bagian 26.1, 30).

### Tahap 1 — Fondasi (Fase 1)
- Solusi berlapis, layout Blazor, Identity, RBAC, koneksi database, migrations, FluentValidation, error handling, audit interceptor, serilog, `.env.example`, seed development, CI dasar (`dotnet build` + `dotnet test`).

### Tahap 2 — Master data (Fase 1)
- Pengguna/role, gudang/lokasi, kategori, satuan, barang, pemasok, pelanggan.

### Tahap 3 — Ledger, valuasi, transaksi stok (Fase 1)
- `inventory_balances`, `inventory_valuations`, `stock_movements`, barang masuk, barang keluar, riwayat pergerakan, idempotency, test konkurensi.

### Tahap 4 — Dashboard dan laporan dasar (Fase 1)
- Dashboard, stok saat ini, kartu stok, nilai persediaan, audit log viewer.

### Tahap 5 — Workflow operasional (Fase 2)
- PO + penerimaan parsial, retur, transfer, opname, kejadian hilang/rusak, approval.

### Tahap 6 — Notifikasi dan ekspor (Fase 2)
- Background job, notifikasi, ekspor CSV/XLSX/PDF, laporan lengkap.

### Tahap 7 — Fitur lanjutan (Fase 3)
- Batch/FEFO, barcode/QR, impor, putaway/picking, sales order/reservasi, REST API, backup/export.

### Tahap 8 — Pengujian akhir dan dokumentasi
- Lengkapi unit/integration/e2e, jalankan seluruh pemeriksaan, perbaiki, dokumentasikan.

Setiap akhir tahap adalah gate: laporan, demo, dan persetujuan pemilik produk sebelum lanjut.

## 32. Pengujian dan acceptance criteria

### Autentikasi dan izin
- Pengguna belum login tidak dapat mengakses halaman/endpoint terlindungi.
- Role tanpa izin ditolak di server meski memanggil endpoint langsung.
- Pengguna nonaktif tidak dapat membuat transaksi.
- Pembuat dokumen tidak dapat menyetujui dokumennya sendiri.

### Master data
- SKU/barcode duplikat ditolak.
- Barang dengan riwayat transaksi tidak dapat dihapus permanen.
- Pencarian, filter, pagination, validasi berjalan.

### Stok, ledger, valuasi
- Barang masuk diposting menambah stok tepat satu kali.
- Barang keluar mengurangi stok tepat satu kali; melebihi stok tersedia ditolak.
- Dua posting bersamaan atas stok terakhir: hanya satu berhasil (test konkurensi dengan Testcontainers).
- Retry dengan idempotency key sama tidak membuat transaksi/movement ganda.
- Pembatalan/koreksi membuat movement lawan dan audit log.
- Ledger tidak dapat diubah/dihapus oleh role aplikasi.
- Nilai rata-rata benar pada skenario: dua pembelian dengan harga berbeda, pengeluaran, pembatalan, transfer, penyesuaian, stok nol.
- Rekonsiliasi saldo = jumlah movement.
- Transfer tidak menambah stok tujuan sebelum penerimaan; penerimaan ganda ditolak.
- Opname baru mengubah stok setelah persetujuan/posting.
- Kejadian hilang/rusak mengikuti workflow dan tidak mengurangi stok diam-diam.

### Laporan
- Filter tanggal dan gudang menghasilkan data benar, termasuk batas hari pada zona `Asia/Jakarta`.
- CSV/XLSX/PDF berisi data yang sama dengan tampilan terfilter.
- Tidak ada kebocoran data antar-gudang.
- Dashboard sesuai data transaksi.

### UX dan stabilitas
- Semua tombol/aksi utama berfungsi; state loading, empty, error, success tersedia.
- Form tervalidasi di UI dan server.
- Layout dapat dipakai di desktop dan ponsel.
- `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`, dan pemeriksaan paket rentan berhasil; kegagalan dicatat dan diperbaiki.

## 33. Definisi selesai

Fitur selesai hanya jika:
- UI tersedia dan terhubung ke backend.
- Validasi UI dan server ada.
- Hak akses benar dan diuji.
- Data tersimpan dan dibaca dari PostgreSQL (Supabase).
- State loading, empty, error, success tersedia.
- Audit log dibuat untuk operasi penting.
- Tes relevan ditambahkan atau diperbarui dan dijalankan.
- Tidak ada tombol dummy, angka hardcoded, atau TODO yang disamarkan sebagai fitur selesai.
- Dokumentasi terkait diperbarui.

## 34. Instruksi untuk implementer

Baca dokumen ini sampai selesai sebelum mengubah kode. Kerjakan per tahap mulai Tahap 0. Setelah setiap tahap:
1. Ringkas file yang dibuat/diubah.
2. Sebutkan fitur yang benar-benar selesai.
3. Jalankan pemeriksaan yang tersedia dan laporkan hasil aktual.
4. Catat blocker dan keputusan yang belum ditetapkan.
5. Jangan mengklaim tes lulus jika tidak dijalankan.
6. Jangan berhenti pada mockup UI; implementasikan alur end-to-end.
7. Untuk ambiguitas kecil, pilih keputusan paling aman dan konsisten, dokumentasikan, lanjutkan. Untuk keputusan yang mengubah scope, aturan stok/valuasi, keamanan, atau biaya, berhenti dan tanyakan.
8. Berhenti di akhir tiap tahap dan tunggu persetujuan sebelum lanjut.
