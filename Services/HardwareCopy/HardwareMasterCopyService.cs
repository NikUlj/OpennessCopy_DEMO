using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.MasterCopies;

namespace OpennessCopy.Services.HardwareCopy;

/// <summary>
/// Service for hardware device copying using global library master copies
/// This approach preserves all device settings unlike AML export/import
/// </summary>
public static class HardwareMasterCopyService
{
    /// <summary>
    /// Creates a global library in the specified directory for storing master copies
    /// </summary>
    /// <param name="tiaPortal">TIA Portal instance to use for library creation</param>
    /// <param name="exportDirectory">Base export directory</param>
    /// <returns>Created global library</returns>
    public static (UserGlobalLibrary library, FileInfo libraryFile) CreateGlobalLibrary(TiaPortal tiaPortal, string exportDirectory)
    {
        try
        {
            // TIA Portal will create a folder with the library name, so just use the export directory
            var exportDir = new DirectoryInfo(exportDirectory);

            if (!exportDir.Exists)
            {
                exportDir.Create();
                Logger.LogInfo($"Created export directory: {exportDirectory}");
            }

            // Remove existing library folder if it exists (TIA creates "HardwareDeviceLibrary" folder)
            var libraryFolderPath = Path.Combine(exportDirectory, "HardwareDeviceLibrary");
            if (Directory.Exists(libraryFolderPath))
            {
                Logger.LogInfo($"Removing existing library folder: {libraryFolderPath}");
                Directory.Delete(libraryFolderPath, true);
            }

            // Create global library - TIA will create "HardwareDeviceLibrary" folder inside exportDirectory
            Logger.LogInfo("Creating global library for hardware devices...");
            var globalLibrary = tiaPortal.GlobalLibraries.Create<UserGlobalLibrary>(
                exportDir,
                "HardwareDeviceLibrary");

            // Find the created library file inside the TIA-created folder
            var libraryFolder = new DirectoryInfo(libraryFolderPath);
            if (!libraryFolder.Exists)
            {
                throw new InvalidOperationException($"Library folder not found: {libraryFolderPath}");
            }

            var libraryFiles = libraryFolder.GetFiles("HardwareDeviceLibrary.al*");
            if (libraryFiles.Length == 0)
            {
                throw new InvalidOperationException($"Library file not found in {libraryFolder.FullName}");
            }

            var libraryFile = libraryFiles[0]; // Should only be one file
            Logger.LogSuccess($"Global library created successfully at: {libraryFile.FullName}");
            return (globalLibrary, libraryFile);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error creating global library: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Creates a master copy of a hardware device in the global library
    /// </summary>
    /// <param name="globalLibrary">Global library to store the master copy</param>
    /// <param name="deviceInfo">Device information</param>
    /// <returns>Created master copy</returns>
    public static MasterCopy CreateMasterCopy(UserGlobalLibrary globalLibrary, HardwareDeviceInfo deviceInfo)
    {
        try
        {
            Logger.LogInfo($"Creating master copy for device: {deviceInfo.Name}");

            // Retrieve actual Device object from cache
            var sourceDevice = DataCacheUtility.GetCachedObject<Device>(deviceInfo.DeviceId);
            if (sourceDevice == null)
            {
                throw new InvalidOperationException($"Device object not found in cache for device: {deviceInfo.Name}");
            }

            // Create master copy in library
            var masterCopy = globalLibrary.MasterCopyFolder.MasterCopies.Create(sourceDevice);

            Logger.LogSuccess($"Master copy created successfully: {masterCopy.Name}");
            return masterCopy;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error creating master copy for device '{deviceInfo.Name}': {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Creates a device in the target project from a master copy
    /// </summary>
    /// <param name="targetProject">Target project to create the device in</param>
    /// <param name="masterCopy">Master copy to create device from</param>
    /// <returns>Created device</returns>
    public static Device CreateDeviceFromMasterCopy(Project targetProject, MasterCopy masterCopy)
    {
        try
        {
            Logger.LogInfo($"Creating device from master copy: {masterCopy.Name}");

            // Create device from master copy
            var newDevice = targetProject.Devices.CreateFrom(masterCopy);

            Logger.LogSuccess($"Device created successfully: {newDevice.Name}");
            return newDevice;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error creating device from master copy '{masterCopy.Name}': {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Applies ET address transformations to a device by recursively traversing device items
    /// Uses original addresses from AddressModules to avoid conflicts from TIA Portal's auto-adjustment
    /// </summary>
    /// <param name="device">Device to apply transformations to</param>
    /// <param name="deviceInfo">Original device info containing AddressModules with original addresses</param>
    /// <param name="addressTransformations">List of address transformation pairs</param>
    public static void ApplyETAddressTransformations(Device device, HardwareDeviceInfo deviceInfo, List<TagAddressReplacePair> addressTransformations)
    {
        try
        {
            if (addressTransformations == null || addressTransformations.Count == 0)
            {
                Logger.LogInfo($"No address transformations to apply for device: {device.Name}");
                return;
            }

            if (deviceInfo.AddressModules == null || deviceInfo.AddressModules.Count == 0)
            {
                Logger.LogInfo($"No address modules found in device info for: {device.Name}");
                return;
            }

            Logger.LogInfo($"Applying {addressTransformations.Count} address transformations to device: {device.Name} using {deviceInfo.AddressModules.Count} address modules");
            int transformedCount = 0;
            int moduleIndex = 0; // Track position in AddressModules list

            // Recursively process all device items (same pattern as ExtractDeviceNetworkInfo)
            foreach (var deviceItem in device.DeviceItems)
            {
                if (deviceItem != null)
                {
                    transformedCount += ProcessDeviceItemForAddressTransformation(deviceItem, deviceInfo.AddressModules, ref moduleIndex, addressTransformations);
                }
            }

            Logger.LogSuccess($"Address transformation completed for '{device.Name}': {transformedCount} addresses transformed using {moduleIndex} address modules");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error applying address transformations to device '{device.Name}': {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Recursively processes a device item and applies address transformations using original addresses
    /// Maintains exact parity with extraction logic - only increments moduleIndex for items with valid addresses
    /// </summary>
    private static int ProcessDeviceItemForAddressTransformation(
        DeviceItem deviceItem,
        List<DeviceAddressInfo> addressModules,
        ref int moduleIndex,
        List<TagAddressReplacePair> addressTransformations)
    {
        int transformedCount = 0;

        try
        {
            // Get addresses collection (same as extraction logic)
            var addresses = deviceItem.Addresses;

            // Filter to valid addresses only (same as extraction: StartAddress != -1)
            var validAddresses = new List<Address>();
            foreach (var address in addresses)
            {
                if (address.StartAddress != -1)
                {
                    validAddresses.Add(address);
                }
            }

            // Only process if there are valid addresses (maintains extraction parity)
            if (validAddresses.Count > 0)
            {
                // Check if we have a corresponding AddressModule entry
                if (moduleIndex < addressModules.Count)
                {
                    var originalModule = addressModules[moduleIndex];

                    // Sanity check: number of addresses should match
                    if (validAddresses.Count == originalModule.AddressInfos.Count)
                    {
                        // Transform each address using its original value
                        for (int i = 0; i < validAddresses.Count; i++)
                        {
                            var copiedAddress = validAddresses[i];
                            var originalAddressInfo = originalModule.AddressInfos[i];
                            var originalValue = originalAddressInfo.StartAddress;
                            var autoAdjustedValue = copiedAddress.StartAddress; // Value after TIA Portal auto-adjustment

                            // Check if the address is writable (some addresses like Q on dual I/Q modules are read-only)
                            if (!IsStartAddressWritable(copiedAddress))
                            {
                                Logger.LogInfo($"  Skipping read-only address in '{deviceItem.Name}': {originalValue} ({copiedAddress.IoType})");
                                continue;
                            }

                            // Apply transformations to the ORIGINAL address
                            var transformedValue = ApplyAddressTransformations(originalValue, addressTransformations);

                            // Write transformed value to the copied device's address
                            copiedAddress.StartAddress = transformedValue;
                            transformedCount++;

                            Logger.LogInfo(autoAdjustedValue != originalValue
                                ? $"  Transformed address in '{deviceItem.Name}': {originalValue} -> {transformedValue} ({copiedAddress.IoType}) [TIA auto-adjusted to {autoAdjustedValue}, overwritten]"
                                : $"  Transformed address in '{deviceItem.Name}': {originalValue} -> {transformedValue} ({copiedAddress.IoType})");
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"Address count mismatch for '{deviceItem.Name}': copied={validAddresses.Count}, original={originalModule.AddressInfos.Count}");
                        foreach (var address in originalModule.AddressInfos)
                        {
                            Logger.LogWarning($"  Original address in '{deviceItem.Name}': {address.StartAddress}");
                        }
                    }

                    // Increment module index only when we processed addresses (same as extraction)
                    moduleIndex++;
                }
                else
                {
                    Logger.LogWarning($"Module index {moduleIndex} out of range for device item '{deviceItem.Name}' (total modules: {addressModules.Count})");
                }
            }

            // Recurse into children (same pattern as extraction)
            foreach (var childDeviceItem in deviceItem.DeviceItems)
            {
                if (childDeviceItem != null)
                {
                    transformedCount += ProcessDeviceItemForAddressTransformation(childDeviceItem, addressModules, ref moduleIndex, addressTransformations);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error processing device item '{deviceItem?.Name}': {ex.Message}");
        }

        return transformedCount;
    }

    /// <summary>
    /// Applies all transformation pairs to an address
    /// Reuses the same logic as ProcessAddressTransformation from UI forms
    /// </summary>
    private static int ApplyAddressTransformations(int address, List<TagAddressReplacePair> transformations)
    {
        foreach (var pair in transformations)
        {
            address = ProcessAddressTransformation(address, pair);
        }
        return address;
    }

    /// <summary>
    /// Processes a single address transformation pair
    /// Same logic as in HardwareProcessingConfigForm and ETModuleConfigurationForm
    /// </summary>
    private static int ProcessAddressTransformation(int address, TagAddressReplacePair pair)
    {
        // Convert address to string for processing (similar to tag address processing)
        string addressString = address.ToString();

        // Check length filter - only process addresses with specified digit count
        if (addressString.Length == pair.LengthFilter)
        {
            // Apply replacement to specific digit position (right-to-left counting)
            if (pair.DigitPosition > 0 && pair.DigitPosition <= addressString.Length && !string.IsNullOrEmpty(pair.FindString))
            {
                int findLength = pair.FindString.Length;
                int rightmostIndex = addressString.Length - pair.DigitPosition; // Where rightmost digit of find string should be
                int leftmostIndex = rightmostIndex - findLength + 1; // Where leftmost digit of find string should be

                // Check if we have enough digits to the left for the find string
                if (leftmostIndex >= 0 && rightmostIndex < addressString.Length)
                {
                    // Extract the substring to compare
                    string currentSubstring = addressString.Substring(leftmostIndex, findLength);

                    if (currentSubstring == pair.FindString)
                    {
                        // Replace the multi-digit sequence
                        string beforeReplacement = addressString.Substring(0, leftmostIndex);
                        string afterReplacement = addressString.Substring(rightmostIndex + 1);
                        string replacement = pair.ReplaceString ?? "";

                        string newAddressString = beforeReplacement + replacement + afterReplacement;

                        // Convert back to integer
                        if (int.TryParse(newAddressString, out int newAddress))
                        {
                            return newAddress;
                        }
                    }
                }
            }
        }

        return address; // Return unchanged if no transformation applied
    }

    /// <summary>
    /// Checks if the StartAddress property is writable for a given address object
    /// Some addresses (particularly Q addresses on modules with both I and Q) are read-only
    /// </summary>
    /// <param name="address">The address object to check</param>
    /// <returns>True if StartAddress can be written, false otherwise</returns>
    private static bool IsStartAddressWritable(Address address)
    {
        try
        {
            // Cast to IEngineeringObject to access GetAttributeInfos
            var engineeringObject = address as IEngineeringObject;

            if (engineeringObject == null)
            {
                Logger.LogWarning("Unable to cast Address to IEngineeringObject");
                return false;
            }

            // Get all attribute information
            var attributeInfos = engineeringObject.GetAttributeInfos();

            // Find the StartAddress attribute
            var startAddressInfo = attributeInfos.FirstOrDefault(attr => attr.Name == "StartAddress");

            if (startAddressInfo == null)
            {
                Logger.LogWarning("StartAddress attribute not found");
                return false;
            }

            // Check if the attribute has write access
            bool isWritable = (startAddressInfo.AccessMode & EngineeringAttributeAccessMode.Write)
                              == EngineeringAttributeAccessMode.Write;

            return isWritable;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error checking if StartAddress is writable: {ex.Message}");
            return false;
        }
    }
}
