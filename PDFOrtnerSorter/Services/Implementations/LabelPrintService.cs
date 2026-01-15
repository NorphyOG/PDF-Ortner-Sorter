using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;
using QRCoder;
using Brush = System.Drawing.Brush;
using FontFamily = System.Drawing.FontFamily;
using Pen = System.Drawing.Pen;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class LabelPrintService : ILabelPrintService
{
    private readonly ILoggerService _logger;

    public LabelPrintService(ILoggerService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> GetAvailablePrinters()
    {
        var printers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            _logger.LogInfo("=== Starting printer detection ===");
            
            // Method 1: PrinterSettings.InstalledPrinters (alte Registry-basierte Drucker)
            try
            {
                var installedCount = PrinterSettings.InstalledPrinters.Count;
                _logger.LogInfo($"[Method 1] PrinterSettings.InstalledPrinters.Count: {installedCount}");
                
                if (installedCount > 0)
                {
                    foreach (string printerName in PrinterSettings.InstalledPrinters)
                    {
                        if (!string.IsNullOrWhiteSpace(printerName))
                        {
                            printers.Add(printerName);
                            _logger.LogInfo($"[Method 1] Found: {printerName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[Method 1] Failed to get printers from InstalledPrinters", ex);
            }
            
            // Method 2: WMI-basierte Drucker-Erkennung (findet auch moderne IPP-Drucker)
            try
            {
                _logger.LogInfo("[Method 2] Trying WMI Win32_Printer...");
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Printer");
                using var collection = searcher.Get();
                
                var wmiCount = 0;
                foreach (var printer in collection)
                {
                    wmiCount++;
                    var name = printer["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var isNew = printers.Add(name);
                        _logger.LogInfo($"[Method 2] Found: {name}{(isNew ? " (NEW)" : " (duplicate)")}");
                    }
                }
                _logger.LogInfo($"[Method 2] WMI found {wmiCount} printer(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError("[Method 2] Failed to get printers from WMI", ex);
            }
            
            _logger.LogInfo($"=== Total unique printers found: {printers.Count} ===");
            
            if (printers.Count == 0)
            {
                _logger.LogInfo("WARNING: No printers found! Check:");
                _logger.LogInfo("  1. Print Spooler service is running");
                _logger.LogInfo("  2. Printers are properly installed in Windows");
                _logger.LogInfo("  3. User has permissions to access printers");
            }
            else
            {
                _logger.LogInfo("Available printers:");
                var sorted = printers.OrderBy(p => p).ToList();
                foreach (var printer in sorted)
                {
                    _logger.LogInfo($"  - {printer}");
                }
                return sorted;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get available printers", ex);
        }
        
        return Array.Empty<string>();
    }

    public Task<bool> PrintLabelAsync(LabelPrintRequest request, string printerName, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var printDoc = new PrintDocument
                {
                    PrinterSettings = new PrinterSettings { PrinterName = printerName }
                };

                _logger.LogInfo($"Starting print to: {printerName}");

                var printedLabels = 0;
                var totalLabels = request.Copies;

                printDoc.PrintPage += (sender, e) =>
                {
                    try
                    {
                        _logger.LogInfo($"PrintPage event fired. Page bounds: {e.PageBounds}, DPI: {e.Graphics?.DpiX}x{e.Graphics?.DpiY}");
                        
                        if (e.Graphics == null)
                        {
                            _logger.LogError("Graphics is null!");
                            return;
                        }

                        // Label size (increased by 30%)
                        int labelWidth = 585;   // 450 * 1.3
                        int labelHeight = 390;  // 300 * 1.3
                        int marginLeft = 20;    // Small margin from page edge
                        int marginTop = 20;
                        
                        _logger.LogInfo($"Drawing label at ({marginLeft},{marginTop}) with size {labelWidth}x{labelHeight}px");

                        // Draw label with fixed position and size
                        DrawLabel(e.Graphics, request, labelWidth, labelHeight, marginLeft, marginTop);

                        printedLabels++;
                        _logger.LogInfo($"Label {printedLabels} drawn");

                        e.HasMorePages = printedLabels < totalLabels;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error in PrintPage: {ex.Message}", ex);
                    }
                };

                printDoc.Print();
                _logger.LogInfo($"Print job completed: {printedLabels} label(s)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to print label to {printerName}", ex);
                return false;
            }
        }, cancellationToken);
    }

    public Task<byte[]?> GenerateLabelPreviewAsync(LabelPrintRequest request, CancellationToken cancellationToken)
    {
        return Task.Run<byte[]?>(() =>
        {
            try
            {
                // Render a simple A4 preview canvas and draw the label
                // A4 aspect ratio ~ 1:1.414; choose a friendly preview size
                int pageWidth = 800;
                int pageHeight = (int)Math.Round(pageWidth * 1.414f);

                // Use the same label size and offset as printing for consistency (30% larger)
                int labelWidth = 585;   // 450 * 1.3
                int labelHeight = 390;  // 300 * 1.3
                int marginLeft = 20;
                int marginTop = 20;

                using var bitmap = new Bitmap(pageWidth, pageHeight);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.Clear(System.Drawing.Color.White);
                DrawLabel(graphics, request, labelWidth, labelHeight, marginLeft, marginTop);

                using var stream = new MemoryStream();
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to generate label preview", ex);
                return null;
            }
        }, cancellationToken);
    }

    public Task<bool> ExportLabelAsPdfAsync(LabelPrintRequest request, string outputPath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var (widthMm, heightMm) = GetLabelDimensionsInMm(request.Format);
                int widthPixels = (int)Math.Round((double)widthMm * 4);
                int heightPixels = (int)Math.Round((double)heightMm * 4);
                
                using var bitmap = new Bitmap(widthPixels, heightPixels);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.Clear(System.Drawing.Color.White);
                DrawLabel(graphics, request, widthPixels, heightPixels);

                // Save as high-quality image that can be printed
                bitmap.Save(outputPath.Replace(".pdf", ".png"), System.Drawing.Imaging.ImageFormat.Png);
                _logger.LogInfo($"Exported label to {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to export label as PDF to {outputPath}", ex);
                return false;
            }
        }, cancellationToken);
    }

    private static (int width, int height) GetLabelDimensions(LabelFormat format)
    {
        // DEPRECATED - use GetLabelDimensionsInMm instead
        var (widthMm, heightMm) = GetLabelDimensionsInMm(format);
        int widthHundredthsInch = (int)Math.Round((double)widthMm * 100 / 25.4);
        int heightHundredthsInch = (int)Math.Round((double)heightMm * 100 / 25.4);
        return (widthHundredthsInch, heightHundredthsInch);
    }

    private static (int width, int height) GetLabelDimensionsInMm(LabelFormat format)
    {
        // Return dimensions in millimeters
        return format switch
        {
            LabelFormat.Avery3474 => (70, 36),        // Avery Zweckform 3474
            LabelFormat.BrotherDK11208 => (38, 90),   // Brother DK-11208
            LabelFormat.Dymo99012 => (36, 89),        // Dymo 99012
            _ => (70, 36)
        };
    }

    private static void DrawLabel(Graphics graphics, LabelPrintRequest request, int width, int height, int offsetX = 0, int offsetY = 0)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Hardcoded font sizes in pixels for consistent appearance
        float titleFontSize = 28f;      // Title font
        float infoFontSize = 16f;        // Date and count
        float watermarkFontSize = 12f;   // Watermark
        float barcodeSize = request.IncludeBarcode ? 120f : 0; // QR code size (+50%)
        
        // Measure text to determine required height
        using var titleFont = new Font("Arial", titleFontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        var titleMeasureRect = new RectangleF(10, 0, width - 20, height);
        var titleFormat = new StringFormat();
        titleFormat.Alignment = StringAlignment.Center;
        titleFormat.LineAlignment = StringAlignment.Near;
        titleFormat.Trimming = StringTrimming.Word;
        titleFormat.FormatFlags = StringFormatFlags.NoClip;
        
        var titleSize = graphics.MeasureString(request.LabelInfo.FolderName, titleFont, (int)(width - 20), titleFormat);
        
        // Calculate dynamic layout with padding
        float topPadding = 15f;
        float titleHeight = titleSize.Height + 10;
        float infoHeight = 40f;
        float watermarkHeight = 20f;

        // Border
        using var borderPen = new Pen(System.Drawing.Color.Black, 2);
        graphics.DrawRectangle(borderPen, offsetX + 5, offsetY + 5, width - 10, height - 10);

        // Draw folder name with automatic word wrapping
        float currentY = offsetY + topPadding;
        using (var titleBrush = new SolidBrush(System.Drawing.Color.Black))
        using (var actualTitleFont = new Font("Arial", titleFontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
        {
            float rightPadding = request.IncludeBarcode ? (barcodeSize + 15f) : 10f;
            var titleRect = new RectangleF(offsetX + 10, currentY, width - 10 - rightPadding, titleHeight);
            titleFormat.Alignment = StringAlignment.Near; // left align to avoid overlap with QR
            titleFormat.LineAlignment = StringAlignment.Near;
            graphics.DrawString(request.LabelInfo.FolderName, actualTitleFont, titleBrush, titleRect, titleFormat);
        }
        
        currentY += titleHeight + 5;

        // Date and document count (left column, avoid QR area on the right)
        using (var infoFont = new Font("Arial", infoFontSize, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel))
        using (var infoBrush = new SolidBrush(System.Drawing.Color.FromArgb(120, 0, 0, 0)))
        {
            float rightPadding = request.IncludeBarcode ? (barcodeSize + 15f) : 10f;
            var infoRect = new RectangleF(offsetX + 10, currentY, width - 10 - rightPadding, infoFontSize * 2 + 10);
            var infoFormat = new StringFormat { Alignment = StringAlignment.Near };

            var dateText = $"{request.LabelInfo.Timestamp:dd.MM.yyyy HH:mm}";
            graphics.DrawString(dateText, infoFont, infoBrush, infoRect, infoFormat);

            currentY += infoFontSize + 5;

            var countText = $"{request.LabelInfo.DocumentCount} Dokument(e)";
            var countRect = new RectangleF(offsetX + 10, currentY, width - 10 - rightPadding, infoFontSize + 5);
            graphics.DrawString(countText, infoFont, infoBrush, countRect, infoFormat);
        }

        // Scanora watermark (bottom)
        using (var watermarkFont = new Font("Arial", watermarkFontSize, System.Drawing.FontStyle.Italic, GraphicsUnit.Pixel))
        using (var watermarkBrush = new SolidBrush(System.Drawing.Color.FromArgb(100, 0, 0, 0)))
        {
            var watermarkRect = new RectangleF(offsetX + 10, offsetY + height - watermarkHeight - 5, width - 20, watermarkHeight);
            var watermarkFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("powered by Scanora", watermarkFont, watermarkBrush, watermarkRect, watermarkFormat);
        }

        // Barcode/QR-Code (bottom right)
        if (request.IncludeBarcode && !string.IsNullOrWhiteSpace(request.LabelInfo.BarcodeData))
        {
            try
            {
                var actualBarcodeSize = (int)barcodeSize;
                var barcodeRect = new Rectangle(offsetX + width - actualBarcodeSize - 10, offsetY + height - actualBarcodeSize - 10, actualBarcodeSize, actualBarcodeSize);
                
                // Generate QR Code with JSON data
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(request.LabelInfo.BarcodeData, QRCodeGenerator.ECCLevel.M);
                using var qrCode = new QRCode(qrCodeData);
                
                // Generate QR code as bitmap
                using var qrBitmap = qrCode.GetGraphic(
                    pixelsPerModule: Math.Max(1, actualBarcodeSize / 25),
                    darkColor: System.Drawing.Color.Black,
                    lightColor: System.Drawing.Color.White,
                    drawQuietZones: true
                );
                
                // Draw the QR code
                graphics.DrawImage(qrBitmap, barcodeRect);
            }
            catch
            {
                // Fallback: Draw placeholder if QR code generation fails
                using var barcodeBrush = new SolidBrush(System.Drawing.Color.Black);
                var actualBarcodeSize = (int)barcodeSize;
                var barcodeRect = new Rectangle(offsetX + width - actualBarcodeSize - 10, offsetY + height - actualBarcodeSize - 10, actualBarcodeSize, actualBarcodeSize);
                graphics.FillRectangle(barcodeBrush, barcodeRect);
                
                float barcodeFontSize = actualBarcodeSize * 0.15f;
                using var barcodeFont = new Font("Arial", barcodeFontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
                using var whiteBrush = new SolidBrush(System.Drawing.Color.White);
                var barcodeTextFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString("QR", barcodeFont, whiteBrush, barcodeRect, barcodeTextFormat);
            }
        }
    }

    private static void DrawLabelOnGraphics(Graphics graphics, LabelPrintRequest request, float x, float y, float width, float height)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Draw a simple label box
        using var borderPen = new Pen(System.Drawing.Color.Black, 1);
        graphics.DrawRectangle(borderPen, x, y, width, height);

        // Folder name
        float titleFontSize = width > 200 ? 10 : 8;
        using var titleFont = new Font("Arial", titleFontSize, System.Drawing.FontStyle.Bold);
        using var titleBrush = new SolidBrush(System.Drawing.Color.Black);
        var titleRect = new RectangleF(x + 5, y + 5, width - 10, height * 0.3f);
        var titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(request.LabelInfo.FolderName, titleFont, titleBrush, titleRect, titleFormat);

        // Date and document count
        float infoFontSize = width > 200 ? 7 : 6;
        using var infoFont = new Font("Arial", infoFontSize, System.Drawing.FontStyle.Regular);
        var infoRect = new RectangleF(x + 5, y + height * 0.35f, width - 10, height * 0.25f);
        var infoFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
        var dateText = $"{request.LabelInfo.Timestamp:dd.MM.yyyy HH:mm}";
        var countText = $"{request.LabelInfo.DocumentCount} Doc(s)";
        graphics.DrawString($"{dateText}", infoFont, titleBrush, infoRect, infoFormat);

        // Scanora watermark
        float watermarkFontSize = width > 200 ? 5 : 4;
        using var watermarkFont = new Font("Arial", watermarkFontSize, System.Drawing.FontStyle.Italic);
        using var watermarkBrush = new SolidBrush(System.Drawing.Color.FromArgb(100, 0, 0, 0));
        var watermarkRect = new RectangleF(x + 5, y + height - 15, width - 10, 10);
        var watermarkFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("powered by Scanora", watermarkFont, watermarkBrush, watermarkRect, watermarkFormat);
    }

    private static string TruncatePath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            return path;

        var parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
            return path.Substring(0, maxLength) + "...";

        return $"{parts[0]}\\...\\{parts[^1]}";
    }
}
