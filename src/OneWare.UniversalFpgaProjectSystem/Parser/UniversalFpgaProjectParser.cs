﻿using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Parser;

public static class UniversalFpgaProjectParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true
    };

    public static async Task<UniversalFpgaProjectRoot?> DeserializeAsync(string path, ILogger logger)
    {
        try
        {
            await using var stream = File.OpenRead(path);

            var properties = await JsonNode.ParseAsync(stream, null, new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            });

            var root = new UniversalFpgaProjectRoot(path, properties!.AsObject());
            return root;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return null;
        }
    }

    public static async Task<bool> SerializeAsync(UniversalFpgaProjectRoot root, ILogger logger)
    {
        try
        {
            await using (var stream = File.OpenWrite(root.ProjectFilePath))
            {
                stream.SetLength(0);
                await JsonSerializer.SerializeAsync(stream, root.Properties, SerializerOptions);
            }

            root.LastSaveTime = DateTime.Now;
            return true;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
    }
}
