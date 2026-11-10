namespace Wms.Domain.Enums;

public enum StockTransferStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    InTransit = 4,
    PartiallyReceived = 5,
    Received = 6,
    Cancelled = 7,
    Rejected = 8
}
