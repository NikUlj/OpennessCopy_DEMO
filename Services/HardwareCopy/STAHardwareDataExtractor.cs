using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.Cax;
using Siemens.Engineering.HW;
using Siemens.Engineering.MC.Drives;
using NetworkInterface = Siemens.Engineering.HW.Features.NetworkInterface;

namespace OpennessCopy.Services.HardwareCopy;

/// <summary>
/// Hardware data extraction utilities for TIA Portal objects with caching support
/// Uses DataCacheUtility for caching functionality
/// </summary>
public static class STAHardwareDataExtractor
{
    /// <summary>
    /// Extracts hardware discovery data from all TIA Portal instances
    /// Reuses existing PlcManagementService infrastructure
    /// </summary>
    public static HardwareDiscoveryData ExtractHardwareDiscoveryDataFromAllInstances(
        List<(TiaPortal portal, Project project)> allProjects,
        Action<string> statusCallback = null)
    {
        try
        {
            var discoveryData = new HardwareDiscoveryData();
            statusCallback?.Invoke("Enumerating TIA Portal instances...");

            foreach (var (portal, project) in allProjects)
            {
                try
                {
                    statusCallback?.Invoke($"Processing project: {project.Name}");

                    var instanceInfo = new TiaPortalInstanceInfo
                    {
                        ProjectId = DataCacheUtility.CacheObject(project),
                        ProjectName = project.Name,
                        ProcessId = GetProcessIdFromPortal(portal),
                        InstanceId = DataCacheUtility.CacheObject(portal),
                        IsArchive = false // Live TIA Portal instance (not archive)
                        // HardwareDevices list remains empty during discovery
                    };

                    discoveryData.TiaInstances.Add(instanceInfo);

                    Logger.LogInfo($"Found TIA Portal instance with project '{project.Name}'");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error processing project '{project.Name}': {ex.Message}");
                }
            }

            statusCallback?.Invoke($"Hardware discovery complete: {discoveryData.TiaInstances.Count} instances found");
            return discoveryData;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during hardware discovery: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Extracts hardware devices from a selected TIA Portal instance
    /// Called after user selects a specific instance to work with
    /// </summary>
    public static List<HardwareDeviceInfo> ExtractHardwareDevicesFromSelectedInstance(
        string selectedInstanceId,
        Action<string> statusCallback = null,
        Action<int, int> progressCallback = null)
    {
        try
        {
            var startTime = DateTime.Now;
            statusCallback?.Invoke("Extracting hardware devices from selected instance...");

            // Find the selected project
            var selectedProject = DataCacheUtility.GetCachedObject<Project>(selectedInstanceId);
            if (selectedProject == null)
            {
                throw new InvalidOperationException($"Selected TIA Portal instance not found: {selectedInstanceId}");
            }

            // statusCallback?.Invoke($"Extracting devices from project: {selectedProject.Name}");
            Logger.LogInfo($"Starting device extraction from project '{selectedProject.Name}' at {DateTime.Now:HH:mm:ss.fff}");

            var devices = ExtractHardwareDevicesFromProject(selectedProject, progressCallback);

            var duration = DateTime.Now - startTime;
            Logger.LogInfo($"Extracted {devices.Count} hardware devices from selected project '{selectedProject.Name}' in {duration.TotalSeconds:F2} seconds");
            return devices;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting devices from selected instance: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Extracts lightweight hardware device information (names and types only) for UI display
    /// Skips expensive network traversal, IP extraction, and IoSystem detection
    /// Used when source != target to show device selection UI quickly
    /// </summary>
    public static List<HardwareDeviceInfo> ExtractHardwareDevicesLight(
        string selectedInstanceId,
        Action<string> statusCallback = null,
        Action<int, int> progressCallback = null)
    {
        try
        {
            var startTime = DateTime.Now;
            statusCallback?.Invoke("Extracting device list (lightweight)...");

            // Find the selected project
            var selectedProject = DataCacheUtility.GetCachedObject<Project>(selectedInstanceId);
            if (selectedProject == null)
            {
                throw new InvalidOperationException($"Selected TIA Portal instance not found: {selectedInstanceId}");
            }

            Logger.LogInfo($"Starting lightweight device extraction from project '{selectedProject.Name}' at {DateTime.Now:HH:mm:ss.fff}");

            var devices = ExtractHardwareDevicesFromProjectLight(selectedProject, progressCallback);

            var duration = DateTime.Now - startTime;
            Logger.LogInfo($"Extracted {devices.Count} hardware devices (lightweight) from project '{selectedProject.Name}' in {duration.TotalSeconds:F2} seconds");
            return devices;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting lightweight devices from selected instance: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Extracts hardware devices from a TIA Portal project
    /// Uses simple iteration through all devices in the project
    /// </summary>
    private static List<HardwareDeviceInfo> ExtractHardwareDevicesFromProject(Project project, Action<int, int> progressCallback = null)
    {
        var hardwareDevices = new List<HardwareDeviceInfo>();

        try
        {
            var startTime = DateTime.Now;

            // First, count total devices for accurate progress reporting
            var totalDeviceCount = project.Devices.Count + project.UngroupedDevicesGroup.Devices.Count;
            foreach (var group in project.DeviceGroups)
            {
                totalDeviceCount += CountDevicesInGroup(group);
            }

            Logger.LogInfo($"Processing {totalDeviceCount} total devices from project");

            var projectId = DataCacheUtility.CacheObject(project);
            var currentDeviceIndex = 0;

            // Create IoSystem cache mapping to avoid caching the same IoSystem multiple times
            var ioSystemCache = new Dictionary<int, string>(); // IoSystem name -> cache ID

            // Process root devices
            foreach (Device device in project.Devices)
            {
                var deviceInfo = CreateHardwareDeviceInfo(device, projectId, ioSystemCache);
                if (deviceInfo is { DeviceType: not null })
                {
                    hardwareDevices.Add(deviceInfo);
                }
                currentDeviceIndex++;
                progressCallback?.Invoke(currentDeviceIndex, totalDeviceCount);
            }

            // Process device groups
            foreach (var group in project.DeviceGroups)
            {
                GetDevicesFromGroup(group, hardwareDevices, projectId, ref currentDeviceIndex, totalDeviceCount, progressCallback, ioSystemCache);
            }

            // Process ungrouped devices
            foreach (var device in project.UngroupedDevicesGroup.Devices)
            {
                var deviceInfo = CreateHardwareDeviceInfo(device, projectId, ioSystemCache);
                if (deviceInfo is { DeviceType: not null })
                {
                    hardwareDevices.Add(deviceInfo);
                }
                currentDeviceIndex++;
                progressCallback?.Invoke(currentDeviceIndex, totalDeviceCount);
            }

            var duration = DateTime.Now - startTime;
            Logger.LogInfo($"Project device extraction completed in {duration.TotalSeconds:F2} seconds");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting hardware devices from project '{project.Name}': {ex.Message}", false);
        }

        return hardwareDevices;
    }

    /// <summary>
    /// Extracts lightweight hardware device information from a TIA Portal project
    /// Only extracts basic properties needed for UI display (names, types)
    /// Skips expensive network traversal and IoSystem detection
    /// </summary>
    private static List<HardwareDeviceInfo> ExtractHardwareDevicesFromProjectLight(Project project, Action<int, int> progressCallback = null)
    {
        var hardwareDevices = new List<HardwareDeviceInfo>();

        try
        {
            var startTime = DateTime.Now;

            // First, count total devices for accurate progress reporting
            var totalDeviceCount = project.Devices.Count + project.UngroupedDevicesGroup.Devices.Count;
            foreach (var group in project.DeviceGroups)
            {
                totalDeviceCount += CountDevicesInGroup(group);
            }

            Logger.LogInfo($"Processing {totalDeviceCount} total devices from project (lightweight mode)");

            var projectId = DataCacheUtility.CacheObject(project);
            var currentDeviceIndex = 0;

            // Process root devices
            foreach (Device device in project.Devices)
            {
                var deviceInfo = CreateHardwareDeviceInfoLight(device, projectId);
                if (deviceInfo is { DeviceType: not null })
                {
                    hardwareDevices.Add(deviceInfo);
                }
                currentDeviceIndex++;
                progressCallback?.Invoke(currentDeviceIndex, totalDeviceCount);
            }

            // Process device groups
            foreach (var group in project.DeviceGroups)
            {
                GetDevicesFromGroupLight(group, hardwareDevices, projectId, ref currentDeviceIndex, totalDeviceCount, progressCallback);
            }

            // Process ungrouped devices
            foreach (var device in project.UngroupedDevicesGroup.Devices)
            {
                var deviceInfo = CreateHardwareDeviceInfoLight(device, projectId);
                if (deviceInfo is { DeviceType: not null })
                {
                    hardwareDevices.Add(deviceInfo);
                }
                currentDeviceIndex++;
                progressCallback?.Invoke(currentDeviceIndex, totalDeviceCount);
            }

            var duration = DateTime.Now - startTime;
            Logger.LogInfo($"Project lightweight device extraction completed in {duration.TotalSeconds:F2} seconds");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting lightweight hardware devices from project '{project.Name}': {ex.Message}", false);
        }

        return hardwareDevices;
    }

    /// <summary>
    /// Counts total devices in a device group recursively
    /// </summary>
    private static int CountDevicesInGroup(DeviceUserGroup group)
    {
        var count = group.Devices.Count;
        foreach (var subGroup in group.Groups)
        {
            count += CountDevicesInGroup(subGroup);
        }
        return count;
    }

    private static void GetDevicesFromGroup(DeviceUserGroup group, List<HardwareDeviceInfo> devices, string projectId, ref int currentDeviceIndex, int totalDeviceCount, Action<int, int> progressCallback, Dictionary<int, string> ioSystemCache)
    {
        foreach (var device in group.Devices)
        {
            var deviceInfo = CreateHardwareDeviceInfo(device, projectId, ioSystemCache);
            if (deviceInfo is { DeviceType: not null })
            {
                devices.Add(deviceInfo);
            }
            currentDeviceIndex++;
            progressCallback?.Invoke(currentDeviceIndex, totalDeviceCount);
        }

        foreach (var newGroup in group.Groups)
        {
            GetDevicesFromGroup(newGroup, devices, projectId, ref currentDeviceIndex, totalDeviceCount, progressCallback, ioSystemCache);
        }
    }

    private static void GetDevicesFromGroupLight(DeviceUserGroup group, List<HardwareDeviceInfo> devices, string projectId, ref int currentDeviceIndex, int totalDeviceCount, Action<int, int> progressCallback)
    {
        foreach (var device in group.Devices)
        {
            var deviceInfo = CreateHardwareDeviceInfoLight(device, projectId);
            if (deviceInfo is { DeviceType: not null })
            {
                devices.Add(deviceInfo);
            }
            currentDeviceIndex++;
            progressCallback?.Invoke(currentDeviceIndex, totalDeviceCount);
        }

        foreach (var newGroup in group.Groups)
        {
            GetDevicesFromGroupLight(newGroup, devices, projectId, ref currentDeviceIndex, totalDeviceCount, progressCallback);
        }
    }

    /// <summary>
    /// Creates HardwareDeviceInfo from TIA Portal Device object
    /// </summary>
    private static HardwareDeviceInfo CreateHardwareDeviceInfo(Device device, string projectId, Dictionary<int, string> ioSystemCache)
    {
        try
        {
            // Cache the actual TIA Portal objects and get cache IDs
            var deviceId = DataCacheUtility.CacheObject(device);

            // Get basic device properties with bounds checking for device items
            var itemName = (device.DeviceItems.Count > 1) ? device.DeviceItems[1].Name : null;

            // Extract network information, device number, address modules, and controlling PLC
            var (ipAddresses, deviceNumber, isETDevice, addressModules, ioSystem, controllingPlcName) = ExtractDeviceNetworkInfo(device);

            // Cache the IoSystem (with deduplication)
            string ioSystemId = null;
            int? ioSystemHash = null;
            if (ioSystem != null)
            {
                ioSystemHash = ioSystem.GetHashCode();
                if (ioSystemCache.TryGetValue(ioSystemHash.Value, out var value))
                {
                    // Reuse existing cache ID
                    ioSystemId = value;
                }
                else
                {
                    // Cache new IoSystem and store the mapping
                    ioSystemId = DataCacheUtility.CacheObject(ioSystem);
                    ioSystemCache[ioSystemHash.Value] = ioSystemId;
                }
            }

            var deviceInfo = new HardwareDeviceInfo
            {
                Name = device.Name,
                ItemName = itemName,
                DeviceType = GetDeviceAttribute(device, "TypeName") ?? device.TypeIdentifier,
                DeviceId = deviceId, // This is now a cache ID, not a generated string
                ProjectId = projectId,
                IpAddresses = ipAddresses,
                DeviceNumber = deviceNumber,
                IoSystemId = ioSystemId,
                IoSystemHash = ioSystemHash,
                NetworkPortCount = ipAddresses.Count, // Use IP count as port count for full extraction
                IsETDevice = isETDevice,
                AddressModules = addressModules,
                ControllingPlcName = controllingPlcName
            };

            return deviceInfo;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error creating device info for device '{device?.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates lightweight HardwareDeviceInfo from TIA Portal Device object
    /// Only extracts basic properties needed for UI display (names, types)
    /// Skips expensive network traversal and IoSystem detection
    /// </summary>
    private static HardwareDeviceInfo CreateHardwareDeviceInfoLight(Device device, string projectId)
    {
        try
        {
            // Cache the actual TIA Portal objects and get cache IDs
            var deviceId = DataCacheUtility.CacheObject(device);

            // Get basic device properties with bounds checking for device items
            var itemName = (device.DeviceItems.Count > 1) ? device.DeviceItems[1].Name : null;

            // Quick port count - minimal overhead, no attribute reads
            int portCount = CountNetworkPortsLight(device);

            var deviceInfo = new HardwareDeviceInfo
            {
                Name = device.Name,
                ItemName = itemName,
                DeviceType = GetDeviceAttribute(device, "TypeName") ?? device.TypeIdentifier,
                DeviceId = deviceId,
                ProjectId = projectId,
                NetworkPortCount = portCount,
                // All network-related fields left empty for lightweight extraction
                IpAddresses = new List<string>(),
                DeviceNumber = null,
                IoSystemId = null,
                IoSystemHash = null,
                IsETDevice = false,
                AddressModules = new List<DeviceAddressInfo>(),
                ControllingPlcName = null
            };

            return deviceInfo;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error creating lightweight device info for device '{device?.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Quickly counts network ports without reading any attributes or traversing IoSystems
    /// Very fast - only checks for existence of NetworkInterface service and nodes
    /// Used during lightweight extraction to detect multi-port devices for filtering
    /// </summary>
    private static int CountNetworkPortsLight(Device device)
    {
        int count = 0;

        if (device.DeviceItems.Count == 0)
            return 0;

        try
        {
            foreach (var layer1DeviceItem in device.DeviceItems)
            {
                if (layer1DeviceItem == null) continue;

                var networkInterface = layer1DeviceItem.GetService<NetworkInterface>();
                if (networkInterface?.Nodes.Count > 0)
                    count++;

                foreach (var layer2DeviceItem in layer1DeviceItem.DeviceItems)
                {
                    if (layer2DeviceItem == null) continue;
                    var ni = layer2DeviceItem.GetService<NetworkInterface>();
                    if (ni?.Nodes.Count > 0)
                        count++;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error counting network ports for device '{device.Name}': {ex.Message}");
            // Return 0 on error - device will be allowed through filter
            return 0;
        }

        return count;
    }

    /// <summary>
    /// Safely gets device attribute value using IEngineeringObject cast
    /// </summary>
    private static string GetDeviceAttribute(Device device, string attributeName)
    {
        try
        {
            if (device is IEngineeringObject engineeringObject)
            {
                var attributeValue = engineeringObject.GetAttribute(attributeName);
                return attributeValue?.ToString();
            }
        }
        catch (Exception)
        {
            // Attribute may not exist or be accessible, return null
        }
        return null;
    }

    /// <summary>
    /// Builds hardware workflow configuration from user selections
    /// </summary>
    public static HardwareWorkflowConfiguration BuildHardwareWorkflowConfiguration(
        HardwareUserSelections userSelections)
    {
        try
        {
            if (userSelections.SelectedDevices == null || userSelections.SelectedDevices.Count == 0)
            {
                throw new InvalidOperationException("No hardware devices selected for export");
            }

            var config = new HardwareWorkflowConfiguration
            {
                SourceProjectId = userSelections.SourceProjectId,
                SourceInstanceId = userSelections.SourceInstanceId,
                TargetProjectId = userSelections.TargetProjectId,
                TargetInstanceId = userSelections.TargetInstanceId,
                SelectedDevices = userSelections.SelectedDevices,
                DeviceNameFindReplacePairs = userSelections.DeviceNameFindReplacePairs ?? new List<FindReplacePair>(),
                IpAddressOffset = userSelections.IpAddressOffset,
                ETAddressReplacements = userSelections.ETAddressReplacements ?? new List<TagAddressReplacePair>(),
                SelectedIoSystem = userSelections.SelectedIoSystem
            };

            Logger.LogInfo($"Built hardware workflow configuration for {config.SelectedDevices.Count} devices with {config.DeviceNameFindReplacePairs.Count} find/replace pairs and IoSystem: {(string.IsNullOrWhiteSpace(config.SelectedIoSystem.IoSystemId) ? "None" : "Selected")}");
            return config;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error building hardware workflow configuration: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Enriches lightweight device info objects with full details (IPs, IoSystems, ET modules, etc.)
    /// Takes a list of lightweight HardwareDeviceInfo objects and populates network-related fields
    /// Used when source != target to extract full details only for user-selected devices
    /// </summary>
    public static List<HardwareDeviceInfo> EnrichSelectedDevicesWithDetails(
        List<HardwareDeviceInfo> lightweightDevices,
        Action<int, int> progressCallback = null)
    {
        try
        {
            var startTime = DateTime.Now;
            Logger.LogInfo($"Starting device enrichment for {lightweightDevices.Count} selected devices at {DateTime.Now:HH:mm:ss.fff}");

            var enrichedDevices = new List<HardwareDeviceInfo>();
            var ioSystemCache = new Dictionary<int, string>(); // IoSystem hash -> cache ID

            for (int i = 0; i < lightweightDevices.Count; i++)
            {
                var lightDevice = lightweightDevices[i];
                progressCallback?.Invoke(i + 1, lightweightDevices.Count);

                try
                {
                    // Retrieve the cached Device object
                    var device = DataCacheUtility.GetCachedObject<Device>(lightDevice.DeviceId);
                    if (device == null)
                    {
                        Logger.LogWarning($"Could not retrieve cached device for enrichment: {lightDevice.Name}");
                        enrichedDevices.Add(lightDevice); // Keep lightweight version
                        continue;
                    }

                    // Extract network information (the expensive part we skipped earlier)
                    var (ipAddresses, deviceNumber, isETDevice, addressModules, ioSystem, controllingPlcName) = ExtractDeviceNetworkInfo(device);

                    // Cache the IoSystem (with deduplication)
                    string ioSystemId = null;
                    int? ioSystemHash = null;
                    if (ioSystem != null)
                    {
                        ioSystemHash = ioSystem.GetHashCode();
                        if (ioSystemCache.TryGetValue(ioSystemHash.Value, out var value))
                        {
                            ioSystemId = value;
                        }
                        else
                        {
                            ioSystemId = DataCacheUtility.CacheObject(ioSystem);
                            ioSystemCache[ioSystemHash.Value] = ioSystemId;
                        }
                    }

                    // Create enriched device info with full details
                    var enrichedDevice = new HardwareDeviceInfo
                    {
                        Name = lightDevice.Name,
                        ItemName = lightDevice.ItemName,
                        DeviceType = lightDevice.DeviceType,
                        DeviceId = lightDevice.DeviceId,
                        ProjectId = lightDevice.ProjectId,
                        // Enriched fields
                        IpAddresses = ipAddresses,
                        DeviceNumber = deviceNumber,
                        IoSystemId = ioSystemId,
                        IoSystemHash = ioSystemHash,
                        IsETDevice = isETDevice,
                        AddressModules = addressModules,
                        ControllingPlcName = controllingPlcName
                    };

                    enrichedDevices.Add(enrichedDevice);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error enriching device '{lightDevice.Name}': {ex.Message}");
                    enrichedDevices.Add(lightDevice); // Keep lightweight version on error
                }
            }

            var duration = DateTime.Now - startTime;
            Logger.LogInfo($"Device enrichment completed for {enrichedDevices.Count} devices in {duration.TotalSeconds:F2} seconds");
            return enrichedDevices;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during device enrichment: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// Gets the process ID of a TIA Portal instance
    /// </summary>
    private static int GetProcessIdFromPortal(TiaPortal portal)
    {
        try
        {
            // Use hash code as process ID placeholder
            return Math.Abs(portal.GetHashCode()) % 10000;
        }
        catch
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Extracts network information, address data, and controlling PLC from a device
    /// Uses recursive traversal to handle any device hierarchy depth
    /// </summary>
    private static (
        List<string> IpAddresses,
        int? DeviceNumber, 
        bool IsETDevice, 
        List<DeviceAddressInfo> AddressModules, 
        IoSystem ioSystem, 
        string controllingPlcName) 
        ExtractDeviceNetworkInfo(Device device)
    {
        var ipAddresses = new List<string>();
        int? deviceNumber = null;
        IoSystem ioSystem = null;
        string controllingPlcName = null;
        var addressModules = new List<DeviceAddressInfo>();

        if (device.DeviceItems.Count == 0)
            return (ipAddresses, null, false, addressModules, null, null);

        // Check if this is an ET 200SP device
        var deviceType = device.TypeIdentifier;
        var isETDevice = Equals(deviceType, "System:Device.ET200SP");

        // Start recursive traversal from device items
        foreach (var deviceItem in device.DeviceItems)
        {
            if (deviceItem == null) continue;
            ProcessDeviceItemRecursively(deviceItem, ipAddresses, ref deviceNumber, ref ioSystem, ref controllingPlcName, addressModules);
        }

        return (ipAddresses, deviceNumber, isETDevice, addressModules, ioSystem, controllingPlcName);
    }

    /// <summary>
    /// Recursively processes a device item and all its children to extract network info and addresses
    /// </summary>
    private static void ProcessDeviceItemRecursively(
        DeviceItem deviceItem,
        List<string> ipAddresses,
        ref int? deviceNumber,
        ref IoSystem ioSystem,
        ref string controllingPlcName,
        List<DeviceAddressInfo> addressModules)
    {
        if (deviceItem == null) return;

        // Process network interface (extracts IPs, device number, ioSystem, and PLC name)
        var networkInterface = deviceItem.GetService<NetworkInterface>();
        ProcessNetworkInterface(networkInterface, ipAddresses, ref deviceNumber, ref ioSystem, ref controllingPlcName);

        // Extract addresses from this device item
        var addressInfo = deviceItem.Classification != DeviceItemClassifications.HM 
            ? ExtractAddressesFromDeviceItem(deviceItem) 
            : ExtractAddressesFromHeaderModule(deviceItem);
        if (addressInfo != null)
        {
            addressModules.Add(addressInfo);
        }

        // Recurse into children
        foreach (var childDeviceItem in deviceItem.DeviceItems)
        {
            ProcessDeviceItemRecursively(childDeviceItem, ipAddresses, ref deviceNumber, ref ioSystem, ref controllingPlcName, addressModules);
        }
    }

    /// <summary>
    /// Extracts address information from a device item
    /// Works for all device types, not just ET modules
    /// </summary>
    private static DeviceAddressInfo ExtractAddressesFromDeviceItem(DeviceItem deviceItem)
    {
        try
        {
            var addresses = deviceItem.Addresses;
            if (addresses.Count == 0)
                return null;

            var addressInfos = new List<AddressInfo>();
            foreach (var address in addresses)
            {
                var addressStart = address.StartAddress;
                if (addressStart != -1) // Filter out invalid addresses
                {
                    addressInfos.Add(new AddressInfo
                    {
                        StartAddress = addressStart,
                        Length = address.Length,
                        Type = address.IoType
                    });
                }
            }

            // Only create info if we have valid addresses
            if (addressInfos.Count > 0)
            {
                return new DeviceAddressInfo
                {
                    ModuleName = deviceItem.Name,
                    AddressInfos = addressInfos
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting addresses from device item '{deviceItem?.Name}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Extracts address information from a header module item
    /// </summary>
    private static DeviceAddressInfo ExtractAddressesFromHeaderModule(DeviceItem headerModule)
    {
        try
        {
            var driveContainer = headerModule.GetService<DriveObjectContainer>();
            if (driveContainer == null) 
                return null;

            foreach (var driveObject in driveContainer.DriveObjects)
            {
                var telegrams = driveObject.Telegrams;
                
                if (telegrams == null)
                    continue;

                foreach (var telegram in telegrams)
                {
                    var addresses = telegram.Addresses;
                    if (addresses.Count == 0)
                        return null;

                    var addressInfos = new List<AddressInfo>();
                    foreach (var address in addresses)
                    {
                        var addressStart = address.StartAddress;
                        if (addressStart != -1) // Filter out invalid addresses
                        {
                            addressInfos.Add(new AddressInfo
                            {
                                StartAddress = addressStart,
                                Length = address.Length,
                                Type = address.IoType
                            });
                        }
                    }

                    // Only create info if we have valid addresses
                    if (addressInfos.Count > 0)
                    {
                        return new DeviceAddressInfo
                        {
                            ModuleName = headerModule.Name,
                            AddressInfos = addressInfos
                        };
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting addresses from device item '{headerModule?.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts all IoSystems from a project with network information
    /// </summary>
    public static List<IoSystemInfo> ExtractIoSystemsFromProject(Project project)
    {
        var ioSystems = new List<IoSystemInfo>();

        try
        {
            Logger.LogInfo($"Extracting IoSystems from project: {project.Name}");

            foreach (var subnet in project.Subnets)
            {
                try
                {
                    // Extract network address and subnet mask from nodes
                    string networkAddress = null;
                    string subnetMask = null;

                    // Collect all network addresses to detect inconsistencies
                    var networkAddresses = new Dictionary<string, int>(); // networkAddress -> count

                    foreach (var node in subnet.Nodes)
                    {
                        try
                        {
                            var ipAddress = node.GetAttribute("Address") as string;
                            var mask = node.GetAttribute("SubnetMask") as string;

                            if (!string.IsNullOrWhiteSpace(ipAddress) && !string.IsNullOrWhiteSpace(mask))
                            {
                                var calculatedNetwork = CalculateNetworkAddress(ipAddress, mask);

                                if (!networkAddresses.ContainsKey(calculatedNetwork))
                                    networkAddresses[calculatedNetwork] = 0;

                                networkAddresses[calculatedNetwork]++;

                                // Store first valid mask found
                                subnetMask ??= mask;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Error reading node attributes: {ex.Message}");
                        }
                    }

                    // Check for inconsistencies and use most common network address
                    if (networkAddresses.Count > 1)
                    {
                        Logger.LogWarning($"Subnet '{subnet.Name}' has inconsistent network addresses across nodes: {string.Join(", ", networkAddresses.Keys)}");
                    }

                    if (networkAddresses.Count > 0)
                    {
                        // Use most common network address (or first if tied)
                        networkAddress = networkAddresses.OrderByDescending(kvp => kvp.Value).First().Key;

                        if (networkAddresses.Count > 1)
                        {
                            Logger.LogWarning($"Using most common network address for subnet '{subnet.Name}': {networkAddress}");
                        }
                    }

                    // Extract IoSystems from this subnet
                    foreach (var ioSystem in subnet.IoSystems)
                    {
                        // Get the controlling PLC name from IoSystem hierarchy
                        string controllingPlcName = null;
                        if (ioSystem.Parent?.Parent?.Parent?.Parent is DeviceItem plcDevice)
                        {
                            controllingPlcName = plcDevice.Name;
                        }

                        var ioSystemInfo = new IoSystemInfo
                        {
                            Name = ioSystem.Name,
                            SubnetName = subnet.Name,
                            IoSystemId = DataCacheUtility.CacheObject(ioSystem),
                            SubnetId = DataCacheUtility.CacheObject(subnet),
                            NetworkAddress = networkAddress ?? "Unknown",
                            SubnetMask = subnetMask ?? "Unknown",
                            IoSystemHash = ioSystem.GetHashCode(),
                            ControllingPlcName = controllingPlcName ?? "Unknown"
                        };
                        
                        ioSystems.Add(ioSystemInfo);
                        Logger.LogInfo($"Extracted IoSystem: {ioSystem.Name} on subnet {subnet.Name} (PLC: {controllingPlcName ?? "N/A"}, Network: {networkAddress ?? "N/A"}, Hash: {ioSystemInfo.IoSystemHash})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error processing subnet '{subnet.Name}': {ex.Message}");
                }
            }

            Logger.LogInfo($"Extracted {ioSystems.Count} IoSystem(s) from project");
            return ioSystems;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting IoSystems from project: {ex.Message}", false);
            return ioSystems;
        }
    }

    /// <summary>
    /// Calculates network address from IP address and subnet mask
    /// </summary>
    private static string CalculateNetworkAddress(string ipAddress, string subnetMask)
    {
        try
        {
            var ipParts = ipAddress.Split('.');
            var maskParts = subnetMask.Split('.');

            if (ipParts.Length != 4 || maskParts.Length != 4)
                return ipAddress; // Return original if invalid format

            var networkParts = new int[4];
            for (int i = 0; i < 4; i++)
            {
                networkParts[i] = int.Parse(ipParts[i]) & int.Parse(maskParts[i]);
            }

            return $"{networkParts[0]}.{networkParts[1]}.{networkParts[2]}.{networkParts[3]}";
        }
        catch
        {
            return ipAddress; // Return original on error
        }
    }

    /// <summary>
    /// Calculates new IP address by combining network address with modified host portion from original IP
    /// </summary>
    /// <param name="originalIp">Original IP address (e.g., "192.168.1.100")</param>
    /// <param name="networkAddress">Network address from subnet (e.g., "192.168.1.0")</param>
    /// <param name="subnetMask">Subnet mask (e.g., "255.255.255.0")</param>
    /// <param name="offset">Offset to apply to host portion</param>
    /// <returns>New IP address</returns>
    private static string CalculateNewIp(string originalIp, string networkAddress, string subnetMask, int offset)
    {
        try
        {
            var originalParts = originalIp.Split('.');
            var networkParts = networkAddress.Split('.');
            var maskParts = subnetMask.Split('.');

            if (originalParts.Length != 4 || networkParts.Length != 4 || maskParts.Length != 4)
            {
                Logger.LogWarning($"Invalid IP format, returning original: {originalIp}");
                return originalIp;
            }

            var newParts = new int[4];

            // For each octet: network part OR (host part + offset) masked by inverse of subnet mask
            for (int i = 0; i < 4; i++)
            {
                int mask = int.Parse(maskParts[i]);
                int network = int.Parse(networkParts[i]);
                int originalHost = int.Parse(originalParts[i]);

                if (mask == 255)
                {
                    // Network portion - use network address
                    newParts[i] = network;
                }
                else
                {
                    // Host portion - apply offset to original host value
                    int hostMask = 255 - mask;
                    int hostValue = originalHost & hostMask;
                    newParts[i] = network + ((hostValue + offset) & hostMask);
                }
            }

            var newIp = $"{newParts[0]}.{newParts[1]}.{newParts[2]}.{newParts[3]}";
            Logger.LogInfo($"Calculated new IP: {originalIp} -> {newIp} (network: {networkAddress}, offset: {offset:+0;-0;0})");
            return newIp;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error calculating new IP, returning original: {ex.Message}");
            return originalIp;
        }
    }

    /// <summary>
    /// Calculates the new device number from an IP address and offset
    /// Uses the last byte of the IP address plus the offset
    /// </summary>
    /// <param name="ipAddress">IP address (e.g., "192.168.1.100")</param>
    /// <returns>New device number</returns>
    private static int GetDeviceNumberFromIp(string ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddress));
            }

            var parts = ipAddress.Split('.');
            if (parts.Length != 4)
            {
                throw new ArgumentException($"Invalid IP address format: '{ipAddress}'", nameof(ipAddress));
            }

            if (!int.TryParse(parts[3], out int lastByte))
            {
                throw new ArgumentException($"Invalid last byte in IP address '{ipAddress}'", nameof(ipAddress));
            }

            return lastByte;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error calculating device number from IP '{ipAddress}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Configures a copied device's PROFINET network settings: set IP address and assign the PN device number.
    /// </summary>
    /// <remarks>
    /// Order matters: the node must be connected to the target subnet before the IoConnector can be connected to the target IoSystem.
    /// </remarks>
    /// <param name="device">Device to configure.</param>
    /// <param name="ipOffset">Host offset applied when calculating the new IP address.</param>
    /// <param name="targetIoSystem">IoSystem to connect the device to.</param>
    /// <param name="targetSubnet">Subnet to connect the device node to.</param>
    /// <param name="networkAddress">Network address for the target subnet (for example, "192.168.1.0").</param>
    /// <param name="subnetMask">Subnet mask for the target subnet (for example, "255.255.255.0").</param>
    public static void ConfigureDeviceNetworkAndNumber(Device device, int ipOffset,
        IoSystem targetIoSystem, Subnet targetSubnet, string networkAddress, string subnetMask)
    {
        try
        {
            Logger.LogInfo($"Configuring network and device number for device: {device.Name}");

            if (device.DeviceItems.Count == 0)
            {
                Logger.LogWarning($"Device '{device.Name}' has no device items");
                return;
            }

            var (ioConnector, node, originalIp) = FindIoConnectorAndNode(device, targetIoSystem, targetSubnet);
            if (node == null)
            {
                Logger.LogWarning($"No network interface or node found in device '{device.Name}'");
                return;
            }

            if (string.IsNullOrWhiteSpace(originalIp))
            {
                Logger.LogWarning($"No IP address found on node for device '{device.Name}'");
                return;
            }

            Logger.LogInfo($"Original IP address: {originalIp}");

            string newIpAddress = CalculateNewIp(originalIp, networkAddress, subnetMask, ipOffset);
            Logger.LogInfo($"Setting new IP address: {newIpAddress}");
            node.SetAttribute("Address", newIpAddress);

            // Note: Multi-port devices (devices with multiple network interfaces) are filtered out during device selection
            // This function only configures single-port devices (NetworkPortCount <= 1)
            // Multi-port devices require manual network configuration

            if (ioConnector == null)
            {
                Logger.LogWarning($"No IoConnector found in device '{device.Name}', skipping device number configuration");
                return;
            }

            int deviceNumber = GetDeviceNumberFromIp(newIpAddress); // offset already applied
            Logger.LogInfo($"Setting device number: {deviceNumber}");
            ioConnector.SetAttribute("PnDeviceNumber", deviceNumber);

            Logger.LogSuccess($"Successfully configured network for '{device.Name}': IP={newIpAddress}, DeviceNumber={deviceNumber}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error configuring device network for '{device.Name}': {ex.Message}", false);
        }
    }

    /// <summary>
    /// Finds the first IoConnector in a device using the same traversal pattern as ExtractDeviceNetworkInfo
    /// </summary>
    /// <param name="device">Device to search for IoConnector</param>
    /// <param name="ioSystem">IoSystem the device should be connected to.</param>
    /// <param name="subnet">Subnet the device node should be connected to.</param>
    /// <returns>First IoConnector found, or null if none exists</returns>
    private static (IoConnector, Node, string) FindIoConnectorAndNode(Device device, IoSystem ioSystem, Subnet subnet)
    {
        if (device.DeviceItems.Count == 0)
            return (null, null, null);

        foreach (var layer1DeviceItem in device.DeviceItems)
        {
            if (layer1DeviceItem == null) continue;

            // Check layer 1 for interface (safety check)
            var networkInterface = layer1DeviceItem.GetService<NetworkInterface>();
            var ioConnectorAndNode = FindIoConnectorInAndNodeNetworkInterface(networkInterface, ioSystem, subnet);
            if (ioConnectorAndNode.Item1 != null || ioConnectorAndNode.Item2 != null) return ioConnectorAndNode;

            // Check layer 2 (main pattern)
            foreach (var layer2DeviceItem in layer1DeviceItem.DeviceItems)
            {
                if (layer2DeviceItem == null) continue;
                networkInterface = layer2DeviceItem.GetService<NetworkInterface>();
                if (networkInterface == null) continue;
                ioConnectorAndNode = FindIoConnectorInAndNodeNetworkInterface(networkInterface, ioSystem, subnet);
                if (ioConnectorAndNode.Item1 != null || ioConnectorAndNode.Item2 != null) return ioConnectorAndNode;
            }
        }

        return (null, null, null);
    }

    /// <summary>
    /// Finds the first IoConnector in a network interface using the selected IO system
    /// </summary>
    /// <param name="networkInterface">NetworkInterface to search</param>
    /// <param name="ioSystem">IoSystem to connect the IoConnector to.</param>
    /// <param name="subnet">Subnet to connect the node to.</param>
    /// <returns>First IoConnector found that connects successfully, or null if none exists</returns>
    private static (IoConnector, Node, string) FindIoConnectorInAndNodeNetworkInterface(NetworkInterface networkInterface, IoSystem ioSystem, Subnet subnet)
    {
        if (networkInterface == null) return (null, null, null);
        
        var node =  networkInterface.Nodes.FirstOrDefault();
        if  (node == null) return (null, null, null);

        string originalIp;
        try
        {
            originalIp = node.GetAttribute("Address") as string;
            node.ConnectToSubnet(subnet);
        }
        catch (Exception e)
        {
            Logger.LogInfo($"Exception while connecting to subnet:\n{e.Message}");
            return (null, null, null);
        }
        
        foreach (var ioConnector in networkInterface.IoConnectors)
        {
            if (ioConnector == null) continue;
    
            try
            {
                ioConnector.ConnectToIoSystem(ioSystem);
            }
            catch (Exception e)
            {
                Logger.LogInfo($"Exception while connecting to IO system:\n{e.Message}");
                continue;
            }

            try
            {
                ioConnector.GetAttribute("PnDeviceNumber");
                return (ioConnector, node, originalIp);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception while getting device number:\n{e.Message}");
            }
        }

        return (null, null, null);
    }

    /// <summary>
    /// Imports hardware devices from AML files in device-specific subfolders
    /// Uses device mapping to find device info and transformed names for each imported device
    /// </summary>
    /// <param name="exportDirectory">Directory containing device subfolders with AML files</param>
    /// <param name="deviceMappings">Dictionary mapping folder names to device info and transformed names</param>
    /// <param name="ipOffset">IP offset for device number calculation</param>
    /// <param name="project">TIA Portal project to import devices into</param>
    /// <param name="statusCallback">Optional callback for status updates</param>
    /// <returns>Number of devices successfully imported</returns>
    public static void ImportHardwareDevicesFromAml(
        string exportDirectory,
        Dictionary<string, (HardwareDeviceInfo deviceInfo, string transformedName)> deviceMappings,
        int ipOffset,
        Project project,
        Action<string> statusCallback = null)
    {
        try
        {
            Logger.LogInfo($"Starting AML import from directory: {exportDirectory}");
            statusCallback?.Invoke("Starting hardware device import...");

            if (!Directory.Exists(exportDirectory))
            {
                throw new DirectoryNotFoundException($"Export directory not found: {exportDirectory}");
            }

            // Get CaxProvider service for AML import
            var caxProvider = project.GetService<CaxProvider>();
            if (caxProvider == null)
            {
                throw new InvalidOperationException("CaxProvider service not available for AML import");
            }

            var deviceSubfolders = Directory.GetDirectories(exportDirectory);
            var successfulImports = 0;

            Logger.LogInfo($"Found {deviceSubfolders.Length} device subfolders to process");

            foreach (var deviceFolder in deviceSubfolders)
            {
                try
                {
                    var deviceFolderName = Path.GetFileName(deviceFolder);
                    statusCallback?.Invoke($"Importing device: {deviceFolderName}");

                    // Find device mapping for this folder
                    if (!deviceMappings.TryGetValue(deviceFolderName, out var deviceMapping))
                    {
                        Logger.LogWarning($"No device mapping found for folder: {deviceFolderName}");
                        continue;
                    }

                    var (deviceInfo, transformedName) = deviceMapping;

                    // Find AML and log files in this device folder
                    var amlFiles = Directory.GetFiles(deviceFolder, "*.aml");
                    var logFiles = Directory.GetFiles(deviceFolder, "*_Log.log");

                    if (amlFiles.Length == 0)
                    {
                        Logger.LogWarning($"No AML file found in device folder: {deviceFolder}");
                        continue;
                    }

                    if (amlFiles.Length > 1)
                    {
                        Logger.LogWarning($"Multiple AML files found in device folder: {deviceFolder}, using first one");
                    }

                    var amlFile = new FileInfo(amlFiles[0]);
                    var logFile = logFiles.Length > 0 ? new FileInfo(logFiles[0]) : null;

                    Logger.LogInfo($"Importing AML file: {amlFile.Name} (original: {deviceInfo.Name} -> transformed: {transformedName})");

                    // Import the AML file using CaxProvider
                    bool importSuccess = caxProvider.Import(amlFile, logFile, CaxImportOptions.MoveToParkingLot);

                    if (!importSuccess)
                    {
                        Logger.LogError($"AML import failed for device folder: {deviceFolderName}", false);
                        continue;
                    }

                    Logger.LogSuccess($"AML import completed for device: {deviceFolderName}");

                    // Update device number if IP offset is specified
                    if (ipOffset != 0 && deviceInfo.IpAddresses.Count > 0)
                    {
                        // Find the newly imported device using the transformed name
                        var importedDevice = project.Devices.Find(transformedName) ?? project.UngroupedDevicesGroup.Devices.Find(transformedName);
                        if (importedDevice == null)
                            foreach (var deviceGroup in project.DeviceGroups)
                            {
                                importedDevice = RecursiveFindDevice(deviceGroup, transformedName);
                                if (importedDevice != null) break;
                                if (importedDevice != null) break;
                            }

                        if (importedDevice != null)
                        {
                            // Get the cached IoSystem
                            IoSystem targetIoSystem = null;
                            if (deviceInfo.IoSystemId != null)
                                targetIoSystem = DataCacheUtility.GetCachedObject<IoSystem>(deviceInfo.IoSystemId);
                            
                            if (targetIoSystem == null)
                            {
                                bool isPLC = importedDevice.DeviceItems.Any(deviceItem =>
                                    deviceItem.Classification == DeviceItemClassifications.CPU);

                                if (!isPLC)
                                {
                                    Logger.LogInfo($"Could not find cached IoSystem for device '{transformedName}. Changing operating mode to none.'");
                                    if (ChangeOperatingMode(importedDevice))
                                    {
                                        Logger.LogSuccess($"Operating mode successfully changed for device '{transformedName}'.");
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Could not change operating mode for device '{transformedName}'.");
                                    }
                                }
                                
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                            Logger.LogWarning($"Could not find imported device '{transformedName}' in project");
                        }
                    }

                    successfulImports++;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error importing device from folder '{deviceFolder}': {ex.Message}", false);
                }
            }

            Logger.LogSuccess($"Hardware import completed: {successfulImports}/{deviceSubfolders.Length} devices imported successfully");
            statusCallback?.Invoke($"Import completed: {successfulImports} devices imported");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during hardware device import: {ex.Message}", false);
            throw;
        }
    }

    private static bool ChangeOperatingMode(Device device)
    {
        try
        {
            if (device.DeviceItems.Count == 0)
                return false;

            foreach (var layer1DeviceItem in device.DeviceItems)
            {
                if (layer1DeviceItem == null) continue;

                // Check layer 1 for interface (safety check)
                var networkInterface = layer1DeviceItem.GetService<NetworkInterface>();
                if (networkInterface != null)
                {
                    networkInterface.InterfaceOperatingMode = InterfaceOperatingModes.None;
                    return true;
                }

                // Check layer 2 (main pattern)
                foreach (var layer2DeviceItem in layer1DeviceItem.DeviceItems)
                {
                    if (layer2DeviceItem == null) continue;
                    networkInterface = layer2DeviceItem.GetService<NetworkInterface>();
                    if (networkInterface == null) continue;
                    networkInterface.InterfaceOperatingMode = InterfaceOperatingModes.None;
                    return true;
                }
            }

            return false;
        }
        catch (Exception e)
        {
            Logger.LogWarning(e.Message);
            return false;
        }
    }

    private static Device RecursiveFindDevice(DeviceUserGroup deviceGroup, string deviceName)
    {
        var device = deviceGroup.Devices.Find(deviceName);
        
        if (device == null)
            foreach (var group in deviceGroup.Groups)
            {
                device = RecursiveFindDevice(group, deviceName);
            }
        
        return device;
    }

    /// <summary>
    /// Processes a network interface to extract IP addresses, device number, IoSystem, and controlling PLC name
    /// </summary>
    private static void ProcessNetworkInterface(NetworkInterface networkInterface, List<string> ipAddresses, ref int? deviceNumber, ref IoSystem ioSystem, ref string controllingPlcName)
    {
        if (networkInterface == null) return;

        // Extract IP addresses from nodes
        foreach (var node in networkInterface.Nodes)
        {
            try
            {
                var ipAddress = node.GetAttribute("Address") as string;
                if (!string.IsNullOrWhiteSpace(ipAddress) && !ipAddresses.Contains(ipAddress) && IsValidIpAddress(ipAddress))
                {
                    ipAddresses.Add(ipAddress);
                }
            }
            catch
            {
                // Ignore nodes without address attribute
            }
        }

        // Extract device number, ioSystem, and controlling PLC name from IoConnectors
        if (!deviceNumber.HasValue || ioSystem == null || controllingPlcName == null)
        {
            foreach (var ioConnector in networkInterface.IoConnectors)
            {
                try
                {
                    // Get device number
                    if (!deviceNumber.HasValue)
                    {
                        var deviceNumberObj = ioConnector.GetAttribute("PnDeviceNumber");
                        if (deviceNumberObj != null && int.TryParse(deviceNumberObj.ToString(), out int parsedDeviceNumber))
                        {
                            deviceNumber = parsedDeviceNumber;
                        }
                    }

                    // Get IoSystem object
                    ioSystem ??= ioConnector.ConnectedToIoSystem;

                    // Get controlling PLC name via parent traversal
                    controllingPlcName ??= ioConnector.ConnectedToIoSystem?.Parent?.Parent?.Parent?.Parent?.GetAttribute("Name") as string;

                    // If we have everything, break early
                    if (deviceNumber.HasValue && ioSystem != null && controllingPlcName != null)
                        break;
                }
                catch
                {
                    // Ignore connectors without attributes
                }
            }
        }
    }

    /// <summary>
    /// Validates if a string is a valid IPv4 address format
    /// </summary>
    private static bool IsValidIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        var parts = ipAddress.Split('.');
        if (parts.Length != 4)
            return false;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out int value) || value < 0 || value > 255)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Loads a TIA Portal archive and extracts hardware discovery data from it
    /// Archive is opened in Secondary mode to avoid conflicts with primary projects
    /// </summary>
    public static (bool success, Project retrievedProject, HardwareDiscoveryData discoveryData) LoadAndExtractArchive(
        TiaPortal tiaPortal,
        string archivePath,
        DirectoryInfo retrieveDirectory)
    {
        try
        {
            Logger.LogInfo($"Loading file from: {archivePath}");

            // Determine if this is an archive (.zap*) or a project file (.ap*)
            var fileExtension = Path.GetExtension(archivePath).ToLowerInvariant();
            var isArchiveFile = fileExtension.StartsWith(".zap");
            Project loadedProject;

            if (isArchiveFile)
            {
                // Archive file (.zap*) - use Retrieve
                Logger.LogInfo("Detected archive file (.zap*) - using Projects.Retrieve()");

                // Create unique subdirectory for this archive
                // Use short name to avoid TIA Portal's 143 character path limit
                // ReSharper disable once StringLiteralTypo
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var uniqueDirName = $"Archive_{timestamp}";
                var retrieveDir = new DirectoryInfo(Path.Combine(retrieveDirectory.FullName, uniqueDirName));
                retrieveDir.Create();

                Logger.LogInfo($"Archive will be retrieved to: {retrieveDir.FullName}");

                // Retrieve the archive in SECONDARY mode (critical to avoid primary project conflicts)
                loadedProject = tiaPortal.Projects.Retrieve(
                    new FileInfo(archivePath),
                    retrieveDir);
            }
            else
            {
                // Project file (.ap*) - use Open
                Logger.LogInfo("Detected project file (.ap*) - using Projects.Open()");
                loadedProject = tiaPortal.Projects.Open(new FileInfo(archivePath));
            }

            if (loadedProject == null)
            {
                Logger.LogError("Failed to load file - returned null");
                return (false, null, null);
            }

            Logger.LogInfo($"File loaded successfully: {loadedProject.Name}");

            // Extract discovery data from loaded project
            var discoveryData = ExtractHardwareDiscoveryDataFromArchive(tiaPortal, loadedProject, archivePath);

            return (true, loadedProject, discoveryData);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading file: {ex.Message}");
            return (false, null, null);
        }
    }

    /// <summary>
    /// Extracts hardware discovery data from a single archive project
    /// Creates a TiaPortalInstanceInfo representing the archive
    /// </summary>
    private static HardwareDiscoveryData ExtractHardwareDiscoveryDataFromArchive(TiaPortal tiaPortal, Project project, string archivePath)
    {
        try
        {
            var discoveryData = new HardwareDiscoveryData();

            var instanceInfo = new TiaPortalInstanceInfo
            {
                ProjectId = DataCacheUtility.CacheObject(project),
                InstanceId = DataCacheUtility.CacheObject(tiaPortal), // Cache TIA Portal instance for later retrieval
                ProjectName = Path.GetFileName(archivePath),
                ProcessId = 0, // No process ID for archives
                IsArchive = true // Mark as archive for UI filtering
                // HardwareDevices list remains empty during discovery
            };

            discoveryData.TiaInstances.Add(instanceInfo);

            Logger.LogInfo($"Extracted discovery data from archive: {Path.GetFileName(archivePath)}");
            return discoveryData;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting discovery data from archive: {ex.Message}", false);
            throw;
        }
    }
}

/// <summary>
/// Configuration for hardware workflow execution
/// </summary>
public class HardwareWorkflowConfiguration
{
    // Source and target project/instance IDs for cross-project device copying
    public string SourceProjectId { get; set; }
    public string SourceInstanceId { get; set; } // TIA Portal instance ID for source (needed for global library creation)
    public string TargetProjectId { get; set; }
    public string TargetInstanceId { get; set; } // TIA Portal instance ID for target (needed for device creation)

    public List<HardwareDeviceInfo> SelectedDevices { get; set; } = new List<HardwareDeviceInfo>();
    public List<FindReplacePair> DeviceNameFindReplacePairs { get; set; } = new List<FindReplacePair>();
    public int IpAddressOffset { get; set; } // Amount to add/subtract from last byte of IP address
    public List<TagAddressReplacePair> ETAddressReplacements { get; set; } = new List<TagAddressReplacePair>(); // ET module address transformations
    public IoSystemInfo SelectedIoSystem { get; set; } // Cache ID for selected IoSystem
}
