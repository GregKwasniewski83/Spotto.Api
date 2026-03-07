using System.Text;
using System.Xml;
using System.Xml.Linq;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PlaySpace.Services.Services;

/// <summary>
/// Service for generating FA XML format for KSeF
///
/// Supports both FA(2) and FA(3) schemas:
/// - FA(2): Valid until January 31, 2026
/// - FA(3): Mandatory from February 1, 2026
///
/// Based on official schemas from Ministry of Finance:
/// https://ksef.podatki.gov.pl/informacje-ogolne-ksef-20/struktura-logiczna-fa-3/
/// </summary>
public class FAXmlGeneratorService : IFAXmlGeneratorService
{
    private readonly ILogger<FAXmlGeneratorService> _logger;

    // FA(2) schema - valid until January 31, 2026
    private const string FA2_NAMESPACE = "http://crd.gov.pl/wzor/2023/06/29/12648/";
    private const string FA2_SYSTEM_CODE = "FA (2)";
    private const string FA2_SCHEMA_VERSION = "1-0E";
    private const int FA2_VARIANT = 2;

    // FA(3) schema - mandatory from February 1, 2026
    // Official namespace from: https://crd.gov.pl/wzor/2025/06/25/13775/
    private const string FA3_NAMESPACE = "http://crd.gov.pl/wzor/2025/06/25/13775/";
    private const string FA3_SYSTEM_CODE = "FA (3)";
    private const string FA3_SCHEMA_VERSION = "1-0E";
    private const int FA3_VARIANT = 1;

    // Cutoff date for FA(3) - February 1, 2026
    private static readonly DateTime FA3_CUTOFF_DATE = new DateTime(2026, 2, 1);

    public FAXmlGeneratorService(ILogger<FAXmlGeneratorService> logger)
    {
        _logger = logger;
    }

    public string GenerateFAXml(KSeFInvoice invoice)
    {
        try
        {
            // Determine which schema to use based on invoice date
            bool useFA3 = invoice.IssueDate >= FA3_CUTOFF_DATE;
            var schemaVersion = useFA3 ? "FA(3)" : "FA(2)";

            _logger.LogInformation("Generating {Schema} XML for invoice: {InvoiceNumber} (date: {Date})",
                schemaVersion, invoice.InvoiceNumber, invoice.IssueDate.ToString("yyyy-MM-dd"));

            // Parse invoice items from JSON
            var items = string.IsNullOrEmpty(invoice.InvoiceItems)
                ? new List<KSeFInvoiceItem>()
                : JsonSerializer.Deserialize<List<KSeFInvoiceItem>>(invoice.InvoiceItems) ?? new List<KSeFInvoiceItem>();

            XNamespace ns = useFA3 ? FA3_NAMESPACE : FA2_NAMESPACE;
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            // Build FA XML structure according to official schema
            var faXml = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(ns + "Faktura",
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),

                    // NAGLOWEK (Header) - MANDATORY
                    BuildNaglowek(ns, invoice, useFA3),

                    // PODMIOT1 (Seller) - MANDATORY
                    BuildPodmiot1(ns, invoice),

                    // PODMIOT2 (Buyer) - MANDATORY
                    BuildPodmiot2(ns, invoice),

                    // FA (Invoice details) - MANDATORY
                    BuildFa(ns, invoice, items, useFA3)
                )
            );

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            using var stringWriter = new Utf8StringWriter();
            using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
            {
                faXml.Save(xmlWriter);
            }

            var xmlString = stringWriter.ToString();
            _logger.LogInformation("{Schema} XML generated successfully. Length: {Length}", schemaVersion, xmlString.Length);
            _logger.LogDebug("{Schema} XML content: {Xml}", schemaVersion, xmlString);

            return xmlString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating FA XML for invoice: {InvoiceNumber}", invoice.InvoiceNumber);
            throw;
        }
    }

    /// <summary>
    /// Builds Naglowek (Header) element
    /// Contains technical data about the invoice file
    /// </summary>
    private XElement BuildNaglowek(XNamespace ns, KSeFInvoice invoice, bool useFA3)
    {
        var systemCode = useFA3 ? FA3_SYSTEM_CODE : FA2_SYSTEM_CODE;
        var schemaVersion = useFA3 ? FA3_SCHEMA_VERSION : FA2_SCHEMA_VERSION;
        var variant = useFA3 ? FA3_VARIANT : FA2_VARIANT;

        return new XElement(ns + "Naglowek",
            // KodFormularza with required attributes
            new XElement(ns + "KodFormularza",
                new XAttribute("kodSystemowy", systemCode),
                new XAttribute("wersjaSchemy", schemaVersion),
                "FA"
            ),
            // WariantFormularza - variant 2 for FA(2), variant 1 for FA(3)
            new XElement(ns + "WariantFormularza", variant.ToString()),
            // DataWytworzeniaFa - file creation datetime
            new XElement(ns + "DataWytworzeniaFa", invoice.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")),
            // SystemInfo - optional but recommended
            new XElement(ns + "SystemInfo", "Spotto")
        );
    }

    /// <summary>
    /// Builds Podmiot1 (Seller) element - MANDATORY
    /// Contains seller identification and address
    /// </summary>
    private XElement BuildPodmiot1(XNamespace ns, KSeFInvoice invoice)
    {
        return new XElement(ns + "Podmiot1",
            // DaneIdentyfikacyjne - Seller identification
            new XElement(ns + "DaneIdentyfikacyjne",
                new XElement(ns + "NIP", CleanNip(invoice.SellerNIP)),
                new XElement(ns + "Nazwa", invoice.SellerName)
            ),
            // Adres - Seller address
            new XElement(ns + "Adres",
                new XElement(ns + "KodKraju", "PL"),
                new XElement(ns + "AdresL1", invoice.SellerAddress ?? "-"),
                new XElement(ns + "AdresL2", FormatAddressL2(invoice.SellerPostalCode, invoice.SellerCity))
            )
        );
    }

    /// <summary>
    /// Builds Podmiot2 (Buyer) element - MANDATORY
    /// Contains buyer identification and address
    /// For B2C (consumer without NIP), uses BrakID element
    /// </summary>
    private XElement BuildPodmiot2(XNamespace ns, KSeFInvoice invoice)
    {
        var daneIdentyfikacyjne = new XElement(ns + "DaneIdentyfikacyjne");

        if (!string.IsNullOrEmpty(invoice.BuyerNIP))
        {
            // B2B - buyer has NIP
            daneIdentyfikacyjne.Add(new XElement(ns + "NIP", CleanNip(invoice.BuyerNIP)));
        }
        else
        {
            // B2C - consumer without NIP
            daneIdentyfikacyjne.Add(new XElement(ns + "BrakID", "1"));
        }

        daneIdentyfikacyjne.Add(new XElement(ns + "Nazwa", invoice.BuyerName ?? "Konsument"));

        // Build address
        var adresL1 = string.IsNullOrWhiteSpace(invoice.BuyerAddress) ? "-" : invoice.BuyerAddress;
        var adresL2 = FormatAddressL2(invoice.BuyerPostalCode, invoice.BuyerCity);

        return new XElement(ns + "Podmiot2",
            daneIdentyfikacyjne,
            new XElement(ns + "Adres",
                new XElement(ns + "KodKraju", "PL"),
                new XElement(ns + "AdresL1", adresL1),
                new XElement(ns + "AdresL2", adresL2)
            )
        );
    }

    /// <summary>
    /// Builds Fa (Invoice details) element - MANDATORY
    /// Contains invoice data, amounts, and line items
    /// </summary>
    private XElement BuildFa(XNamespace ns, KSeFInvoice invoice, List<KSeFInvoiceItem> items, bool useFA3)
    {
        var faElement = new XElement(ns + "Fa",
            // KodWaluty - Currency code
            new XElement(ns + "KodWaluty", "PLN"),

            // P_1 - Issue date
            new XElement(ns + "P_1", invoice.IssueDate.ToString("yyyy-MM-dd")),

            // P_2 - Invoice number
            new XElement(ns + "P_2", invoice.InvoiceNumber),

            // P_6 - Sale date (same as issue date for services)
            new XElement(ns + "P_6", invoice.IssueDate.ToString("yyyy-MM-dd"))
        );

        // Add VAT summary based on rate
        // For 23% VAT rate
        if (invoice.VATRate == 23)
        {
            faElement.Add(new XElement(ns + "P_13_1", FormatDecimal(invoice.NetAmount)));
            faElement.Add(new XElement(ns + "P_14_1", FormatDecimal(invoice.VATAmount)));
        }
        // For 8% VAT rate
        else if (invoice.VATRate == 8)
        {
            faElement.Add(new XElement(ns + "P_13_2", FormatDecimal(invoice.NetAmount)));
            faElement.Add(new XElement(ns + "P_14_2", FormatDecimal(invoice.VATAmount)));
        }
        // For 5% VAT rate
        else if (invoice.VATRate == 5)
        {
            faElement.Add(new XElement(ns + "P_13_3", FormatDecimal(invoice.NetAmount)));
            faElement.Add(new XElement(ns + "P_14_3", FormatDecimal(invoice.VATAmount)));
        }
        // For 0% VAT rate
        else if (invoice.VATRate == 0)
        {
            faElement.Add(new XElement(ns + "P_13_6", FormatDecimal(invoice.NetAmount)));
        }
        else
        {
            // Default to 23% if unknown rate
            faElement.Add(new XElement(ns + "P_13_1", FormatDecimal(invoice.NetAmount)));
            faElement.Add(new XElement(ns + "P_14_1", FormatDecimal(invoice.VATAmount)));
        }

        // P_15 - Total gross amount
        faElement.Add(new XElement(ns + "P_15", FormatDecimal(invoice.GrossAmount)));

        // Adnotacje (Annotations) - required in FA(2)
        faElement.Add(new XElement(ns + "Adnotacje",
            new XElement(ns + "P_16", "2"), // 2 = not a self-billing invoice
            new XElement(ns + "P_17", "2"), // 2 = not a reverse charge
            new XElement(ns + "P_18", "2"), // 2 = not intra-community supply
            new XElement(ns + "P_18A", "2"), // 2 = not export
            new XElement(ns + "Zwolnienie",
                new XElement(ns + "P_19N", "1") // 1 = not exempt
            ),
            new XElement(ns + "NoweSrodkiTransportu",
                new XElement(ns + "P_22N", "1") // 1 = not new transport
            ),
            new XElement(ns + "P_23", "2"), // 2 = not simplified procedure
            new XElement(ns + "PMarzy",
                new XElement(ns + "P_PMarzyN", "1") // 1 = not margin procedure
            )
        ));

        // RodzajFaktury - Invoice type
        faElement.Add(new XElement(ns + "RodzajFaktury", "VAT"));

        // Add line items (FaWiersz)
        int lineNumber = 1;
        foreach (var item in items)
        {
            faElement.Add(BuildFaWiersz(ns, item, lineNumber++, useFA3));
        }

        // If no items, add a default line item from invoice totals
        if (!items.Any())
        {
            faElement.Add(new XElement(ns + "FaWiersz",
                new XElement(ns + "NrWierszaFa", "1"),
                new XElement(ns + "P_7", "Uslugi"),
                new XElement(ns + "P_8A", "usl"),  // P_8A = miara (unit)
                new XElement(ns + "P_8B", "1"),    // P_8B = ilość (quantity)
                new XElement(ns + "P_9A", FormatDecimal(invoice.NetAmount)),
                new XElement(ns + "P_11", FormatDecimal(invoice.NetAmount)),
                new XElement(ns + "P_12", invoice.VATRate.ToString())
            ));
        }

        // Platnosc - Payment information
        faElement.Add(new XElement(ns + "Platnosc",
            new XElement(ns + "Zaplacono", "1"), // 1 = paid
            new XElement(ns + "DataZaplaty", invoice.IssueDate.ToString("yyyy-MM-dd")),
            new XElement(ns + "FormaPlatnosci", "6") // 6 = electronic payment
        ));

        return faElement;
    }

    /// <summary>
    /// Builds FaWiersz (Invoice line item) element
    /// </summary>
    private XElement BuildFaWiersz(XNamespace ns, KSeFInvoiceItem item, int lineNumber, bool useFA3)
    {
        // Map Polish unit abbreviations to standard codes
        var unit = MapUnitToCode(item.Unit);

        // FA(3) allows 512 characters for item name, FA(2) allows 256
        var maxNameLength = useFA3 ? 512 : 256;

        return new XElement(ns + "FaWiersz",
            new XElement(ns + "NrWierszaFa", lineNumber.ToString()),
            new XElement(ns + "P_7", TruncateString(item.Name, maxNameLength)),
            new XElement(ns + "P_8A", unit),  // P_8A = miara (unit)
            new XElement(ns + "P_8B", FormatDecimal(item.Quantity)),  // P_8B = ilość (quantity)
            new XElement(ns + "P_9A", FormatDecimal(item.UnitPrice)),
            new XElement(ns + "P_11", FormatDecimal(item.NetAmount)),
            new XElement(ns + "P_12", item.VATRate.ToString())
        );
    }

    /// <summary>
    /// Maps Polish unit names to KSeF standard unit codes
    /// </summary>
    private string MapUnitToCode(string unit)
    {
        return unit?.ToLower() switch
        {
            "szt" or "szt." or "sztuka" or "sztuki" => "szt",
            "godz" or "godz." or "godzina" or "godziny" or "h" => "godz",
            "usl" or "usł" or "usluga" or "usługa" => "usl",
            "kg" or "kilogram" => "kg",
            "m" or "metr" => "m",
            "m2" or "m²" => "m2",
            "l" or "litr" => "l",
            _ => "usl" // Default to service
        };
    }

    /// <summary>
    /// Formats decimal for KSeF (max 2 decimal places, dot as separator)
    /// </summary>
    private string FormatDecimal(decimal value)
    {
        return value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Cleans NIP by removing dashes and spaces
    /// </summary>
    private string CleanNip(string? nip)
    {
        if (string.IsNullOrEmpty(nip))
            return "";

        return nip.Replace("-", "").Replace(" ", "").Trim();
    }

    /// <summary>
    /// Truncates string to max length
    /// </summary>
    private string TruncateString(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    /// <summary>
    /// Formats address line 2 (postal code + city)
    /// </summary>
    private string FormatAddressL2(string? postalCode, string? city)
    {
        var result = $"{postalCode ?? ""} {city ?? ""}".Trim();
        return string.IsNullOrEmpty(result) ? "-" : result;
    }

    public FAXmlValidationResult ValidateFAXml(string faXml)
    {
        try
        {
            var result = new FAXmlValidationResult { IsValid = true };

            try
            {
                var doc = XDocument.Parse(faXml);

                // Check for mandatory elements according to FA schema
                var mandatoryElements = new Dictionary<string, string>
                {
                    { "Naglowek", "Header" },
                    { "Podmiot1", "Seller" },
                    { "Podmiot2", "Buyer" },
                    { "Fa", "Invoice details" },
                    { "KodFormularza", "Form code" },
                    { "WariantFormularza", "Form variant" },
                    { "P_1", "Issue date" },
                    { "P_2", "Invoice number" },
                    { "P_15", "Gross amount" },
                    { "KodWaluty", "Currency code" },
                    { "NrWierszaFa", "Line number" },
                    { "Adnotacje", "Annotations" },
                    { "RodzajFaktury", "Invoice type" }
                };

                foreach (var element in mandatoryElements)
                {
                    if (!doc.Descendants().Any(e => e.Name.LocalName == element.Key))
                    {
                        result.Errors.Add($"Required element '{element.Key}' ({element.Value}) is missing");
                        result.IsValid = false;
                    }
                }

                // Validate NIP in Podmiot1
                var podmiot1Nip = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Podmiot1")?
                    .Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "NIP")?.Value;

                if (string.IsNullOrEmpty(podmiot1Nip))
                {
                    result.Errors.Add("Seller NIP (Podmiot1/DaneIdentyfikacyjne/NIP) is required");
                    result.IsValid = false;
                }
                else if (!ValidateNip(podmiot1Nip))
                {
                    result.Warnings.Add($"Seller NIP '{podmiot1Nip}' may be invalid (checksum verification)");
                }

                // Validate amounts consistency
                var p15 = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "P_15")?.Value;
                var p13_1 = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "P_13_1")?.Value;
                var p14_1 = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "P_14_1")?.Value;

                if (p15 != null && p13_1 != null && p14_1 != null)
                {
                    if (decimal.TryParse(p15, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gross) &&
                        decimal.TryParse(p13_1, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var net) &&
                        decimal.TryParse(p14_1, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var vat))
                    {
                        var calculatedGross = net + vat;
                        if (Math.Abs(gross - calculatedGross) > 0.01m)
                        {
                            result.Warnings.Add($"Gross amount ({gross}) doesn't match Net ({net}) + VAT ({vat}) = {calculatedGross}");
                        }
                    }
                }
            }
            catch (XmlException ex)
            {
                result.IsValid = false;
                result.Errors.Add($"XML parsing error: {ex.Message}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating FA XML");
            return new FAXmlValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Validation error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Validates Polish NIP checksum
    /// </summary>
    private bool ValidateNip(string nip)
    {
        nip = CleanNip(nip);

        if (nip.Length != 10 || !nip.All(char.IsDigit))
            return false;

        int[] weights = { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
        int sum = 0;

        for (int i = 0; i < 9; i++)
        {
            sum += (nip[i] - '0') * weights[i];
        }

        int checkDigit = sum % 11;
        if (checkDigit == 10)
            checkDigit = 0;

        return (nip[9] - '0') == checkDigit;
    }

    /// <summary>
    /// Helper class to force UTF-8 encoding without BOM
    /// </summary>
    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
