namespace PlaySpace.Domain.DTOs;

public class TPayLegalFormDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; }
}

public class TPayCategoryDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; }
    public List<TPayCategoryDto> Children { get; set; } = new();
}

public class TPayDictionarySyncDto
{
    public string DictionaryType { get; set; }
    public DateTime LastSyncAt { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public int RecordsCount { get; set; }
    public TimeSpan TimeSinceLastSync => DateTime.UtcNow - LastSyncAt;
}

public class TPayDictionariesStatusDto
{
    public List<TPayDictionarySyncDto> SyncStatus { get; set; } = new();
    public bool AllDictionariesSynced => SyncStatus.All(s => s.IsSuccessful);
    public DateTime? LastSuccessfulSync => SyncStatus
        .Where(s => s.IsSuccessful)
        .OrderBy(s => s.LastSyncAt)
        .FirstOrDefault()?.LastSyncAt;
}