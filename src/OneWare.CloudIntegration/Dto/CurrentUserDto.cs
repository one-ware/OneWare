namespace OneWare.CloudIntegration.Dto;

public class CurrentUserDto
{
    public string? Email { get; set; }
    
    public string? AvatarUrl { get; set; }
    
    public required bool IsProfileComplete { get; set; }
    
    public required UserPlanDto UserPlan { get; set; }
}