namespace Wms.Domain.Enums;

public static class ApiKeyScope
{
    public const string ProductsRead = "products:read";
    public const string InventoryRead = "inventory:read";
    public const string SalesOrdersRead = "salesorders:read";
    public const string SalesOrdersWrite = "salesorders:write";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ProductsRead,
        InventoryRead,
        SalesOrdersRead,
        SalesOrdersWrite
    };
}

