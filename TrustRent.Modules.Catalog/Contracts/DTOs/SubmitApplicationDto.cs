namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class SubmitApplicationDto
{
    public string Message { get; set; } = string.Empty;
    public bool ShareProfile { get; set; }
    public bool WantsVisit { get; set; }
    public int DurationMonths { get; set; }
    public List<string> SelectedDates { get; set; } = new();
}
