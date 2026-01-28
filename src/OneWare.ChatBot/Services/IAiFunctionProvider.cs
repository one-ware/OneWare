using System.Collections.Generic;
using Microsoft.Extensions.AI;

namespace OneWare.ChatBot.Services;

public interface IAiFunctionProvider
{
    event EventHandler<string>? FunctionUsed;
    ICollection<AIFunction> GetTools();
}
