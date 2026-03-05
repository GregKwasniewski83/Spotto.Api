namespace PlaySpace.Domain.DTOs
{
    public class SearchFiltersDto
    {
        public string? Type { get; set; }
        public string? City { get; set; }
        public DateTime? Date { get; set; }
        public string? Name { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? MinCapacity { get; set; }
        public string? Country { get; set; } = "Polska";
        public string? State { get; set; }
        public bool? OnlyAvailable { get; set; } = false;
    }
}
