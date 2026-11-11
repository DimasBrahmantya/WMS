using Wms.Application.Common.Models;

namespace Wms.Application.Common.Guards;

public static class SeparationOfDutiesGuard
{
    public const string SelfApprovalErrorMessage = "Pemisahan tugas (Separation of Duties): Pembuat dokumen tidak diizinkan menyetujui dokumen yang dibuatnya sendiri.";

    public static Result EnsureCanApprove(Guid createdByUserId, Guid approverUserId, string? documentName = null)
    {
        if (createdByUserId == approverUserId)
        {
            var docPrefix = string.IsNullOrWhiteSpace(documentName) ? "dokumen" : $"dokumen {documentName}";
            return Result.Failure($"Pemisahan tugas (Separation of Duties): Pembuat {docPrefix} tidak diizinkan menyetujui dokumen yang dibuatnya sendiri.");
        }

        return Result.Success();
    }
}

