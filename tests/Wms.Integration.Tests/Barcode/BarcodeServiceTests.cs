using FluentAssertions;
using Wms.Infrastructure.Services.Barcode;
using Xunit;

namespace Wms.Integration.Tests.Barcode;

public class BarcodeServiceTests
{
    [Fact]
    public void GenerateBarcodeSvg_ShouldProduceValidSvgMarkup()
    {
        var service = new SvgBarcodeService();
        var svg = service.GenerateBarcodeSvg("SKU-PROD-001", 80, true);

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().StartWith("<svg xmlns=\"http://www.w3.org/2000/svg\"");
        svg.Should().EndWith("</svg>");
        svg.Should().Contain("<rect");
        svg.Should().Contain("SKU-PROD-001");
    }

    [Fact]
    public void GenerateQrCodeSvg_ShouldProduceValidSvgMarkup()
    {
        var service = new SvgBarcodeService();
        var svg = service.GenerateQrCodeSvg("https://wms.local/products/SKU-001", 200);

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().StartWith("<svg xmlns=\"http://www.w3.org/2000/svg\"");
        svg.Should().EndWith("</svg>");
        svg.Should().Contain("<rect");
    }
}

