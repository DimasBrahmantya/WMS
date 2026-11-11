namespace Wms.Domain.Enums;

public enum MovementType
{
    GoodsReceipt = 1,
    GoodsReceiptReversal = 2,
    GoodsIssue = 3,
    GoodsIssueReversal = 4,
    StockAdjustment = 5,
    RoundingCorrection = 6,
    TransferIn = 7,
    TransferOut = 8,
    PurchaseReturn = 9,
    CustomerReturn = 10,
    StockIncident = 11,
    SalesOrderIssue = 12
}

