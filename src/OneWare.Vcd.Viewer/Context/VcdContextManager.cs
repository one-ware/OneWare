using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.Vcd.Parser.Data;

namespace OneWare.Vcd.Viewer.Context;

public static class VcdContextManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static async Task<VcdContext?> LoadContextAsync(string path)
    {
        if (File.Exists(path))
            try
            {
                await using var stream = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<VcdContext>(stream, Options);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            }

        return null;
    }

    /// <summary>
    ///     Finds the VCD signals for the signals of <paramref name="context" />. Signals are matched by id, or by a
    ///     unique name if the id is missing or belongs to a signal with a different name. Unknown signals are skipped.
    /// </summary>
    public static IEnumerable<(VcdContextSignal ContextSignal, IVcdSignal Signal)> ResolveSignals(VcdContext context,
        IReadOnlyDictionary<string, IVcdSignal> signalRegister)
    {
        if (context.OpenSignals == null) yield break;

        Dictionary<string, IVcdSignal>? signalsByName = null;

        foreach (var contextSignal in context.OpenSignals)
        {
            if (signalRegister.TryGetValue(contextSignal.Id, out var signal) &&
                (contextSignal.Name == null || signal.Name == contextSignal.Name))
            {
                yield return (contextSignal, signal);
                continue;
            }

            if (contextSignal.Name == null) continue;

            signalsByName ??= signalRegister.Values
                .GroupBy(x => x.Name)
                .Where(x => x.Count() == 1)
                .ToDictionary(x => x.Key, x => x.First());

            if (signalsByName.TryGetValue(contextSignal.Name, out signal)) yield return (contextSignal, signal);
        }
    }

    public static async Task<bool> SaveContextAsync(string path, VcdContext context)
    {
        try
        {
            await using var stream = File.OpenWrite(path);
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, context, Options);
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }
}