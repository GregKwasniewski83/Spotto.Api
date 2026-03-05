namespace PlaySpace.Domain.DTOs;

public class CityDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Province { get; set; }
    public string District { get; set; }
    public string Commune { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}