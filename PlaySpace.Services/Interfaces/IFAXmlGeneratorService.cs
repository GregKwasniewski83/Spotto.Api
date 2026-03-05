using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

/// <summary>
/// Service for generating FA (Faktura) XML format required by KSeF
/// FA is the Polish structured invoice format mandated by the Ministry of Finance
/// </summary>
public interface IFAXmlGeneratorService
{
    /// <summary>
    /// Generate FA (Faktura) XML from a KSeFInvoice entity
    /// </summary>
    /// <param name="invoice">Invoice entity with all required data</param>
    /// <returns>XML string in FA format compatible with KSeF</returns>
    string GenerateFAXml(KSeFInvoice invoice);

    /// <summary>
    /// Validate FA XML against KSeF schema
    /// </summary>
    /// <param name="faXml">FA XML content to validate</param>
    /// <returns>Validation result with any errors</returns>
    FAXmlValidationResult ValidateFAXml(string faXml);
}
