namespace Wms.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Ordered = 4,
    PartiallyReceived = 5,
    Received = 6,
    Cancelled = 7,
    Rejected = 8
}
