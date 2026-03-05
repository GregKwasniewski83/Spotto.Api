namespace PlaySpace.Domain.DTOs;

public class MonthlySalesReportDetailedDto : MonthlySalesReportDto
{
    public List<ReservationSaleItemDto> Reservations { get; set; } = new();
    public List<ProductPurchaseSaleItemDto> ProductPurchases { get; set; } = new();
}

public class ReservationSaleItemDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal RemainingPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public int NumberOfUsers { get; set; }
    public bool PaidForAllUsers { get; set; }
    public bool PaidWithProduct { get; set; }
    public bool PaidOnline { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? GroupId { get; set; }
}

public class ProductPurchaseSaleItemDto
{
    public Guid Id { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalUsage { get; set; }
    public int RemainingUsage { get; set; }
    public DateTime ExpiryDate { get; set; }
}

public class MonthlySalesReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public Guid BusinessProfileId { get; set; }
    public string BusinessName { get; set; } = string.Empty;

    // Revenue totals
    public decimal TotalRevenue { get; set; }
    public decimal ReservationRevenue { get; set; }
    public decimal ProductPurchaseRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue => TotalRevenue - RefundedAmount;

    // Counts
    public int TotalReservations { get; set; }
    public int CancelledReservations { get; set; }
    public int ProductPurchaseCount { get; set; }

    // Per-facility breakdown
    public List<FacilitySalesDto> FacilityBreakdown { get; set; } = new();
}

public class FacilitySalesDto
{
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int ReservationCount { get; set; }
    public int CancelledCount { get; set; }
}
