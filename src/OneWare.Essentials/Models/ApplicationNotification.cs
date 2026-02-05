using System.Windows.Input;
using OneWare.Essentials.Enums;

namespace OneWare.Essentials.Models;

public class ApplicationNotification
{
    public ApplicationMessageKind Kind { get; init; }
        
    public required string Message { get; init; }
    
    public DateTime Timestamp { get; } = DateTime.Now;
    
    public ICommand? Command { get; init; }
    
    public bool IsRead { get; set; } = false;
}