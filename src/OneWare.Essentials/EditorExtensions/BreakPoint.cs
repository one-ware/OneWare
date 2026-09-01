namespace OneWare.Essentials.EditorExtensions;

public class BreakPoint
{
    public required string File { get; set; }

    public int Line { get; set; }
    
    public bool IsVerified { get; set; } = true;
}