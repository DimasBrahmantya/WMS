using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Application.Features.Exports;

namespace Wms.Web.Controllers;

[ApiController]
[Route("api/exports")]
[Authorize]
public class ExportsDownloadController : ControllerBase
{
    private readonly IExportService _exportService;

    public ExportsDownloadController(IExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpGet("download/{jobId:guid}")]
    public async Task<IActionResult> DownloadExport(Guid jobId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var result = await _exportService.GetExportFileAsync(jobId, userId, isSuperAdmin, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        var (fileBytes, contentType, fileName) = result.Data;
        return File(fileBytes, contentType, fileName);
    }
}
