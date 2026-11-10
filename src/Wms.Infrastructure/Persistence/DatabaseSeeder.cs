using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Domain.Authorization;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.System;

namespace Wms.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly WmsDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        WmsDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedRolesAndPermissionsAsync();
            await SeedUsersAsync();
            await SeedSettingsAsync();
            await SeedMasterDataAsync();
            await SeedStockTransactionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database seeding.");
            throw;
        }
    }

    private async Task SeedRolesAndPermissionsAsync()
    {
        var roles = new (string Name, string Description, string[] Permissions)[]
        {
            ("SuperAdmin", "Akses penuh ke seluruh modul sistem", Domain.Authorization.Permissions.All.ToArray()),
            ("AdminGudang", "Kelola master data, transaksi gudang, PO, dan opname", new[]
            {
                Domain.Authorization.Permissions.MasterWarehouseView, Domain.Authorization.Permissions.MasterProductView, Domain.Authorization.Permissions.MasterProductManage,
                Domain.Authorization.Permissions.MasterCategoryManage, Domain.Authorization.Permissions.MasterUnitManage, Domain.Authorization.Permissions.MasterSupplierManage,
                Domain.Authorization.Permissions.MasterCustomerManage, Domain.Authorization.Permissions.StockInView, Domain.Authorization.Permissions.StockInCreate,
                Domain.Authorization.Permissions.StockInPost, Domain.Authorization.Permissions.StockOutView, Domain.Authorization.Permissions.StockOutCreate,
                Domain.Authorization.Permissions.StockOutPost, Domain.Authorization.Permissions.PurchaseOrderView, Domain.Authorization.Permissions.PurchaseOrderCreate,
                Domain.Authorization.Permissions.StockTransferView, Domain.Authorization.Permissions.StockTransferCreate, Domain.Authorization.Permissions.StockOpnameView,
                Domain.Authorization.Permissions.StockOpnameManage, Domain.Authorization.Permissions.ReportsView, Domain.Authorization.Permissions.ReportsExport
            }),
            ("PetugasGudang", "Pencatatan barang masuk/keluar harian", new[]
            {
                Domain.Authorization.Permissions.MasterProductView, Domain.Authorization.Permissions.MasterWarehouseView,
                Domain.Authorization.Permissions.StockInView, Domain.Authorization.Permissions.StockInCreate,
                Domain.Authorization.Permissions.StockOutView, Domain.Authorization.Permissions.StockOutCreate
            }),
            ("Supervisor", "Persetujuan transaksi stok, transfer, PO, opname, dan laporan", new[]
            {
                Domain.Authorization.Permissions.MasterWarehouseView, Domain.Authorization.Permissions.MasterProductView,
                Domain.Authorization.Permissions.StockInView, Domain.Authorization.Permissions.StockOutView,
                Domain.Authorization.Permissions.PurchaseOrderView, Domain.Authorization.Permissions.PurchaseOrderApprove,
                Domain.Authorization.Permissions.StockTransferView, Domain.Authorization.Permissions.StockTransferApprove,
                Domain.Authorization.Permissions.StockOpnameView, Domain.Authorization.Permissions.StockOpnameApprove,
                Domain.Authorization.Permissions.StockIncidentView, Domain.Authorization.Permissions.StockIncidentApprove,
                Domain.Authorization.Permissions.ReportsView, Domain.Authorization.Permissions.ReportsExport, Domain.Authorization.Permissions.AuditView
            }),
            ("Auditor", "Akses baca laporan, transaksi, opname, dan audit log", new[]
            {
                Domain.Authorization.Permissions.MasterWarehouseView, Domain.Authorization.Permissions.MasterProductView,
                Domain.Authorization.Permissions.StockInView, Domain.Authorization.Permissions.StockOutView,
                Domain.Authorization.Permissions.PurchaseOrderView, Domain.Authorization.Permissions.StockTransferView,
                Domain.Authorization.Permissions.StockOpnameView, Domain.Authorization.Permissions.ReportsView,
                Domain.Authorization.Permissions.ReportsExport, Domain.Authorization.Permissions.AuditView
            })
        };

        foreach (var (name, description, rolePermissions) in roles)
        {
            var role = await _roleManager.FindByNameAsync(name);
            if (role == null)
            {
                role = new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    NormalizedName = name.ToUpperInvariant(),
                    Description = description,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                await _roleManager.CreateAsync(role);
                _logger.LogInformation("Role {Role} created.", name);
            }

            var existingPerms = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.Permission)
                .ToListAsync();

            var newPerms = rolePermissions.Except(existingPerms).ToList();
            if (newPerms.Count > 0)
            {
                _context.RolePermissions.AddRange(newPerms.Select(p => new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    Permission = p
                }));
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added {Count} permissions to role {Role}.", newPerms.Count, name);
            }
        }
    }

    private async Task SeedUsersAsync()
    {
        const string adminEmail = "admin@wms.local";
        var adminUser = await _userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "admin",
                Email = adminEmail,
                FullName = "Super Administrator",
                EmailConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(adminUser, "Admin123!");
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, "SuperAdmin");
                _logger.LogInformation("Super Admin user ({Email}) created successfully.", adminEmail);
            }
            else
            {
                _logger.LogError("Failed to create Super Admin user: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task SeedSettingsAsync()
    {
        var defaultSettings = new (string Key, string Value, string Description)[]
        {
            ("company_name", "PT WMS Digital Nusantara", "Nama perusahaan yang tertera pada dokumen dan kop laporan"),
            ("timezone", "Asia/Jakarta", "Zona waktu default operasional"),
            ("currency", "IDR", "Mata uang dasar untuk valuasi persediaan"),
            ("po_approval_threshold", "10000000", "Batas nilai PO (IDR) yang mewajibkan persetujuan Manajer"),
            ("stock_low_threshold_default", "10", "Batas stok rendah default jika barang tidak menentukan stok minimum"),
            ("audit_retention_days", "0", "Retensi audit log dalam hari (0 = tanpa batas)")
        };

        foreach (var (key, value, description) in defaultSettings)
        {
            var exists = await _context.AppSettings.AnyAsync(s => s.Key == key);
            if (!exists)
            {
                _context.AppSettings.Add(new AppSetting
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = value,
                    Description = description,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedMasterDataAsync()
    {
        // 1. Warehouses
        Warehouse? warehouseJkt = await _context.Warehouses.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Code == "GDG-JKT");
        if (warehouseJkt == null)
        {
            warehouseJkt = new Domain.Entities.Master.Warehouse
            {
                Id = Guid.NewGuid(),
                Code = "GDG-JKT",
                Name = "Gudang Utama Jakarta",
                Address = "Jl. Daan Mogot No. 100, Jakarta Barat",
                ContactPerson = "Budi Santoso",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.Warehouses.Add(warehouseJkt);
        }

        Warehouse? warehouseSby = await _context.Warehouses.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Code == "GDG-SBY");
        if (warehouseSby == null)
        {
            warehouseSby = new Domain.Entities.Master.Warehouse
            {
                Id = Guid.NewGuid(),
                Code = "GDG-SBY",
                Name = "Gudang Cabang Surabaya",
                Address = "Jl. Rungkut Industri No. 45, Surabaya",
                ContactPerson = "Agus Setiawan",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.Warehouses.Add(warehouseSby);
        }

        await _context.SaveChangesAsync();

        // 2. Locations
        if (!await _context.WarehouseLocations.IgnoreQueryFilters().AnyAsync(l => l.WarehouseId == warehouseJkt.Id))
        {
            _context.WarehouseLocations.AddRange(
                new Domain.Entities.Master.WarehouseLocation
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = warehouseJkt.Id,
                    Code = "A-01-01",
                    Name = "Rak Elektronik 1",
                    Zone = "Zone A",
                    Aisle = "01",
                    Rack = "01",
                    Bin = "01",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new Domain.Entities.Master.WarehouseLocation
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = warehouseJkt.Id,
                    Code = "A-01-02",
                    Name = "Rak Elektronik 2",
                    Zone = "Zone A",
                    Aisle = "01",
                    Rack = "01",
                    Bin = "02",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new Domain.Entities.Master.WarehouseLocation
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = warehouseJkt.Id,
                    Code = "B-01-01",
                    Name = "Rak Umum 1",
                    Zone = "Zone B",
                    Aisle = "01",
                    Rack = "01",
                    Bin = "01",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                }
            );
        }

        if (!await _context.WarehouseLocations.IgnoreQueryFilters().AnyAsync(l => l.WarehouseId == warehouseSby.Id))
        {
            _context.WarehouseLocations.Add(new Domain.Entities.Master.WarehouseLocation
            {
                Id = Guid.NewGuid(),
                WarehouseId = warehouseSby.Id,
                Code = "S-01-01",
                Name = "Rak Cabang 1",
                Zone = "Zone S",
                Aisle = "01",
                Rack = "01",
                Bin = "01",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // 3. Categories
        var categories = new (string Name, string Description)[]
        {
            ("Elektronik", "Perangkat elektronik, komponen, dan aksesoris"),
            ("Pakaian & Tekstil", "Pakaian jadi, seragam, dan bahan kain"),
            ("Makanan & Minuman", "Bahan pangan kering dan minuman kemasan"),
            ("Alat Tulis & Kantor", "Kertas, pulpen, map, dan perlengkapan kantor")
        };

        foreach (var (cName, cDesc) in categories)
        {
            if (!await _context.Categories.AnyAsync(c => c.Name == cName))
            {
                _context.Categories.Add(new Domain.Entities.Master.Category
                {
                    Id = Guid.NewGuid(),
                    Name = cName,
                    Description = cDesc,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // 4. Units
        var units = new (string Name, string Abbr)[]
        {
            ("Pcs", "PCS"),
            ("Box", "BOX"),
            ("Kilogram", "KG"),
            ("Liter", "LTR"),
            ("Pack", "PCK")
        };

        foreach (var (uName, uAbbr) in units)
        {
            if (!await _context.Units.AnyAsync(u => u.Name == uName || u.Abbreviation == uAbbr))
            {
                _context.Units.Add(new Domain.Entities.Master.Unit
                {
                    Id = Guid.NewGuid(),
                    Name = uName,
                    Abbreviation = uAbbr,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // 5. Suppliers
        var suppliers = new (string Code, string Name, string Contact, string Email, string Phone, string Address)[]
        {
            ("SUP-001", "PT Sumber Makmur Elektronik", "Hendra Wijaya", "sales@sumbermakmur.co.id", "021-5551234", "Kawasan Industri Pulo Gadung, Jakarta"),
            ("SUP-002", "CV Pangan Sejahtera Bersama", "Siti Rahma", "order@pangansejahtera.com", "031-7778899", "Jl. Industri No. 88, Sidoarjo")
        };

        foreach (var (sCode, sName, sContact, sEmail, sPhone, sAddress) in suppliers)
        {
            if (!await _context.Suppliers.AnyAsync(s => s.Code == sCode))
            {
                _context.Suppliers.Add(new Domain.Entities.Master.Supplier
                {
                    Id = Guid.NewGuid(),
                    Code = sCode,
                    Name = sName,
                    ContactPerson = sContact,
                    Email = sEmail,
                    Phone = sPhone,
                    Address = sAddress,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // 6. Customers
        var customers = new (string Code, string Name, string Contact, string Email, string Phone, string Address)[]
        {
            ("CUST-001", "Toko Retail Jaya Makmur", "Andi Wijaya", "jayamakmur@retail.co.id", "081234567890", "Jl. Hayam Wuruk No. 12, Jakarta"),
            ("CUST-002", "PT Mitra Distribusi Utama", "Dewi Lestari", "purchasing@mitradistribusi.co.id", "021-8889900", "Kawasan Industri Cikarang, Jawa Barat")
        };

        foreach (var (cCode, cName, cContact, cEmail, cPhone, cAddress) in customers)
        {
            if (!await _context.Customers.AnyAsync(c => c.Code == cCode))
            {
                _context.Customers.Add(new Domain.Entities.Master.Customer
                {
                    Id = Guid.NewGuid(),
                    Code = cCode,
                    Name = cName,
                    ContactPerson = cContact,
                    Email = cEmail,
                    Phone = cPhone,
                    Address = cAddress,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        // 7. Products
        var catElektronik = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Elektronik");
        var catAtk = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Alat Tulis & Kantor");
        var unitPcs = await _context.Units.FirstOrDefaultAsync(u => u.Abbreviation == "PCS");
        var unitBox = await _context.Units.FirstOrDefaultAsync(u => u.Abbreviation == "BOX");

        if (catElektronik != null && unitPcs != null)
        {
            if (!await _context.Products.AnyAsync(p => p.Sku == "ELK-001"))
            {
                _context.Products.Add(new Domain.Entities.Master.Product
                {
                    Id = Guid.NewGuid(),
                    Sku = "ELK-001",
                    Barcode = "89910010001",
                    Name = "Mouse Wireless Ergonomis",
                    Description = "Mouse optik nirkabel 2.4GHz dengan silent click",
                    Brand = "LogiTech",
                    CategoryId = catElektronik.Id,
                    UnitId = unitPcs.Id,
                    MinStock = 10,
                    MaxStock = 100,
                    TrackingMethod = Domain.Enums.TrackingMethod.None,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            if (!await _context.Products.AnyAsync(p => p.Sku == "ELK-002"))
            {
                _context.Products.Add(new Domain.Entities.Master.Product
                {
                    Id = Guid.NewGuid(),
                    Sku = "ELK-002",
                    Barcode = "89910010002",
                    Name = "Keyboard Mekanikal TKL",
                    Description = "Keyboard mekanikal 87-key dengan switch merah",
                    Brand = "KeyChron",
                    CategoryId = catElektronik.Id,
                    UnitId = unitPcs.Id,
                    MinStock = 5,
                    MaxStock = 50,
                    TrackingMethod = Domain.Enums.TrackingMethod.None,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        if (catAtk != null && unitBox != null)
        {
            if (!await _context.Products.AnyAsync(p => p.Sku == "ATK-001"))
            {
                _context.Products.Add(new Domain.Entities.Master.Product
                {
                    Id = Guid.NewGuid(),
                    Sku = "ATK-001",
                    Barcode = "89920010001",
                    Name = "Kertas HVS A4 80gsm 1 Box",
                    Description = "Kertas fotokopi putih isi 5 rim per box",
                    Brand = "PaperOne",
                    CategoryId = catAtk.Id,
                    UnitId = unitBox.Id,
                    MinStock = 20,
                    MaxStock = 200,
                    TrackingMethod = Domain.Enums.TrackingMethod.None,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // 8. Assign Admin user to warehouses
        var adminUser = await _userManager.FindByNameAsync("admin");
        if (adminUser != null)
        {
            var existingAssigns = await _context.UserWarehouses
                .Where(uw => uw.UserId == adminUser.Id)
                .Select(uw => uw.WarehouseId)
                .ToListAsync();

            if (warehouseJkt != null && !existingAssigns.Contains(warehouseJkt.Id))
            {
                _context.UserWarehouses.Add(new Domain.Entities.Identity.UserWarehouse
                {
                    Id = Guid.NewGuid(),
                    UserId = adminUser.Id,
                    WarehouseId = warehouseJkt.Id
                });
            }

            if (warehouseSby != null && !existingAssigns.Contains(warehouseSby.Id))
            {
                _context.UserWarehouses.Add(new Domain.Entities.Identity.UserWarehouse
                {
                    Id = Guid.NewGuid(),
                    UserId = adminUser.Id,
                    WarehouseId = warehouseSby.Id
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedStockTransactionsAsync()
    {
        if (await _context.GoodsReceipts.AnyAsync())
        {
            return;
        }

        var warehouse = await _context.Warehouses.Include(w => w.Locations).FirstOrDefaultAsync(w => w.Code == "WH-JKT-01");
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Code == "SUP-001");
        var adminUser = await _userManager.FindByNameAsync("admin");
        var productMouse = await _context.Products.FirstOrDefaultAsync(p => p.Sku == "ELK-001");
        var productKertas = await _context.Products.FirstOrDefaultAsync(p => p.Sku == "ATK-001");

        if (warehouse == null || adminUser == null || productMouse == null || warehouse.Locations.Count == 0)
        {
            return;
        }

        var location1 = warehouse.Locations.First();
        var now = DateTime.UtcNow;

        // 1. Penerimaan Awal (Goods Receipt)
        var receipt = new Domain.Entities.Transactions.GoodsReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = "GR-202611-000001",
            WarehouseId = warehouse.Id,
            SupplierId = supplier?.Id,
            PurchaseOrderNumber = "PO-202611-0001",
            Notes = "Penerimaan stok awal sistem (Seeding demo)",
            Status = Domain.Enums.DocumentStatus.Posted,
            CreatedByUserId = adminUser.Id,
            PostedByUserId = adminUser.Id,
            PostedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Lines = new List<Domain.Entities.Transactions.GoodsReceiptLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = productMouse.Id,
                    WarehouseLocationId = location1.Id,
                    Quantity = 50m,
                    UnitCost = 150000m,
                    TotalCost = 7500000m
                }
            }
        };

        if (productKertas != null && warehouse.Locations.Count > 1)
        {
            var location2 = warehouse.Locations.Skip(1).First();
            receipt.Lines.Add(new Domain.Entities.Transactions.GoodsReceiptLine
            {
                Id = Guid.NewGuid(),
                ProductId = productKertas.Id,
                WarehouseLocationId = location2.Id,
                Quantity = 100m,
                UnitCost = 45000m,
                TotalCost = 4500000m
            });
        }

        _context.GoodsReceipts.Add(receipt);

        // 2. Balances, Valuations & Stock Movements
        foreach (var line in receipt.Lines)
        {
            _context.InventoryBalances.Add(new Domain.Entities.Inventory.InventoryBalance
            {
                Id = Guid.NewGuid(),
                ProductId = line.ProductId,
                WarehouseId = warehouse.Id,
                WarehouseLocationId = line.WarehouseLocationId,
                Status = Domain.Enums.StockStatus.Available,
                OnHand = line.Quantity,
                Reserved = 0m,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            _context.InventoryValuations.Add(new Domain.Entities.Inventory.InventoryValuation
            {
                Id = Guid.NewGuid(),
                ProductId = line.ProductId,
                WarehouseId = warehouse.Id,
                OnHandQty = line.Quantity,
                OnHandValue = line.TotalCost,
                AvgCost = line.UnitCost,
                LastCost = line.UnitCost,
                UpdatedAtUtc = now
            });

            _context.StockMovements.Add(new Domain.Entities.Inventory.StockMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"MV-202611-{_context.StockMovements.Count() + 1:D6}",
                MovementType = Domain.Enums.MovementType.GoodsReceipt,
                ProductId = line.ProductId,
                WarehouseId = warehouse.Id,
                WarehouseLocationId = line.WarehouseLocationId,
                StockStatus = Domain.Enums.StockStatus.Available,
                Quantity = line.Quantity,
                Direction = Domain.Enums.MovementDirection.In,
                BalanceBefore = 0m,
                BalanceAfter = line.Quantity,
                UnitCost = line.UnitCost,
                TotalValue = line.TotalCost,
                SourceDocumentType = Domain.Enums.SourceDocumentType.GoodsReceipt,
                SourceDocumentId = receipt.Id,
                SourceDocumentNumber = receipt.ReceiptNumber,
                Reason = "Penerimaan stok awal",
                UserId = adminUser.Id,
                TimestampUtc = now,
                Status = Domain.Enums.MovementStatus.Posted
            });
        }

        await _context.SaveChangesAsync();
    }
}
