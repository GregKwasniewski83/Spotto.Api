using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class SalesReportService : ISalesReportService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IProductPurchaseRepository _productPurchaseRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IBusinessProfileAgentRepository _agentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SalesReportService> _logger;

    public SalesReportService(
        IReservationRepository reservationRepository,
        IProductPurchaseRepository productPurchaseRepository,
        IBusinessProfileRepository businessProfileRepository,
        IBusinessProfileAgentRepository agentRepository,
        IUserRepository userRepository,
        ILogger<SalesReportService> logger)
    {
        _reservationRepository = reservationRepository;
        _productPurchaseRepository = productPurchaseRepository;
        _businessProfileRepository = businessProfileRepository;
        _agentRepository = agentRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<MonthlySalesReportDto> GetMonthlySalesReportAsync(
        Guid businessProfileId, Guid requestingUserId, int year, int month)
    {
        var (businessProfile, reservations, purchases) =
            await FetchReportDataAsync(businessProfileId, requestingUserId, year, month);

        return BuildSummary(businessProfile, reservations, purchases, year, month);
    }

    public async Task<MonthlySalesReportDetailedDto> GetMonthlySalesReportDetailedAsync(
        Guid businessProfileId, Guid requestingUserId, int year, int month)
    {
        var (businessProfile, reservations, purchases) =
            await FetchReportDataAsync(businessProfileId, requestingUserId, year, month);

        var summary = BuildSummary(businessProfile, reservations, purchases, year, month);

        var detailed = new MonthlySalesReportDetailedDto
        {
            Year = summary.Year,
            Month = summary.Month,
            MonthName = summary.MonthName,
            BusinessProfileId = summary.BusinessProfileId,
            BusinessName = summary.BusinessName,
            TotalRevenue = summary.TotalRevenue,
            ReservationRevenue = summary.ReservationRevenue,
            ProductPurchaseRevenue = summary.ProductPurchaseRevenue,
            RefundedAmount = summary.RefundedAmount,
            TotalReservations = summary.TotalReservations,
            CancelledReservations = summary.CancelledReservations,
            ProductPurchaseCount = summary.ProductPurchaseCount,
            FacilityBreakdown = summary.FacilityBreakdown,
            Reservations = await MapReservationsAsync(reservations),
            ProductPurchases = await MapPurchasesAsync(purchases)
        };

        return detailed;
    }

    // --- Private helpers ---

    private async Task<(BusinessProfile businessProfile, List<Reservation> reservations, List<ProductPurchase> purchases)>
        FetchReportDataAsync(Guid businessProfileId, Guid requestingUserId, int year, int month)
    {
        _logger.LogInformation(
            "Generating monthly sales report for business {BusinessProfileId}, {Year}-{Month}, requested by {UserId}",
            businessProfileId, year, month, requestingUserId);

        var businessProfile = await _businessProfileRepository.GetBusinessProfileByIdAsync(businessProfileId);
        if (businessProfile == null)
            throw new NotFoundException("BusinessProfile", businessProfileId.ToString());

        var isOwner = businessProfile.UserId == requestingUserId;
        var isAgent = await _agentRepository.IsAgentActiveForBusinessAsync(requestingUserId, businessProfileId);

        if (!isOwner && !isAgent)
            throw new UnauthorizedAccessException("You are not authorized to view reports for this business");

        var reservations = await _reservationRepository.GetMonthlyReservationsForBusinessAsync(businessProfileId, year, month);
        var purchases = await _productPurchaseRepository.GetMonthlyPurchasesForBusinessAsync(businessProfileId, year, month);

        return (businessProfile, reservations, purchases);
    }

    private static MonthlySalesReportDto BuildSummary(
        BusinessProfile businessProfile,
        List<Reservation> reservations,
        List<ProductPurchase> purchases,
        int year, int month)
    {
        var active = reservations.Where(r => r.Status != "Cancelled").ToList();
        var cancelled = reservations.Where(r => r.Status == "Cancelled").ToList();

        var reservationRevenue = active.Sum(r => r.TotalPrice);
        var productRevenue = purchases.Sum(p => p.Price);

        var facilityBreakdown = active
            .GroupBy(r => new { r.FacilityId, Name = r.Facility?.Name ?? "Unknown" })
            .Select(g => new FacilitySalesDto
            {
                FacilityId = g.Key.FacilityId,
                FacilityName = g.Key.Name,
                Revenue = g.Sum(r => r.TotalPrice),
                ReservationCount = g.Count(),
                CancelledCount = cancelled.Count(r => r.FacilityId == g.Key.FacilityId)
            })
            .OrderByDescending(f => f.Revenue)
            .ToList();

        return new MonthlySalesReportDto
        {
            Year = year,
            Month = month,
            MonthName = new DateTime(year, month, 1).ToString("MMMM yyyy"),
            BusinessProfileId = businessProfile.Id,
            BusinessName = businessProfile.DisplayName ?? string.Empty,
            TotalRevenue = reservationRevenue + productRevenue,
            ReservationRevenue = reservationRevenue,
            ProductPurchaseRevenue = productRevenue,
            RefundedAmount = 0,
            TotalReservations = active.Count,
            CancelledReservations = cancelled.Count,
            ProductPurchaseCount = purchases.Count,
            FacilityBreakdown = facilityBreakdown
        };
    }

    private async Task<List<ReservationSaleItemDto>> MapReservationsAsync(List<Reservation> reservations)
    {
        var result = new List<ReservationSaleItemDto>();

        foreach (var r in reservations.OrderByDescending(r => r.Date))
        {
            string? customerName = r.GuestName;
            string? customerEmail = r.GuestEmail;

            if (customerName == null && r.UserId.HasValue)
            {
                var user = await _userRepository.GetUserByIdAsync(r.UserId.Value);
                if (user != null)
                {
                    customerName = $"{user.FirstName} {user.LastName}".Trim();
                    customerEmail = user.Email;
                }
            }

            result.Add(new ReservationSaleItemDto
            {
                Id = r.Id,
                Date = r.Date,
                TimeSlots = r.TimeSlots,
                FacilityId = r.FacilityId,
                FacilityName = r.Facility?.Name ?? string.Empty,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                TotalPrice = r.TotalPrice,
                RemainingPrice = r.RemainingPrice,
                Status = r.Status,
                NumberOfUsers = r.NumberOfUsers,
                PaidForAllUsers = r.PaidForAllUsers,
                PaidWithProduct = r.ProductPurchaseId.HasValue,
                PaidOnline = r.PaymentId.HasValue,
                CreatedByName = r.CreatedBy != null
                    ? $"{r.CreatedBy.FirstName} {r.CreatedBy.LastName}".Trim()
                    : null,
                GroupId = r.GroupId
            });
        }

        return result;
    }

    private async Task<List<ProductPurchaseSaleItemDto>> MapPurchasesAsync(List<ProductPurchase> purchases)
    {
        var result = new List<ProductPurchaseSaleItemDto>();

        foreach (var p in purchases.OrderByDescending(p => p.PurchaseDate))
        {
            var user = await _userRepository.GetUserByIdAsync(p.UserId);
            var customerName = user != null
                ? $"{user.FirstName} {user.LastName}".Trim()
                : string.Empty;

            result.Add(new ProductPurchaseSaleItemDto
            {
                Id = p.Id,
                PurchaseDate = p.PurchaseDate,
                ProductTitle = p.ProductTitle,
                CustomerName = customerName,
                Price = p.Price,
                Status = p.Status,
                TotalUsage = p.TotalUsage,
                RemainingUsage = p.RemainingUsage,
                ExpiryDate = p.ExpiryDate
            });
        }

        return result;
    }
}
