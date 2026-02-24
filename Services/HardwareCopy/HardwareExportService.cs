using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.Cax;
using Siemens.Engineering.HW;

namespace OpennessCopy.Services.HardwareCopy;

/// <summary>
/// Service for exporting hardware devices to AML format
/// </summary>
public static class HardwareExportService
{
    /// <summary>
    /// Exports a hardware device to AML format using TIA Portal Openness API
    /// </summary>
    /// <param name="deviceInfo">Hardware device information</param>
    /// <param name="exportDirectory">Directory to export the AML files to</param>
    /// <param name="deviceMapping">Optional mapping populated with the device export folder name and the transformed name.</param>
    /// <param name="transformedName">Optional transformed device name to record in <paramref name="deviceMapping"/>.</param>
    /// <returns>Path to the exported AML file</returns>
    private static string ExportDeviceToAml(
        HardwareDeviceInfo deviceInfo, 
        string exportDirectory, 
        Dictionary<string, (HardwareDeviceInfo deviceInfo, string transformedName)> deviceMapping = null, 
        string transformedName = null)
    {
        try
        {
            Logger.LogInfo($"Starting AML export for device: {deviceInfo.Name}");

            // Ensure export directory exists
            if (!Directory.Exists(exportDirectory))
            {
                Directory.CreateDirectory(exportDirectory);
                Logger.LogInfo($"Created export directory: {exportDirectory}");
            }

            // Generate device-specific subfolder and file names using existing sanitization method
            // Use ItemName if available, otherwise use device Name
            var nameForFile = !string.IsNullOrWhiteSpace(deviceInfo.ItemName) ? deviceInfo.ItemName : deviceInfo.Name;
            var sanitizedDeviceName = MiscUtil.SanitizeFileName(nameForFile);
            var sanitizedDeviceType = MiscUtil.SanitizeFileName(deviceInfo.DeviceType ?? "UnknownType");
            var deviceFolderName = $"{sanitizedDeviceName}_{sanitizedDeviceType}";
            var amlFileName = $"{sanitizedDeviceName}_{sanitizedDeviceType}.aml";
            var logFileName = $"{sanitizedDeviceName}_{sanitizedDeviceType}_Log.log";

            // Populate device mapping if provided
            if (deviceMapping != null && !string.IsNullOrEmpty(transformedName))
            {
                deviceMapping[deviceFolderName] = (deviceInfo, transformedName);
                Logger.LogInfo($"Added device mapping: {deviceFolderName} -> {deviceInfo.Name} (transformed: {transformedName})");
            }

            // Create device-specific subfolder
            var deviceExportDirectory = Path.Combine(exportDirectory, deviceFolderName);
            if (!Directory.Exists(deviceExportDirectory))
            {
                Directory.CreateDirectory(deviceExportDirectory);
                Logger.LogInfo($"Created device export directory: {deviceExportDirectory}");
            }

            var amlFilePath = Path.Combine(deviceExportDirectory, amlFileName);
            var logFilePath = Path.Combine(deviceExportDirectory, logFileName);

            // Retrieve actual Device object from cache using cache ID
            var actualDevice = DataCacheUtility.GetCachedObject<Device>(deviceInfo.DeviceId);
            if (actualDevice == null)
            {
                throw new InvalidOperationException($"Device object not found in cache for device: {deviceInfo.Name}");
            }

            // Retrieve project from cache using cache ID
            var project = DataCacheUtility.GetCachedObject<Project>(deviceInfo.ProjectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project object not found in cache for device: {deviceInfo.Name}");
            }

            // Get CaxProvider service for AML export
            var caxProvider = project.GetService<CaxProvider>();
            if (caxProvider == null)
            {
                throw new InvalidOperationException("CaxProvider service not available for AML export");
            }

            // Create FileInfo objects for export
            var amlFile = new FileInfo(amlFilePath);
            var logFile = new FileInfo(logFilePath);

            // Perform AML export
            Logger.LogInfo($"Exporting device '{deviceInfo.Name}' to AML using CaxProvider...");
            bool exportSuccess = caxProvider.Export(actualDevice, amlFile, logFile);

            if (!exportSuccess)
            {
                var errorMessage = "AML export completed but returned false (check log file for details)";
                throw new InvalidOperationException(errorMessage);
            }

            // Validate the export
            if (!ValidateAmlExport(amlFilePath, logFilePath))
            {
                throw new InvalidOperationException("AML export validation failed");
            }

            Logger.LogSuccess($"Hardware device exported successfully to: {amlFilePath}");
            return amlFilePath;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error exporting hardware device '{deviceInfo.Name}' to AML: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Exports a hardware device to AML format with device name and IP address transformations
    /// Populates the device mapping dictionary for later use during import
    /// </summary>
    /// <param name="deviceInfo">Hardware device information</param>
    /// <param name="exportDirectory">Directory to export the AML files to</param>
    /// <param name="findReplacePairs">Find/replace pairs for device name transformations</param>
    /// <param name="ipAddressOffset">Offset to apply to IP address last byte</param>
    /// <param name="etAddressReplacements">ET module address transformation rules</param>
    /// <param name="deviceMapping">Dictionary to populate with folder name to device info and transformed name mapping</param>
    /// <returns>Path to the exported AML file</returns>
    public static string ExportDeviceToAml(HardwareDeviceInfo deviceInfo, string exportDirectory, List<FindReplacePair> findReplacePairs, int ipAddressOffset, List<TagAddressReplacePair> etAddressReplacements, Dictionary<string, (HardwareDeviceInfo deviceInfo, string transformedName)> deviceMapping)
    {
        try
        {
            // Calculate transformed name first
            var transformationSource = !string.IsNullOrWhiteSpace(deviceInfo.ItemName) ? deviceInfo.ItemName : deviceInfo.Name;
            var transformedName = ApplyFindReplaceToDeviceName(transformationSource, findReplacePairs);

            // Export using the standard method and populate mapping
            var amlFilePath = ExportDeviceToAml(deviceInfo, exportDirectory, deviceMapping, transformedName);

            // Check if any transformations are needed
            var hasDeviceNameTransformations = findReplacePairs is { Count: > 0 };
            var hasIpAddressTransformations = ipAddressOffset != 0 && deviceInfo.IpAddresses.Count > 0;
            var hasETAddressTransformations = etAddressReplacements is { Count: > 0 } && deviceInfo.IsETDevice;

            if (!hasDeviceNameTransformations && !hasIpAddressTransformations && !hasETAddressTransformations)
            {
                Logger.LogInfo($"No transformations specified for device: {deviceInfo.Name}");
                return amlFilePath;
            }

            Logger.LogInfo($"Applying transformations to AML file: {amlFilePath} (DeviceNames: {hasDeviceNameTransformations}, IP: {hasIpAddressTransformations}, ET: {hasETAddressTransformations})");

            // Read the AML content
            var amlContent = File.ReadAllText(amlFilePath);

            // Apply device name transformations first (if specified)
            if (hasDeviceNameTransformations)
            {
                Logger.LogInfo($"Applying {findReplacePairs.Count} device name transformations");
                amlContent = AMLProcessingService.ProcessAMLWithDeviceNameReplacements(amlContent, deviceInfo, findReplacePairs);
            }

            // Apply IP address transformations (if specified)
            if (hasIpAddressTransformations)
            {
                Logger.LogInfo($"Applying IP address transformations with offset {ipAddressOffset:+0;-0;0}");
                amlContent = AMLProcessingService.ProcessAMLWithIpAddressModifications(amlContent, deviceInfo, ipAddressOffset);
            }

            // Apply ET module address transformations (if specified)
            if (hasETAddressTransformations)
            {
                Logger.LogInfo($"Applying {etAddressReplacements.Count} ET module address transformations");
                amlContent = AMLProcessingService.ProcessAMLWithETModuleAddressTransformations(amlContent, deviceInfo, etAddressReplacements);
            }

            // Write the transformed content back to the same file (in-place modification)
            File.WriteAllText(amlFilePath, amlContent);

            Logger.LogInfo($"All transformations applied successfully to: {amlFilePath}");
            return amlFilePath;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error applying device name transformations to AML export: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Validates that the exported AML file exists, has content, and is properly formatted
    /// Also checks the corresponding log file for errors
    /// </summary>
    private static bool ValidateAmlExport(string amlFilePath, string logFilePath = null)
    {
        try
        {
            // Check if AML file exists
            if (!File.Exists(amlFilePath))
            {
                Logger.LogError($"AML export validation failed: File does not exist at {amlFilePath}", false);
                return false;
            }

            // Check if AML file has content
            var fileInfo = new FileInfo(amlFilePath);
            if (fileInfo.Length == 0)
            {
                Logger.LogError($"AML export validation failed: File is empty at {amlFilePath}", false);
                return false;
            }

            // Check log file for errors if provided
            if (!string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath))
            {
                if (!ValidateLogFile(logFilePath))
                {
                    return false;
                }
            }

            // Basic XML validation
            try
            {
                var doc = XDocument.Load(amlFilePath);

                // Check for basic AML structure
                if (doc.Root == null || doc.Root.Name.LocalName != "CAEXFile")
                {
                    Logger.LogError($"AML export validation failed: Invalid AML file structure (missing CAEXFile root)", false);
                    return false;
                }

                Logger.LogSuccess($"AML export validation successful: {Path.GetFileName(amlFilePath)} ({fileInfo.Length} bytes)");
                return true;
            }
            catch (Exception xmlEx)
            {
                Logger.LogError($"AML export validation failed: File is not valid XML - {xmlEx.Message}", false);
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error validating AML export: {ex.Message}", false);
            return false;
        }
    }

    /// <summary>
    /// Validates the log file for errors and warnings by checking the last line
    /// Expected format: "DD.MM.YYYY HH:mm:ss PM: INFO : Exporting of the CAx data is completed (errors: 0, warnings: 0)"
    /// </summary>
    private static bool ValidateLogFile(string logFilePath)
    {
        try
        {
            if (!File.Exists(logFilePath))
            {
                Logger.LogWarning($"Log file does not exist: {logFilePath}");
                return true; // Not critical if log file is missing
            }

            var logLines = File.ReadAllLines(logFilePath);
            if (logLines.Length == 0)
            {
                Logger.LogInfo("Export log file is empty");
                return true;
            }

            // Get the last line which contains the summary information
            string summaryLine = logLines[logLines.Length - 1];

            Logger.LogInfo($"Checking log summary line: {summaryLine}");

            // Parse the summary line to extract error and warning counts
            // Expected format: "22.09.2025 01:54:37 PM: INFO : Exporting of the CAx data is completed (errors: 0, warnings: 0)"
            var lowerSummaryLine = summaryLine.ToLower();

            if (!lowerSummaryLine.Contains("exporting of the cax data is completed"))
            {
                Logger.LogWarning($"Log summary line format not recognized: {summaryLine}");
                return true; // Don't fail if format is unexpected
            }

            // Extract error count using regex
            var errorMatch = System.Text.RegularExpressions.Regex.Match(summaryLine, @"errors:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var warningMatch = System.Text.RegularExpressions.Regex.Match(summaryLine, @"warnings:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            int errorCount = 0;
            int warningCount = 0;

            if (!errorMatch.Success || !int.TryParse(errorMatch.Groups[1].Value, out errorCount))
            {
                Logger.LogWarning("Could not parse error count from log summary line");
            }

            if (!warningMatch.Success || !int.TryParse(warningMatch.Groups[1].Value, out warningCount))
            {
                Logger.LogWarning("Could not parse warning count from log summary line");
            }

            // Fail validation if there are any errors
            if (errorCount > 0)
            {
                Logger.LogError($"AML export validation failed: {errorCount} errors found in export log", false);
                return false;
            }

            // Single consolidated log message for the validation result
            if (warningCount > 0)
            {
                Logger.LogWarning($"Export log validation successful with warnings: {errorCount} errors, {warningCount} warnings");
            }
            else
            {
                Logger.LogSuccess($"Export log validation successful: {errorCount} errors, {warningCount} warnings");
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error reading log file: {ex.Message}");
            return true; // Don't fail validation if we can't read the log
        }
    }

    /// <summary>
    /// Applies find/replace pairs to a device name
    /// </summary>
    /// <param name="deviceName">Original device name</param>
    /// <param name="findReplacePairs">Find/replace pairs to apply</param>
    /// <returns>Transformed device name</returns>
    private static string ApplyFindReplaceToDeviceName(string deviceName, List<FindReplacePair> findReplacePairs)
    {
        if (string.IsNullOrWhiteSpace(deviceName) || findReplacePairs == null || findReplacePairs.Count == 0)
            return deviceName;

        var result = deviceName;

        foreach (var pair in findReplacePairs)
        {
            if (!string.IsNullOrWhiteSpace(pair.FindString))
            {
                result = result.Replace(pair.FindString, pair.ReplaceString ?? "");
            }
        }

        return result;
    }
}
