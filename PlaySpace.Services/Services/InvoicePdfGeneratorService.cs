using Microsoft.Extensions.Logging;
using PlaySpace.Domain.Models;
using PlaySpace.Services.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace PlaySpace.Services.Services;

public class InvoicePdfGeneratorService : IInvoicePdfGeneratorService
{
    private readonly ILogger<InvoicePdfGeneratorService> _logger;

    public InvoicePdfGeneratorService(ILogger<InvoicePdfGeneratorService> logger)
    {
        _logger = logger;
        // Set QuestPDF license (Community license for open source / small business)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateInvoicePdf(KSeFInvoice invoice)
    {
        _logger.LogInformation("Generating PDF for invoice {InvoiceNumber}", invoice.InvoiceNumber);

        // Parse invoice items
        var items = new List<KSeFInvoiceItem>();
        if (!string.IsNullOrEmpty(invoice.InvoiceItems))
        {
            try
            {
                items = JsonSerializer.Deserialize<List<KSeFInvoiceItem>>(invoice.InvoiceItems) ?? new List<KSeFInvoiceItem>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse invoice items for {InvoiceNumber}", invoice.InvoiceNumber);
            }
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, invoice));
                page.Content().Element(c => ComposeContent(c, invoice, items));
                page.Footer().Element(c => ComposeFooter(c, invoice));
            });
        });

        var pdfBytes = document.GeneratePdf();
        _logger.LogInformation("PDF generated for invoice {InvoiceNumber}, size: {Size} bytes", invoice.InvoiceNumber, pdfBytes.Length);

        return pdfBytes;
    }

    private void ComposeHeader(IContainer container, KSeFInvoice invoice)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("FAKTURA VAT").Bold().FontSize(20);
                    col.Item().Text($"Nr: {invoice.InvoiceNumber}").FontSize(14);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text($"Data wystawienia: {invoice.IssueDate:dd.MM.yyyy}");
                    if (!string.IsNullOrEmpty(invoice.KSeFReferenceNumber))
                    {
                        col.Item().Text($"Nr KSeF: {invoice.KSeFReferenceNumber}").FontSize(8);
                    }
                });
            });

            column.Item().PaddingVertical(10).LineHorizontal(1);
        });
    }

    private void ComposeContent(IContainer container, KSeFInvoice invoice, List<KSeFInvoiceItem> items)
    {
        container.Column(column =>
        {
            // Seller and Buyer info side by side
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("SPRZEDAWCA").Bold();
                    col.Item().PaddingTop(5).Text(invoice.SellerName);
                    col.Item().Text($"NIP: {invoice.SellerNIP}");
                    col.Item().Text(invoice.SellerAddress);
                    col.Item().Text($"{invoice.SellerPostalCode} {invoice.SellerCity}");
                });

                row.ConstantItem(20);

                row.RelativeItem().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("NABYWCA").Bold();
                    col.Item().PaddingTop(5).Text(invoice.BuyerName);
                    if (!string.IsNullOrEmpty(invoice.BuyerNIP))
                        col.Item().Text($"NIP: {invoice.BuyerNIP}");
                    if (!string.IsNullOrEmpty(invoice.BuyerAddress))
                        col.Item().Text(invoice.BuyerAddress);
                    if (!string.IsNullOrEmpty(invoice.BuyerPostalCode) || !string.IsNullOrEmpty(invoice.BuyerCity))
                        col.Item().Text($"{invoice.BuyerPostalCode} {invoice.BuyerCity}");
                    if (!string.IsNullOrEmpty(invoice.BuyerEmail))
                        col.Item().Text($"Email: {invoice.BuyerEmail}");
                });
            });

            column.Item().PaddingVertical(15);

            // Invoice items table
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);  // Lp.
                    columns.RelativeColumn(3);   // Name/Description
                    columns.ConstantColumn(40);  // Quantity
                    columns.ConstantColumn(40);  // Unit
                    columns.ConstantColumn(70);  // Unit Price
                    columns.ConstantColumn(70);  // Net Amount
                    columns.ConstantColumn(40);  // VAT %
                    columns.ConstantColumn(60);  // VAT Amount
                    columns.ConstantColumn(80);  // Gross Amount
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Lp.").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Nazwa towaru/uslugi").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Ilosc").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Jm.").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Cena jed.").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Netto").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("VAT%").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("VAT").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Brutto").Bold();
                });

                // Items
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                    table.Cell().Background(bgColor).Padding(5).Text((i + 1).ToString());
                    table.Cell().Background(bgColor).Padding(5).Column(col =>
                    {
                        col.Item().Text(item.Name);
                        if (!string.IsNullOrEmpty(item.Description))
                            col.Item().Text(item.Description).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    table.Cell().Background(bgColor).Padding(5).AlignCenter().Text(item.Quantity.ToString("N2"));
                    table.Cell().Background(bgColor).Padding(5).AlignCenter().Text(item.Unit);
                    table.Cell().Background(bgColor).Padding(5).AlignRight().Text($"{item.UnitPrice:N2} zl");
                    table.Cell().Background(bgColor).Padding(5).AlignRight().Text($"{item.NetAmount:N2} zl");
                    table.Cell().Background(bgColor).Padding(5).AlignCenter().Text($"{item.VATRate}%");
                    table.Cell().Background(bgColor).Padding(5).AlignRight().Text($"{item.VATAmount:N2} zl");
                    table.Cell().Background(bgColor).Padding(5).AlignRight().Text($"{item.GrossAmount:N2} zl");
                }
            });

            column.Item().PaddingVertical(10);

            // Summary
            column.Item().AlignRight().Width(250).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(100);
                });

                table.Cell().Padding(5).Text("Razem netto:").Bold();
                table.Cell().Padding(5).AlignRight().Text($"{invoice.NetAmount:N2} zl").Bold();

                table.Cell().Padding(5).Text($"VAT {invoice.VATRate}%:").Bold();
                table.Cell().Padding(5).AlignRight().Text($"{invoice.VATAmount:N2} zl").Bold();

                table.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("DO ZAPLATY:").Bold().FontSize(12);
                table.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{invoice.GrossAmount:N2} zl").Bold().FontSize(12);
            });

            column.Item().PaddingVertical(20);

            // Payment info
            column.Item().Text($"Slownie: {NumberToWords(invoice.GrossAmount)} zlotych {GetGrosze(invoice.GrossAmount):00}/100").FontSize(9);
            column.Item().Text("Forma platnosci: Przelew").FontSize(9);
            column.Item().Text("Status: Oplacono").FontSize(9);
        });
    }

    private void ComposeFooter(IContainer container, KSeFInvoice invoice)
    {
        container.Column(column =>
        {
            column.Item().PaddingTop(20).LineHorizontal(0.5f);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"Faktura wygenerowana elektronicznie").FontSize(8).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignRight().Text($"Spotto - System rezerwacji").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            if (!string.IsNullOrEmpty(invoice.KSeFReferenceNumber))
            {
                column.Item().AlignCenter().Text($"Dokument zarejestrowany w Krajowym Systemie e-Faktur (KSeF)").FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private string NumberToWords(decimal number)
    {
        var intPart = (int)Math.Floor(number);

        if (intPart == 0) return "zero";

        var ones = new[] { "", "jeden", "dwa", "trzy", "cztery", "piec", "szesc", "siedem", "osiem", "dziewiec" };
        var teens = new[] { "dziesiec", "jedenascie", "dwanascie", "trzynascie", "czternascie", "pietnascie", "szesnascie", "siedemnascie", "osiemnascie", "dziewietnascie" };
        var tens = new[] { "", "dziesiec", "dwadziescia", "trzydziesci", "czterdziesci", "piecdziesiat", "szescdziesiat", "siedemdziesiat", "osiemdziesiat", "dziewiecdziesiat" };
        var hundreds = new[] { "", "sto", "dwiescie", "trzysta", "czterysta", "piecset", "szescset", "siedemset", "osiemset", "dziewiecset" };

        var result = "";

        if (intPart >= 1000)
        {
            var thousands = intPart / 1000;
            if (thousands == 1)
                result += "tysiac ";
            else if (thousands >= 2 && thousands <= 4)
                result += ones[thousands] + " tysiace ";
            else
                result += ones[thousands] + " tysiecy ";
            intPart %= 1000;
        }

        if (intPart >= 100)
        {
            result += hundreds[intPart / 100] + " ";
            intPart %= 100;
        }

        if (intPart >= 20)
        {
            result += tens[intPart / 10] + " ";
            intPart %= 10;
        }
        else if (intPart >= 10)
        {
            result += teens[intPart - 10] + " ";
            intPart = 0;
        }

        if (intPart > 0)
        {
            result += ones[intPart] + " ";
        }

        return result.Trim();
    }

    private int GetGrosze(decimal number)
    {
        return (int)((number - Math.Floor(number)) * 100);
    }
}
