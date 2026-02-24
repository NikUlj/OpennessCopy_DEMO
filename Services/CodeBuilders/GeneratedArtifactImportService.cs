using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpennessCopy.Services.CodeBuilders.SimaticSd;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders;

public static class GeneratedArtifactImportService
{
    public static void WriteFilesAndImport(
        PlcSoftware plcSoftware,
        string exportRoot,
        IEnumerable<GeneratedArtifact> artifacts,
        Action<string> infoLogger,
        Action<string> errorLogger,
        CancellationToken token)
    {
        if (plcSoftware == null)
        {
            throw new ArgumentNullException(nameof(plcSoftware));
        }

        if (exportRoot == null)
        {
            throw new ArgumentNullException(nameof(exportRoot));
        }

        if (artifacts == null)
        {
            throw new ArgumentNullException(nameof(artifacts));
        }

        Directory.CreateDirectory(exportRoot);

        var importTargets = new Dictionary<string, ImportTargetKind>(StringComparer.OrdinalIgnoreCase);
        var xmlImports = new List<XmlImportEntry>();

        foreach (var artifact in artifacts)
        {
            token.ThrowIfCancellationRequested();

            var normalizedRelativePath = NormalizeRelativePath(artifact.RelativePath);
            var absolutePath = Path.Combine(exportRoot, normalizedRelativePath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolutePath, artifact.Content);

            var normalizedDirectory = GetNormalizedDirectory(normalizedRelativePath);
            if (string.IsNullOrWhiteSpace(normalizedDirectory))
            {
                continue;
            }
            var directoryKey = normalizedDirectory!;

            switch (artifact.Kind)
            {
                case SimaticSdArtifactKinds.BlockCode:
                    RegisterImportTarget(importTargets, directoryKey, ImportTargetKind.Block);
                    break;
                case SimaticSdArtifactKinds.UdtCode:
                    RegisterImportTarget(importTargets, directoryKey, ImportTargetKind.Type);
                    break;
                case SimaticSdArtifactKinds.XmlCode:
                    xmlImports.Add(new XmlImportEntry(directoryKey, normalizedRelativePath));
                    break;
            }
        }

        var failedPaths = new List<string>();
        ImportSimaticSdArtifacts(plcSoftware, exportRoot, importTargets, infoLogger, errorLogger, token, failedPaths);
        ImportXmlArtifacts(plcSoftware, exportRoot, xmlImports, infoLogger, errorLogger, token, failedPaths);
        PersistFailedArtifacts(failedPaths, infoLogger, errorLogger);
    }

    private static void ImportSimaticSdArtifacts(
        PlcSoftware plcSoftware,
        string exportRoot,
        IReadOnlyDictionary<string, ImportTargetKind> importTargets,
        Action<string> infoLogger,
        Action<string> errorLogger,
        CancellationToken token,
        List<string> failedPaths)
    {
        if (importTargets.Count == 0)
        {
            return;
        }

        foreach (var kvp in importTargets)
        {
            token.ThrowIfCancellationRequested();

            var relativeDir = kvp.Key;
            if (string.IsNullOrWhiteSpace(relativeDir))
            {
                continue;
            }

            var segments = SplitSegments(relativeDir);
            if (segments.Length == 0)
            {
                continue;
            }

            var artifactName = segments[segments.Length - 1];
            if (string.IsNullOrWhiteSpace(artifactName))
            {
                continue;
            }

            var groupSegments = segments.Take(segments.Length - 1).ToArray();
            var absoluteDir = Path.Combine(exportRoot, relativeDir);
            if (!Directory.Exists(absoluteDir))
            {
                errorLogger($"Artifact directory not found for import: {absoluteDir}");
                continue;
            }

            switch (kvp.Value)
            {
                case ImportTargetKind.Block:
                    ImportSimaticSdBlock(plcSoftware, artifactName, groupSegments, absoluteDir, infoLogger, errorLogger, failedPaths);
                    break;
                case ImportTargetKind.Type:
                    ImportSimaticSdType(plcSoftware, artifactName, groupSegments, absoluteDir, infoLogger, errorLogger, failedPaths);
                    break;
                default:
                    errorLogger($"Unsupported import target '{kvp.Value}' for '{artifactName}'.");
                    break;
            }
        }
    }

    private static void ImportSimaticSdBlock(
        PlcSoftware plcSoftware,
        string blockName,
        IReadOnlyList<string> groupSegments,
        string absoluteDir,
        Action<string> infoLogger,
        Action<string> errorLogger,
        List<string> failedPaths)
    {
        var targetBlocks = ResolveTargetBlockComposition(plcSoftware, groupSegments);
        infoLogger($"Importing block '{blockName}' into PLC '{plcSoftware.Name}' (group: {FormatGroupPath(groupSegments)})");

        var result = targetBlocks.ImportFromDocuments(
            new DirectoryInfo(absoluteDir),
            blockName,
            ImportDocumentOptions.Override);

        LogImportResult(blockName, result, infoLogger, errorLogger, failedPaths, absoluteDir);
    }

    private static void ImportSimaticSdType(
        PlcSoftware plcSoftware,
        string typeName,
        IReadOnlyList<string> groupSegments,
        string absoluteDir,
        Action<string> infoLogger,
        Action<string> errorLogger,
        List<string> failedPaths)
    {
        var targetTypes = ResolveTargetTypeComposition(plcSoftware, groupSegments);
        infoLogger($"Importing UDT '{typeName}' into PLC '{plcSoftware.Name}' (group: {FormatGroupPath(groupSegments)})");

        var result = targetTypes.ImportFromDocuments(
            new DirectoryInfo(absoluteDir),
            typeName,
            ImportDocumentOptions.Override);

        LogImportResult(typeName, result, infoLogger, errorLogger, failedPaths, absoluteDir);
    }

    private static void ImportXmlArtifacts(
        PlcSoftware plcSoftware,
        string exportRoot,
        IReadOnlyList<XmlImportEntry> xmlImports,
        Action<string> infoLogger,
        Action<string> errorLogger,
        CancellationToken token,
        List<string> failedPaths)
    {
        if (xmlImports.Count == 0)
        {
            return;
        }

        foreach (var entry in xmlImports)
        {
            token.ThrowIfCancellationRequested();

            var filePath = Path.Combine(exportRoot, entry.RelativeFilePath);
            if (!File.Exists(filePath))
            {
                errorLogger($"XML file not found for import: {filePath}");
                continue;
            }

            var segments = SplitSegments(entry.RelativeDirectory);
            if (segments.Length == 0)
            {
                errorLogger($"XML import path invalid: {entry.RelativeDirectory}");
                continue;
            }

            var blockName = Path.GetFileNameWithoutExtension(entry.RelativeFilePath);
            var groupSegments = segments.Take(segments.Length - 1).ToArray();
            var targetBlocks = ResolveTargetBlockComposition(plcSoftware, groupSegments);

            infoLogger($"Importing XML block '{blockName}' from '{entry.RelativeFilePath}'.");

            try
            {
                targetBlocks.Import(
                    new FileInfo(filePath),
                    ImportOptions.Override,
                    SWImportOptions.IgnoreMissingReferencedObjects);
            }
            catch (Exception ex)
            {
                errorLogger($"Failed to import XML block '{blockName}': {ex.Message}");
                failedPaths.Add(filePath);
            }
        }
    }

    private static void PersistFailedArtifacts(
        IReadOnlyList<string> failedPaths,
        Action<string> infoLogger,
        Action<string> errorLogger)
    {
        if (failedPaths.Count == 0)
        {
            return;
        }

        var failureBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpennessCopy",
            "ImportFailures");
        Directory.CreateDirectory(failureBase);

        var runFolderName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var debugRoot = Path.Combine(failureBase, runFolderName);
        Directory.CreateDirectory(debugRoot);

        var uniquePaths = failedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in uniquePaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var dirName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                    if (string.IsNullOrWhiteSpace(dirName))
                    {
                        dirName = "Artifact";
                    }

                    var targetDir = GetUniqueDirectoryPath(debugRoot, dirName);
                    CopyDirectory(path, targetDir);
                    infoLogger($"Copied failed artifact directory to '{targetDir}'.");
                }
                else if (File.Exists(path))
                {
                    var fileName = Path.GetFileName(path);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        fileName = $"Artifact_{DateTime.Now:yyyyMMddHHmmssfff}.dat";
                    }

                    var targetFile = GetUniqueFilePath(debugRoot, fileName);
                    File.Copy(path, targetFile, overwrite: false);
                    infoLogger($"Copied failed artifact file to '{targetFile}'.");
                }
            }
            catch (Exception ex)
            {
                errorLogger($"Failed to preserve failed artifact '{path}': {ex.Message}");
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, targetSubDir);
        }
    }

    private static string GetUniqueDirectoryPath(string root, string directoryName)
    {
        var candidate = Path.Combine(root, directoryName);
        var counter = 1;
        while (Directory.Exists(candidate))
        {
            candidate = Path.Combine(root, $"{directoryName}_{counter++}");
        }

        return candidate;
    }

    private static string GetUniqueFilePath(string root, string fileName)
    {
        var candidate = Path.Combine(root, fileName);
        var counter = 1;
        while (File.Exists(candidate))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            candidate = Path.Combine(root, $"{baseName}_{counter++}{extension}");
        }

        return candidate;
    }

    private static bool IsSuccessfulState(object? state)
    {
        var text = state?.ToString();
        return string.Equals(text, "Success", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Ok", StringComparison.OrdinalIgnoreCase);
    }

    private static void TrackFailedPath(List<string> failedPaths, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!failedPaths.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
        {
            failedPaths.Add(path);
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private static string? GetNormalizedDirectory(string normalizedPath)
    {
        var directory = Path.GetDirectoryName(normalizedPath);
        return string.IsNullOrWhiteSpace(directory)
            ? null
            : directory.Trim(Path.DirectorySeparatorChar);
    }

    private static string[] SplitSegments(string path)
    {
        return path
            .Split([Path.DirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
    }

    private static void RegisterImportTarget(
        IDictionary<string, ImportTargetKind> importTargets,
        string relativeDirectory,
        ImportTargetKind targetKind)
    {
        if (string.IsNullOrWhiteSpace(relativeDirectory))
        {
            return;
        }

        if (importTargets.TryGetValue(relativeDirectory, out var existingKind))
        {
            if (existingKind == targetKind)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Conflicting import targets for directory '{relativeDirectory}'.");
        }

        importTargets[relativeDirectory] = targetKind;
    }

    private static PlcBlockComposition ResolveTargetBlockComposition(
        PlcSoftware plcSoftware,
        IReadOnlyList<string> groupSegments)
    {
        if (groupSegments.Count == 0)
        {
            return plcSoftware.BlockGroup.Blocks;
        }

        PlcBlockUserGroup? currentGroup = null;

        foreach (var segment in groupSegments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            currentGroup = currentGroup == null
                ? EnsureGroup(plcSoftware.BlockGroup.Groups, segment)
                : EnsureGroup(currentGroup.Groups, segment);
        }

        return currentGroup != null
            ? currentGroup.Blocks
            : plcSoftware.BlockGroup.Blocks;
    }

    private static PlcTypeComposition ResolveTargetTypeComposition(
        PlcSoftware plcSoftware,
        IReadOnlyList<string> groupSegments)
    {
        if (groupSegments.Count == 0)
        {
            return plcSoftware.TypeGroup.Types;
        }

        PlcTypeUserGroup? currentGroup = null;

        foreach (var segment in groupSegments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            currentGroup = currentGroup == null
                ? EnsureTypeGroup(plcSoftware.TypeGroup.Groups, segment)
                : EnsureTypeGroup(currentGroup.Groups, segment);
        }

        return currentGroup != null
            ? currentGroup.Types
            : plcSoftware.TypeGroup.Types;
    }

    private static PlcBlockUserGroup EnsureGroup(
        PlcBlockUserGroupComposition groups,
        string groupName)
    {
        foreach (PlcBlockUserGroup group in groups)
        {
            if (group.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
            {
                return group;
            }
        }

        return groups.Create(groupName);
    }

    private static PlcTypeUserGroup EnsureTypeGroup(
        PlcTypeUserGroupComposition groups,
        string groupName)
    {
        foreach (PlcTypeUserGroup group in groups)
        {
            if (group.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
            {
                return group;
            }
        }

        return groups.Create(groupName);
    }

    private static void LogImportResult(
        string artifactName,
        DocumentImportResultForBlocks result,
        Action<string> infoLogger,
        Action<string> errorLogger,
        List<string> failedPaths,
        string absoluteDir)
    {
        var log = GetLoggerForState(result.State, infoLogger, errorLogger);
        log($"Import state for '{artifactName}': {result.State}");

        foreach (var message in result.Messages)
        {
            log($"Import message: {message.Message}");
        }

        if (!IsSuccessfulState(result.State))
        {
            TrackFailedPath(failedPaths, absoluteDir);
        }
    }

    private static void LogImportResult(
        string artifactName,
        DocumentImportResultForTypes result,
        Action<string> infoLogger,
        Action<string> errorLogger,
        List<string> failedPaths,
        string absoluteDir)
    {
        var log = GetLoggerForState(result.State, infoLogger, errorLogger);
        log($"Import state for '{artifactName}': {result.State}");

        foreach (var message in result.Messages)
        {
            log($"Import message: {message.Message}");
        }

        if (!IsSuccessfulState(result.State))
        {
            TrackFailedPath(failedPaths, absoluteDir);
        }
    }

    private static Action<string> GetLoggerForState(
        object? state,
        Action<string> infoLogger,
        Action<string> errorLogger)
    {
        return IsSuccessfulState(state) ? infoLogger : errorLogger;
    }

    private static string FormatGroupPath(IReadOnlyList<string> groupSegments)
    {
        return groupSegments.Count == 0
            ? "<root>"
            : string.Join("/", groupSegments);
    }

    private sealed class XmlImportEntry(string relativeDirectory, string relativeFilePath)
    {
        public string RelativeDirectory { get; } = relativeDirectory;

        public string RelativeFilePath { get; } = relativeFilePath;
    }

    private enum ImportTargetKind
    {
        Block,
        Type
    }
}
