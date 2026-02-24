using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace OpennessCopy.Services.BlockCopy;

public static class ImportService
{
    public static void ImportFolderStructure(string exportPath, PlcSoftware targetPlcSoftware, CancellationToken cancellationToken)
    {
        Logger.LogInfo($"Starting import from: {exportPath}");

        string originalGroupName = Path.GetFileName(exportPath);
        PlcBlockUserGroup targetGroup = null;
        int attemptCount = 1;
        const int maxAttempts = 10;

        // Try to create group with original name, retry with modified names if it exists
        while (targetGroup == null)
        {
            string groupName = attemptCount == 1 ? originalGroupName : $"{originalGroupName}_{attemptCount}";
            
            try
            {
                targetGroup = targetPlcSoftware.BlockGroup.Groups.Create(groupName);
                Logger.LogInfo($"Created group: {groupName}");
                break;
            }
            catch (Exception ex)
            {
                // Check if the error is due to duplicate name
                if (ex.Message.Contains("exist") || ex.Message.Contains("duplicate") || 
                    ex.Message.Contains("already") || ex.Message.Contains("name"))
                {
                    Logger.LogWarning($"Group name conflict detected: {groupName} (attempt {attemptCount})");
                    attemptCount++;
                    
                    if (attemptCount > maxAttempts)
                    {
                        throw new InvalidOperationException($"Failed to create group after {maxAttempts} attempts due to naming conflicts. Last error: {ex.Message}");
                    }
                }
                else
                {
                    // Re-throw if it's not a naming conflict
                    throw;
                }
            }
        }

        if (targetGroup == null)
        {
            throw new InvalidOperationException($"Failed to create group after {maxAttempts} attempts");
        }

        // Recursively import this group
        ImportGroupStructure(exportPath, targetGroup, cancellationToken);
    }

    private static void ImportGroupStructure(string directoryPath, PlcBlockUserGroup targetGroup, CancellationToken cancellationToken)
    {
        // Import blocks in current directory
        ImportBlocksInDirectory(directoryPath, targetGroup.Blocks, cancellationToken);

        // Process subdirectories as subgroups
        var subdirectories = Directory.GetDirectories(directoryPath);
        foreach (var subDir in subdirectories)
        {
            string subGroupName = Path.GetFileName(subDir);
            var targetSubGroup = targetGroup.Groups.Create(subGroupName);
            Logger.LogInfo($"Created subgroup: {subGroupName}");

            // Recursively import this subgroup
            ImportGroupStructure(subDir, targetSubGroup, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static void ImportBlocksInDirectory(string directoryPath, PlcBlockComposition blockComposition, CancellationToken cancellationToken)
    {
        // Find all XML files in the directory
        var xmlFiles = Directory.GetFiles(directoryPath, "*.xml");

        foreach (var xmlFile in xmlFiles)
        {
            try
            {
                Logger.LogInfo($"Importing: {Path.GetFileName(xmlFile)}");

                // Import the block
                IList<PlcBlock> importedBlocks = blockComposition.Import(
                    new FileInfo(xmlFile),
                    ImportOptions.Override,
                    SWImportOptions.IgnoreMissingReferencedObjects
                );

                // Rename blocks to replace '`' with '/' 
                foreach (var block in importedBlocks)
                {
                    if (block.Name.Contains('`'))
                    {
                        string originalName = block.Name;
                        string newName = originalName.Replace('`', '/');

                        try
                        {
                            block.SetAttribute("Name", newName);
                            Logger.LogInfo($"Renamed: {originalName} -> {newName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogInfo($"Could not rename {originalName}: {ex.Message}");
                        }
                    }
                }

                Logger.LogInfo($"Imported {importedBlocks.Count} block(s) from {Path.GetFileName(xmlFile)}");

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning($"Import of {Path.GetFileName(xmlFile)} was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to import {Path.GetFileName(xmlFile)}: {ex.Message}", false);
                Logger.LogInfo("Continuing with next block...");
                // Continue to next block instead of stopping the entire import
            }
        }
    }
}
