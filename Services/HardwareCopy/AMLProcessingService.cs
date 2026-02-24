using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OpennessCopy.Models;
using OpennessCopy.Utils;

namespace OpennessCopy.Services.HardwareCopy;

/// <summary>
/// Service for processing AML files and applying device name transformations
/// Handles find/replace operations on device names in AML XML content
/// </summary>
public static class AMLProcessingService
{
    /// <summary>
    /// Applies device name find/replace transformations to AML content using device info
    /// Uses device item name when available, falls back to device name
    /// </summary>
    /// <param name="amlContent">Original AML XML content</param>
    /// <param name="deviceInfo">Device information containing names</param>
    /// <param name="findReplacePairs">List of find/replace pairs to apply</param>
    /// <returns>Transformed AML content with device names replaced</returns>
    public static string ProcessAMLWithDeviceNameReplacements(string amlContent, HardwareDeviceInfo deviceInfo, List<FindReplacePair> findReplacePairs)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(amlContent))
            {
                Logger.LogWarning("AML content is empty or null");
                return amlContent;
            }

            if (deviceInfo == null)
            {
                Logger.LogWarning("Device info is null");
                return amlContent;
            }

            if (findReplacePairs == null || findReplacePairs.Count == 0)
            {
                Logger.LogInfo("No find/replace pairs specified, returning original AML content");
                return amlContent;
            }

            // Determine transformation source: ItemName if available, otherwise device Name
            var transformationSource = !string.IsNullOrWhiteSpace(deviceInfo.ItemName)
                ? deviceInfo.ItemName
                : deviceInfo.Name;

            Logger.LogInfo($"Processing AML for device '{deviceInfo.Name}' using transformation source '{transformationSource}' with {findReplacePairs.Count} find/replace pairs");

            // Apply transformations to get the target name
            var transformedName = ApplyFindReplaceToDeviceName(transformationSource, findReplacePairs);

            if (transformationSource == transformedName)
            {
                Logger.LogInfo($"No transformation needed for device '{deviceInfo.Name}'");
                return amlContent;
            }

            Logger.LogInfo($"Device transformation: '{transformationSource}' -> '{transformedName}'");

            var processedContent = amlContent;
            var replacementCount = 0;

            // Replace both device name and item name (if different) with the transformed name
            if (!string.IsNullOrWhiteSpace(deviceInfo.Name))
            {
                processedContent = ReplaceDeviceNameInAML(processedContent, deviceInfo.Name, transformedName, ref replacementCount);
            }

            if (!string.IsNullOrWhiteSpace(deviceInfo.ItemName) && deviceInfo.ItemName != deviceInfo.Name)
            {
                processedContent = ReplaceDeviceNameInAML(processedContent, deviceInfo.ItemName, transformedName, ref replacementCount);
            }

            // Validate that the processed content is still valid XML
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                XDocument.Parse(processedContent);
                Logger.LogInfo($"AML processing completed successfully. {replacementCount} device names transformed. XML validation passed.");
            }
            catch (Exception xmlEx)
            {
                Logger.LogError($"XML validation failed after processing: {xmlEx.Message}");
                throw new InvalidOperationException("AML processing resulted in invalid XML content", xmlEx);
            }

            return processedContent;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing AML content: {ex.Message}");
            throw new InvalidOperationException($"Failed to process AML content: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies find/replace pairs to a single device name
    /// </summary>
    /// <param name="deviceName">Original device name</param>
    /// <param name="findReplacePairs">Find/replace pairs to apply</param>
    /// <returns>Transformed device name</returns>
    private static string ApplyFindReplaceToDeviceName(string deviceName, List<FindReplacePair> findReplacePairs)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return deviceName;

        var result = deviceName;

        foreach (var pair in findReplacePairs)
        {
            if (!string.IsNullOrWhiteSpace(pair.FindString))
            {
                var originalResult = result;
                result = result.Replace(pair.FindString, pair.ReplaceString ?? "");

                // Log individual replacements for debugging
                if (originalResult != result)
                {
                    Logger.LogInfo($"Applied find/replace: '{pair.FindString}' -> '{pair.ReplaceString}' in device name");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Replaces specific device name in AML content using regex
    /// </summary>
    /// <param name="amlContent">AML content to process</param>
    /// <param name="originalName">Original device name to replace</param>
    /// <param name="transformedName">New device name</param>
    /// <param name="replacementCount">Reference to replacement counter</param>
    /// <returns>Processed AML content</returns>
    private static string ReplaceDeviceNameInAML(string amlContent, string originalName, string transformedName, ref int replacementCount)
    {
        if (string.IsNullOrWhiteSpace(originalName) || string.IsNullOrWhiteSpace(transformedName))
            return amlContent;

        // Create a specific regex pattern for this device name to avoid partial matches
        var specificPattern = @"(<InternalElement\s+ID=""[^""]*""\s+Name="")" + Regex.Escape(originalName) + @"(""[^>]*>)";
        var specificRegex = new Regex(specificPattern, RegexOptions.IgnoreCase);

        var localReplacementCount = 0;
        var processedContent = specificRegex.Replace(amlContent, match =>
        {
            var prefix = match.Groups[1].Value;
            var suffix = match.Groups[2].Value;
            localReplacementCount++;
            Logger.LogInfo($"Replaced device name: '{originalName}' -> '{transformedName}'");
            return prefix + transformedName + suffix;
        });

        replacementCount += localReplacementCount;
        return processedContent;
    }

    /// <summary>
    /// Applies IP address modifications to AML content based on the provided offset
    /// Finds NetworkAddress attributes and modifies the last byte of IP addresses
    /// </summary>
    /// <param name="amlContent">Original AML XML content</param>
    /// <param name="deviceInfo">Device information containing original IP addresses</param>
    /// <param name="ipOffset">Offset to apply to the last byte of IP addresses</param>
    /// <returns>AML content with modified IP addresses</returns>
    public static string ProcessAMLWithIpAddressModifications(string amlContent, HardwareDeviceInfo deviceInfo, int ipOffset)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(amlContent))
            {
                Logger.LogWarning("AML content is empty or null");
                return amlContent;
            }

            if (ipOffset == 0)
            {
                Logger.LogInfo($"IP offset is 0 for device '{deviceInfo.Name}', no IP address changes needed");
                return amlContent;
            }

            if (deviceInfo.IpAddresses.Count == 0)
            {
                Logger.LogInfo($"Device '{deviceInfo.Name}' has no IP addresses, no changes needed");
                return amlContent;
            }

            Logger.LogInfo($"Processing AML for device '{deviceInfo.Name}' with IP offset {ipOffset:+0;-0;0} on {deviceInfo.IpAddresses.Count} IP addresses");

            var doc = XDocument.Parse(amlContent);
            var replacementCount = 0;

            // Find all NetworkAddress attributes
            var networkAddressElements = doc.Descendants("Attribute")
                .Where(attr => attr.Attribute("Name")?.Value == "NetworkAddress")
                .ToList();

            Logger.LogInfo($"Found {networkAddressElements.Count} NetworkAddress elements in AML");

            foreach (var element in networkAddressElements)
            {
                var valueElement = element.Element("Value");
                if (valueElement == null) continue;

                var currentIpAddress = valueElement.Value;

                // Check if this IP address is one of the device's original IP addresses
                if (deviceInfo.IpAddresses.Contains(currentIpAddress))
                {
                    var newIpAddress = ModifyIpAddress(currentIpAddress, ipOffset);
                    if (newIpAddress != currentIpAddress)
                    {
                        valueElement.Value = newIpAddress;
                        replacementCount++;
                        Logger.LogInfo($"Updated IP address: '{currentIpAddress}' -> '{newIpAddress}'");
                    }
                }
            }

            var processedContent = doc.ToString();

            // Validate XML structure is preserved
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                XDocument.Parse(processedContent);
                Logger.LogInfo($"AML IP address processing completed successfully. {replacementCount} IP addresses modified. XML validation passed.");
            }
            catch (Exception xmlEx)
            {
                Logger.LogError($"XML validation failed after IP address processing: {xmlEx.Message}");
                throw new InvalidOperationException("AML IP address processing resulted in invalid XML content", xmlEx);
            }

            return processedContent;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing AML IP addresses: {ex.Message}");
            throw new InvalidOperationException($"Failed to process AML IP addresses: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Modifies an IP address by applying an offset to the last byte
    /// </summary>
    /// <param name="ipAddress">Original IP address (e.g., "192.168.1.100")</param>
    /// <param name="offset">Offset to apply to last byte</param>
    /// <returns>Modified IP address</returns>
    private static string ModifyIpAddress(string ipAddress, int offset)
    {
        try
        {
            var parts = ipAddress.Split('.');
            if (parts.Length != 4)
            {
                Logger.LogWarning($"Invalid IP address format: '{ipAddress}', skipping modification");
                return ipAddress;
            }

            if (!int.TryParse(parts[3], out int lastByte))
            {
                Logger.LogWarning($"Invalid last byte in IP address '{ipAddress}', skipping modification");
                return ipAddress;
            }

            var newLastByte = lastByte + offset;

            // Bounds checking (should have been validated before, but safety check)
            if (newLastByte < 0 || newLastByte > 255)
            {
                Logger.LogError($"IP address modification would result in invalid last byte: {lastByte} + {offset} = {newLastByte}");
                throw new ArgumentOutOfRangeException(nameof(offset), $"IP address modification results in out-of-bounds value: {newLastByte}");
            }

            return $"{parts[0]}.{parts[1]}.{parts[2]}.{newLastByte}";
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error modifying IP address '{ipAddress}' with offset {offset}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Applies ET module address transformations to AML content
    /// Finds StartAddress attributes and applies digit-based transformations
    /// </summary>
    /// <param name="amlContent">Original AML XML content</param>
    /// <param name="deviceInfo">Device information</param>
    /// <param name="etAddressReplacements">List of address transformation rules</param>
    /// <returns>AML content with modified start addresses</returns>
    public static string ProcessAMLWithETModuleAddressTransformations(string amlContent, HardwareDeviceInfo deviceInfo, List<TagAddressReplacePair> etAddressReplacements)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(amlContent))
            {
                Logger.LogWarning("AML content is empty or null");
                return amlContent;
            }

            if (!deviceInfo.IsETDevice)
            {
                Logger.LogInfo($"Device '{deviceInfo.Name}' is not an ET device, no address transformations needed");
                return amlContent;
            }

            if (etAddressReplacements == null || etAddressReplacements.Count == 0)
            {
                Logger.LogInfo($"No ET address transformations specified for device '{deviceInfo.Name}', returning original AML content");
                return amlContent;
            }

            Logger.LogInfo($"Processing AML for ET device '{deviceInfo.Name}' with {etAddressReplacements.Count} address transformation rules");

            var doc = XDocument.Parse(amlContent);
            var replacementCount = 0;

            // Find all StartAddress attributes in the AML
            var startAddressElements = doc.Descendants("Attribute")
                .Where(attr => attr.Attribute("Name")?.Value == "StartAddress")
                .ToList();

            Logger.LogInfo($"Found {startAddressElements.Count} StartAddress elements in AML");

            foreach (var element in startAddressElements)
            {
                var valueElement = element.Element("Value");
                if (valueElement == null) continue;

                var currentAddress = valueElement.Value;
                var transformedAddress = ApplyETAddressTransformations(currentAddress, etAddressReplacements);

                if (transformedAddress != currentAddress)
                {
                    valueElement.Value = transformedAddress;
                    replacementCount++;
                    Logger.LogInfo($"Updated ET module start address: {currentAddress} -> {transformedAddress}");
                }
            }

            var processedContent = doc.ToString();

            // Validate XML structure is preserved
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                XDocument.Parse(processedContent);
                Logger.LogInfo($"AML ET address processing completed successfully. {replacementCount} start addresses modified. XML validation passed.");
            }
            catch (Exception xmlEx)
            {
                Logger.LogError($"XML validation failed after ET address processing: {xmlEx.Message}");
                throw new InvalidOperationException("AML ET address processing resulted in invalid XML content", xmlEx);
            }

            return processedContent;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing AML ET module addresses: {ex.Message}");
            throw new InvalidOperationException($"Failed to process AML ET module addresses: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies address transformation rules to a single ET module address
    /// </summary>
    /// <param name="address">Original address as string</param>
    /// <param name="etAddressReplacements">List of transformation rules</param>
    /// <returns>Transformed address as string</returns>
    private static string ApplyETAddressTransformations(string address, List<TagAddressReplacePair> etAddressReplacements)
    {
        var currentAddress = address;

        foreach (var pair in etAddressReplacements)
        {
            currentAddress = ProcessAddressTransformation(currentAddress, pair);
        }

        return currentAddress;
    }

    /// <summary>
    /// Processes a single address transformation using TagAddressReplacePair logic
    /// </summary>
    /// <param name="address">Address as string</param>
    /// <param name="pair">Transformation rule</param>
    /// <returns>Transformed address as string</returns>
    private static string ProcessAddressTransformation(string address, TagAddressReplacePair pair)
    {
        // Check length filter - only process addresses with specified digit count
        if (address.Length == pair.LengthFilter)
        {
            // Apply replacement to specific digit position (right-to-left counting)
            if (pair.DigitPosition > 0 && pair.DigitPosition <= address.Length && !string.IsNullOrEmpty(pair.FindString))
            {
                int findLength = pair.FindString.Length;
                int rightmostIndex = address.Length - pair.DigitPosition;
                int leftmostIndex = rightmostIndex - findLength + 1;

                // Check if we have enough digits to the left for the find string
                if (leftmostIndex >= 0 && rightmostIndex < address.Length)
                {
                    // Extract the substring to compare
                    string currentSubstring = address.Substring(leftmostIndex, findLength);

                    if (currentSubstring == pair.FindString)
                    {
                        // Replace the multi-digit sequence
                        string beforeReplacement = address.Substring(0, leftmostIndex);
                        string afterReplacement = address.Substring(rightmostIndex + 1);
                        string replacement = pair.ReplaceString ?? "";

                        return beforeReplacement + replacement + afterReplacement;
                    }
                }
            }
        }

        return address; // Return unchanged if processing fails or criteria not met
    }
}
