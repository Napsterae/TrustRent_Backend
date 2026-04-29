namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class SubmitApplicationDto
{
    public string Message { get; set; } = string.Empty;
    public bool ShareProfile { get; set; }
    public bool WantsVisit { get; set; }
    public int DurationMonths { get; set; }
    public List<string> SelectedDates { get; set; } = new();

    /// <summary>
    /// Email do co-candidato a convidar (opcional). Quando preenchido, a candidatura
    /// arranca em estado conjunto e fica suspensa até o convidado aceitar.
    /// </summary>
    public string? CoTenantEmail { get; set; }
}
