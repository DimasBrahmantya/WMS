namespace Wms.Domain.Common;

public interface ISoftDeletable
{
    bool IsActive { get; set; }
}
