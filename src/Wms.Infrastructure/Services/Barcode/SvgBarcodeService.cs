using System.Text;
using Wms.Application.Features.Barcode;

namespace Wms.Infrastructure.Services.Barcode;

public class SvgBarcodeService : IBarcodeService
{
    private static readonly string[] Code128Patterns = new[]
    {
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    };

    public string GenerateBarcodeSvg(string data, int height = 80, bool includeText = true)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            data = "EMPTY";
        }

        var sanitized = new string(data.Where(c => c >= 32 && c <= 126).ToArray());
        if (sanitized.Length == 0) sanitized = "UNKNOWN";

        var patternIndices = new List<int> { 104 }; // Start Code B
        var checksum = 104;

        for (var i = 0; i < sanitized.Length; i++)
        {
            var val = sanitized[i] - 32;
            patternIndices.Add(val);
            checksum += (i + 1) * val;
        }

        checksum %= 103;
        patternIndices.Add(checksum);
        patternIndices.Add(106); // Stop Code

        var modules = new List<bool>();
        foreach (var idx in patternIndices)
        {
            var pattern = Code128Patterns[idx];
            var isBar = true;
            foreach (var ch in pattern)
            {
                var width = ch - '0';
                for (var w = 0; w < width; w++)
                {
                    modules.Add(isBar);
                }
                isBar = !isBar;
            }
        }

        var quietZone = 10;
        var totalWidth = modules.Count + (quietZone * 2);
        var barHeight = includeText ? height - 18 : height;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalWidth} {height}\" width=\"100%\" height=\"{height}\">");
        sb.Append($"<rect width=\"{totalWidth}\" height=\"{height}\" fill=\"#FFFFFF\" />");

        for (var i = 0; i < modules.Count; i++)
        {
            if (modules[i])
            {
                sb.Append($"<rect x=\"{quietZone + i}\" y=\"5\" width=\"1\" height=\"{barHeight}\" fill=\"#000000\" />");
            }
        }

        if (includeText)
        {
            var escapedText = System.Security.SecurityElement.Escape(sanitized);
            sb.Append($"<text x=\"{totalWidth / 2.0:F1}\" y=\"{height - 3}\" font-family=\"monospace\" font-size=\"12\" text-anchor=\"middle\" fill=\"#333333\">{escapedText}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    public string GenerateQrCodeSvg(string data, int size = 200)
    {
        if (string.IsNullOrWhiteSpace(data)) data = "EMPTY";

        // Compact deterministic QR-like 2D matrix (25x25) with standard finder patterns
        var matrixSize = 25;
        var matrix = new bool[matrixSize, matrixSize];

        // 1. Draw 3 Finder Patterns at corners (7x7 outer, 5x5 inner white, 3x3 center black)
        DrawFinderPattern(matrix, 0, 0);
        DrawFinderPattern(matrix, matrixSize - 7, 0);
        DrawFinderPattern(matrix, 0, matrixSize - 7);

        // 2. Timing patterns
        for (var i = 8; i < matrixSize - 8; i++)
        {
            matrix[6, i] = (i % 2 == 0);
            matrix[i, 6] = (i % 2 == 0);
        }

        // 3. Populate data deterministically using data bytes
        var bytes = Encoding.UTF8.GetBytes(data);
        var bitIndex = 0;
        for (var r = 0; r < matrixSize; r++)
        {
            for (var c = 0; c < matrixSize; c++)
            {
                // Skip finder pattern zones
                if (IsFinderZone(r, c, matrixSize)) continue;
                if (r == 6 || c == 6) continue; // Skip timing line

                var b = bytes[bitIndex % bytes.Length];
                var bit = ((b >> (bitIndex % 8)) & 1) == 1;
                matrix[r, c] = bit ^ ((r + c) % 2 == 0);
                bitIndex++;
            }
        }

        var sb = new StringBuilder();
        var quietZone = 2;
        var totalSize = matrixSize + (quietZone * 2);

        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalSize} {totalSize}\" width=\"{size}\" height=\"{size}\">");
        sb.Append($"<rect width=\"{totalSize}\" height=\"{totalSize}\" fill=\"#FFFFFF\" />");

        for (var r = 0; r < matrixSize; r++)
        {
            for (var c = 0; c < matrixSize; c++)
            {
                if (matrix[r, c])
                {
                    sb.Append($"<rect x=\"{c + quietZone}\" y=\"{r + quietZone}\" width=\"1\" height=\"1\" fill=\"#000000\" />");
                }
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void DrawFinderPattern(bool[,] matrix, int startRow, int startCol)
    {
        for (var r = 0; r < 7; r++)
        {
            for (var c = 0; c < 7; c++)
            {
                var isBorder = (r == 0 || r == 6 || c == 0 || c == 6);
                var isCenter = (r >= 2 && r <= 4 && c >= 2 && c <= 4);
                matrix[startRow + r, startCol + c] = isBorder || isCenter;
            }
        }
    }

    private static bool IsFinderZone(int r, int c, int size)
    {
        if (r < 8 && c < 8) return true; // Top-left
        if (r < 8 && c >= size - 8) return true; // Top-right
        if (r >= size - 8 && c < 8) return true; // Bottom-left
        return false;
    }
}

