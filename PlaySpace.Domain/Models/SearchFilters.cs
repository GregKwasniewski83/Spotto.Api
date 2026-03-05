public class SearchFilters
{
    public Guid Id { get; set; }
    public string Query { get; set; }
    public List<string> Categories { get; set; }
    public Dictionary<string, string> AdvancedFilters { get; set; }
}
