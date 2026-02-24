#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpennessCopy.Models;
using OpennessCopy.Services.BlockSelection;
using OpennessCopy.Services.CodeBuilders.SimaticSd;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace OpennessCopy.Services.CodeBuilders;

public static class ConveyorDbProcessor
{
    private static List<GeneratedArtifact> HandleGenerateOrAppend(
        PlcSoftware plc,
        DbConfig config,
        string exportRoot,
        Action<string> infoLogger,
        Action<string> errorLogger,
        Func<IReadOnlyList<GeneratedArtifact>> dbGenerator)
    {
        var artifacts = new List<GeneratedArtifact>();
        var name = config.ManualName;
        var found = PlcSearchHelpers.FindBlockByName(plc, name, [DbConfigModeToKind(config)]);
        if (found != null && config.AppendIfExists)
        {
            infoLogger($"DB '{name}' exists. Appending variables.");
            config.SelectedBlock = new PlcBlockInfo
            {
                BlockId = DataCacheUtility.CacheObject(found),
                Name = found.Name,
                BlockNumber = found.Number,
                Kind = DbConfigModeToKind(config)
            };
            EditExistingDb(plc, config, exportRoot, infoLogger, errorLogger);
            return artifacts;
        }

        infoLogger($"DB '{name}' not found. Generating new DB.");
        try
        {
            artifacts.AddRange(dbGenerator.Invoke());
        }
        catch (Exception ex)
        {
            errorLogger($"Failed to generate DB '{name}': {ex.Message}");
        }
        return artifacts;
    }

    private static void EditExistingDb(
        PlcSoftware plc,
        DbConfig config,
        string exportRoot,
        Action<string> infoLogger,
        Action<string> errorLogger)
    {
        var blockInfo = config.SelectedBlock;
        if (blockInfo == null)
        {
            errorLogger("No DB selected for editing.");
            return;
        }

        PlcBlock? block = DataCacheUtility.GetCachedObject<PlcBlock>(blockInfo.BlockId);
        block ??= PlcSearchHelpers.FindBlockByName(plc, blockInfo.Name,
            new HashSet<PlcBlockKind> { PlcBlockKind.GlobalDb, PlcBlockKind.InstanceDb, PlcBlockKind.ArrayDb });
        if (block == null)
        {
            errorLogger($"DB '{blockInfo.Name}' not found for editing.");
            return;
        }

        var tempDir = Path.Combine(exportRoot, "DbEdits");
        Directory.CreateDirectory(tempDir);
        var exportDir = Path.Combine(tempDir, block.Name);
        Directory.CreateDirectory(exportDir);
        var exportPath = Path.Combine(exportDir, $"{block.Name}.s7dcl");

        try
        {
            block.ExportAsDocuments(new DirectoryInfo(exportDir), block.Name);
            var content = File.ReadAllText(exportPath);

            if (config.Variables != null)
                foreach (var variableSpec in config.Variables)
                {
                    if (config.Key == DbConfigKey.TsubStatus)
                    {
                        content = StatusDbEditor.AppendStatusEntries(content, variableSpec.VariableLines);
                    }
                    else
                    {
                        content = SimaticSdDbEditor.AppendVariables(
                            content,
                            variableSpec.VariableLines,
                            string.IsNullOrWhiteSpace(variableSpec.StructName) ? null : variableSpec.StructName);
                    }
                }

            File.WriteAllText(exportPath, content);

            var parent = ((IEngineeringObject)block).Parent as PlcBlockGroup;
            PlcBlockComposition parentBlocks = parent?.Blocks ?? plc.BlockGroup.Blocks;
            string name = block.Name;
            parentBlocks.ImportFromDocuments(new DirectoryInfo(exportDir), name, ImportDocumentOptions.Override);

            infoLogger($"DB '{name}' updated successfully.");
        }
        catch (Exception ex)
        {
            errorLogger($"Failed to edit DB '{blockInfo.Name}': {ex.Message}");
        }
    }

    private static PlcBlockKind DbConfigModeToKind(DbConfig config)
    {
        return PlcBlockKind.GlobalDb; // default to global DB; adjust if needed per config
    }
}
