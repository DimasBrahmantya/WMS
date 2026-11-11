namespace Wms.Application.Features.Barcode;

public interface IBarcodeService
{
    /// <summary>
    /// Menghasilkan Barcode 1D (Code 128) dalam format SVG string.
    /// </summary>
    string GenerateBarcodeSvg(string data, int height = 80, bool includeText = true);

    /// <summary>
    /// Menghasilkan QR Code 2D dalam format SVG string.
    /// </summary>
    string GenerateQrCodeSvg(string data, int size = 200);
}

