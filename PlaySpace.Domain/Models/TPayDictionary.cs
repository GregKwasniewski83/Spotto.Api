namespace PlaySpace.Domain.Models;

public class TPayLegalForm
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}

public class TPayCategory  
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    
    // Navigation properties
    public TPayCategory? Parent { get; set; }
    public List<TPayCategory> Children { get; set; } = new();
}

public class TPayDictionarySync
{
    public Guid Id { get; set; }
    public required string DictionaryType { get; set; } // "LegalForms" or "Categories"
    public DateTime LastSyncAt { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public int RecordsCount { get; set; }
    public string? SyncVersion { get; set; }
}