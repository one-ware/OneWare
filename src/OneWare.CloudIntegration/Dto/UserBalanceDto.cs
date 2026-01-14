namespace OneWare.CloudIntegration.Dto;

public class UserBalanceDto
{
    public required int Balance { get; init; }
    
    public required int IncludedMonthlyCreditsUsed { get; init; }
}