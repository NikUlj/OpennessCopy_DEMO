using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.Safety;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace OpennessCopy.Services.BlockCopy;

public static class ExportService
{
    public static void DisplayAndExportGroupContents(PlcBlockUserGroup group, string prefix, string path, PlcSoftware plcSoftware = null, SafetyAdministration safetyAdministration = null, SecureString safetyPassword = null)
    {
        List<PlcBlock> blocks = new List<PlcBlock>();
        bool hasCompiledPLC = false;
        
        // Display blocks in this group
        if (group.Blocks.Count > 0)
        {
            blocks.AddRange(group.Blocks.ToList());
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                string blockIcon = GetBlockIcon(block);
                bool isLastBlock = i == blocks.Count - 1 && group.Groups.Count == 0;
                string connector = isLastBlock ? "└──" : "├──";
                Logger.LogInfo($"{prefix}{connector} {blockIcon} {block.Name} (#{block.Number})");
                    
                try
                {
                    string sanitizedBlockName = MiscUtil.SanitizeFileName(block.Name);
                    string fileName = $"{sanitizedBlockName}.xml";
                    string filePath = Path.Combine(path, fileName);
                    block.Export(new FileInfo(filePath), ExportOptions.WithDefaults);
                }
                catch (Exception e)
                {
                    if (IsInconsistencyError(e) && plcSoftware != null && !hasCompiledPLC)
                    {
                        Logger.LogInfo($"Inconsistency error detected for block {block.Name}. Compiling PLC...");
                        try
                        {
                            ValidationService.CompileSafe(plcSoftware, safetyAdministration, safetyPassword);
                            Logger.LogInfo("PLC compilation successful. Retrying block export...");
                        }
                        catch (Exception compilationEx)
                        {
                            Logger.LogInfo($"PLC compilation had errors: {compilationEx.Message}");
                            Logger.LogInfo("Attempting block export anyway - needed UDTs may still be compiled...");
                        }
                        hasCompiledPLC = true; // Prevent further compilation attempts
                        i--; // Retry the same block regardless of compilation result
                    }
                    else
                    {
                        Logger.LogError($"Error while exporting block {block.Name}: {e.Message}", false);
                        throw;
                    }
                }
            }
        }
        
        // Display subgroups
        if (group.Groups.Count > 0)
        {
            var subGroups = group.Groups.ToList();
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                bool isLastGroup = i == subGroups.Count - 1;
                string connector = isLastGroup ? "└──" : "├──";
                string childPrefix = prefix + (isLastGroup ? "    " : "│   ");
                    
                string exportDir = Path.Combine(path, subGroup.Name);
                Directory.CreateDirectory(exportDir);
                
                Logger.LogInfo($"{prefix}{connector} [+] {subGroup.Name}");
                DisplayAndExportGroupContents(subGroup, childPrefix, exportDir, plcSoftware, safetyAdministration, safetyPassword);
            }
        }
        
        if (group.Blocks.Count == 0 && group.Groups.Count == 0)
        {
            Logger.LogInfo($"{prefix}└── (empty)");
        }
    }

    private static string GetBlockIcon(PlcBlock block)
    {
        return block.ProgrammingLanguage switch
        {
            ProgrammingLanguage.LAD => "[LAD]",
            ProgrammingLanguage.FBD => "[FBD]",
            ProgrammingLanguage.STL => "[STL]",
            ProgrammingLanguage.SCL => "[SCL]",
            ProgrammingLanguage.GRAPH => "[GRF]",
            ProgrammingLanguage.CFC => "[CFC]",
            _ => "[BLK]"
        };
    }

    /// <summary>
    /// Checks if an exception is related to inconsistency issues that can be resolved by compilation
    /// </summary>
    private static bool IsInconsistencyError(Exception ex)
    {
        if (ex == null) return false;
        
        string message = ex.Message?.ToLower() ?? "";
        
        return message.Contains("inconsistent") && message.Contains("udt");
    }
}