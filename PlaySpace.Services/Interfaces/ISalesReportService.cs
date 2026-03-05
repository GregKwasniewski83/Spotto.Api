using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface ISalesReportService
{
    Task<MonthlySalesReportDto> GetMonthlySalesReportAsync(Guid businessProfileId, Guid requestingUserId, int year, int month);
    Task<MonthlySalesReportDetailedDto> GetMonthlySalesReportDetailedAsync(Guid businessProfileId, Guid requestingUserId, int year, int month);
}
