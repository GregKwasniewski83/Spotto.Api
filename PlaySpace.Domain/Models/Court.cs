public class Court
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public string Specifications { get; set; }
    public List<Equipment> Equipment { get; set; }
}
