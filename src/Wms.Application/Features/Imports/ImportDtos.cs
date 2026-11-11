namespace Wms.Application.Features.Imports;

public enum ImportType
{
    Products = 1,
    InitialStock = 2
}

public class ImportValidationResultDto
{
    public ImportType ImportType { get; set; }
    public int TotalRows { get; set; }
    public int ValidRowsCount { get; set; }
    public int ErrorRowsCount { get; set; }
    public bool CanCommit => ErrorRowsCount == 0 && ValidRowsCount > 0;
    public List<ImportRowErrorDto> Errors { get; set; } = new();
    public List<ProductImportRowDto> ProductRows { get; set; } = new();
    public List<InitialStockImportRowDto> StockRows { get; set; } = new();
}

public class ImportRowErrorDto
{
    public int RowNumber { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ProductImportRowDto
{
    public int RowNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? Brand { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public string TrackingMethod { get; set; } = "None";
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class InitialStockImportRowDto
{
    public int RowNumber { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class CommitImportCommand
{
    public ImportType ImportType { get; set; }
    public List<ProductImportRowDto> ProductRows { get; set; } = new();
    public List<InitialStockImportRowDto> StockRows { get; set; } = new();
}

public class ImportExecutionResultDto
{
    public ImportType ImportType { get; set; }
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

