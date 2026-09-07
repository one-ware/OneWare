using System.Collections.ObjectModel;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Verilog.Models;

public class VerilogSymbolNode(string name, SymbolKind kind, string filePath, Range range)
{
    public string Name { get; } = name;
    public SymbolKind Kind { get; } = kind;
    public string FilePath { get; } = filePath;
    public Range Range { get; } = range;
    public ObservableCollection<VerilogSymbolNode> Children { get; } = new();
}
