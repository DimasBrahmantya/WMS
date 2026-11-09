using Wms.Domain.Common;

namespace Wms.Domain.Entities.System;

public class AppSetting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
