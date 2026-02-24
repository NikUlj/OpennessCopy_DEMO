using System;
using System.Collections.Generic;
using System.Security;
using OpennessCopy.Forms.BlockCopy;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using static OpennessCopy.Services.SecurityManagementService;
using static OpennessCopy.Utils.DataCacheUtility;
using static OpennessCopy.Utils.MiscUtil;

namespace OpennessCopy.Services.BlockCopy
{
    // Helper class for extracting data from Openness objects on STA thread
    // All methods in this class must be called from the STA thread
    public static class STADataExtractor
    {
        public static DiscoveryData ExtractDiscoveryDataFromAllInstances(List<(TiaPortal portal, Project project)> allProjects, Action<string> progressCallback = null)
        {
            var discoveryData = new DiscoveryData();
            var totalPlcCount = 0;
            
            for (int i = 0; i < allProjects.Count; i++)
            {
                var (_, project) = allProjects[i];
                var instanceId = $"instance_{i}";

                progressCallback?.Invoke($"Scanning TIA Portal instance {i + 1}/{allProjects.Count}: {project.Name}");
                
                // Extract PLC information from this instance
                foreach (Device device in project.Devices)
                {
                    foreach (DeviceItem deviceItem in device.DeviceItems)
                    {
                        var softwareContainer = deviceItem.GetService<SoftwareContainer>();
                        if (softwareContainer?.Software is PlcSoftware plcSoftware)
                        {
                            totalPlcCount++;
                            progressCallback?.Invoke($"Loading PLC {totalPlcCount}: {plcSoftware.Name} (from {project.Name})");
                            
                            var plcId = CacheObject(plcSoftware);
                            var deviceId = CacheObject(device);

                            var plcInfo = new PLCInfo
                            {
                                Name = plcSoftware.Name,
                                DeviceName = device.Name,
                                PlcId = plcId,
                                DeviceId = deviceId,
                                TiaInstanceId = instanceId,
                                ProjectName = project.Name,
                                ActiveCultures = ExtractActiveCultures(project),
                                SafetyPasswordData = null, // Will be loaded later for selected PLC
                                BlockGroupData = null,     // Will be loaded later for selected PLC
                                TagTableData = null        // Will be loaded later for selected PLC
                            };
                            discoveryData.PLCs.Add(plcInfo);
                        }
                    }
                }
            }

            return discoveryData;
        }

        private static DiscoveryData ExtractDiscoveryData(Project project, Action<string> progressCallback = null, string instanceId = "instance_0", bool isArchive = false, string archiveFileName = null)
        {
            var discoveryData = new DiscoveryData();

            // Extract PLC information
            progressCallback?.Invoke("Scanning for PLCs...");
            var plcCount = 0;

            foreach (Device device in project.Devices)
            {
                foreach (DeviceItem deviceItem in device.DeviceItems)
                {
                    var softwareContainer = deviceItem.GetService<SoftwareContainer>();
                    if (softwareContainer?.Software is PlcSoftware plcSoftware)
                    {
                        plcCount++;
                        progressCallback?.Invoke($"Loading PLC {plcCount}: {plcSoftware.Name}");

                        var plcId = CacheObject(plcSoftware);
                        var deviceId = CacheObject(device);

                        var plcInfo = new PLCInfo
                        {
                            Name = plcSoftware.Name,
                            DeviceName = device.Name,
                            PlcId = plcId,
                            DeviceId = deviceId,
                            TiaInstanceId = instanceId,
                            ProjectName = isArchive ? (archiveFileName ?? project.Name) : project.Name,
                            IsArchive = isArchive,
                            ActiveCultures = ExtractActiveCultures(project),
                            SafetyPasswordData = null, // Will be loaded later for selected PLC
                            BlockGroupData = null,     // Will be loaded later for selected PLC
                            TagTableData = null        // Will be loaded later for selected PLC
                        };
                        discoveryData.PLCs.Add(plcInfo);
                    }
                }
            }

            return discoveryData;
        }

        // Load detailed data for a specific selected PLC
        public static void LoadDetailedPlcData(PLCInfo plcInfo, Action<string> progressCallback = null)
        {
            progressCallback?.Invoke($"Loading detailed data for {plcInfo.Name}...");
            
            // Load safety password data
            progressCallback?.Invoke("Checking safety configuration...");
            plcInfo.SafetyPasswordData = ExtractSafetyPasswordData(plcInfo.DeviceId);
            
            // Load block group data
            progressCallback?.Invoke("Loading block groups...");
            plcInfo.BlockGroupData = ExtractBlockGroupData(plcInfo.PlcId);
            
            // Load tag table data
            progressCallback?.Invoke("Loading tag tables...");
            plcInfo.TagTableData = ExtractTagTableData(plcInfo.PlcId);
            
            progressCallback?.Invoke("Detailed data loading completed.");
        }

        private static BlockGroupSelectionData ExtractBlockGroupData(string plcId)
        {
            var plc = GetCachedObject<PlcSoftware>(plcId);
            if (plc == null) return null;

            var data = new BlockGroupSelectionData
            {
                PlcName = plc.Name
            };

            // Extract block groups
            foreach (PlcBlockUserGroup group in plc.BlockGroup.Groups)
            {
                data.RootGroups.Add(ExtractBlockGroupInfo(group, data));
            }

            return data;
        }

        private static TagTableSelectionData ExtractTagTableData(string plcId)
        {
            var plc = GetCachedObject<PlcSoftware>(plcId);
            if (plc == null) return null;

            var data = new TagTableSelectionData
            {
                ExistingTableNames = new HashSet<string>()
            };

            // Extract tag tables from root level
            foreach (PlcTagTable tagTable in plc.TagTableGroup.TagTables)
            {
                data.ExistingTableNames.Add(tagTable.Name);

                var tagTableInfo = new TagTableInfo
                {
                    Name = tagTable.Name,
                    TagCount = tagTable.Tags.Count,
                    TableId = CacheObject(tagTable)
                };
                
                data.TagTables.Add(tagTableInfo);
            }

            // Recursively extract tag tables from groups
            ExtractTagTablesFromGroups(plc.TagTableGroup.Groups, data);

            return data;
        }

        private static void ExtractTagTablesFromGroups(dynamic groups, TagTableSelectionData data)
        {
            foreach (dynamic group in groups)
            {
                // Extract tag tables from current group
                foreach (PlcTagTable tagTable in group.TagTables)
                {
                    data.ExistingTableNames.Add(tagTable.Name);

                    var tagTableInfo = new TagTableInfo
                    {
                        Name = tagTable.Name,
                        TagCount = tagTable.Tags.Count,
                        TableId = CacheObject(tagTable)
                    };
                    
                    data.TagTables.Add(tagTableInfo);
                }

                // Recursively process subgroups
                ExtractTagTablesFromGroups(group.Groups, data);
            }
        }

        private static BlockGroupInfo ExtractBlockGroupInfo(PlcBlockUserGroup group, BlockGroupSelectionData data)
        {
            // Start with counts for this group only
            int totalBlocks = group.Blocks.Count;
            int totalSubGroups = group.Groups.Count;

            var groupInfo = new BlockGroupInfo
            {
                Name = group.Name,
                Path = GetGroupPath(group),
                GroupId = CacheObject(group)
            };

            // Extract subgroups and accumulate their recursive totals
            foreach (PlcBlockUserGroup subGroup in group.Groups)
            {
                var subGroupInfo = ExtractBlockGroupInfo(subGroup, data);
                groupInfo.SubGroups.Add(subGroupInfo);
                
                // Bubble up the recursive counts from subgroups
                totalBlocks += subGroupInfo.BlockCount;
                totalSubGroups += subGroupInfo.SubGroupCount;
            }
            
            // Set the final recursive totals
            groupInfo.BlockCount = totalBlocks;
            groupInfo.SubGroupCount = totalSubGroups;

            // Extract blocks
            foreach (PlcBlock block in group.Blocks)
            {
                data.ExistingBlockNames.Add(block.Name);
                data.ExistingBlockNumbers.Add(block.Number);
            }

            return groupInfo;
        }

        private static string GetGroupPath(PlcBlockUserGroup group)
        {
            var path = new List<string>();
            var current = group;
            
            while (current != null)
            {
                path.Insert(0, current.Name);
                current = current.Parent as PlcBlockUserGroup;
            }
            
            return string.Join(" > ", path);
        }
        
        // Method to retrieve actual Openness objects for workflow execution
        public static WorkflowConfiguration BuildWorkflowConfiguration(UserSelections selections)
        {
            var sourcePlc = GetCachedObject<PlcSoftware>(selections.SourcePlcId);
            var sourceDevice = GetCachedObject<Device>(selections.SourceDeviceId);
            var targetPlc = GetCachedObject<PlcSoftware>(selections.TargetPlcId);
            var selectedGroup = GetCachedObject<PlcBlockUserGroup>(selections.SelectedGroupId);

            return new WorkflowConfiguration
            {
                SourcePlc = sourcePlc,
                SourceDevice = sourceDevice,
                TargetPlc = targetPlc,
                SelectedGroup = selectedGroup,
                PrefixNumber = selections.PrefixNumber,
                FindReplacePairs = selections.FindReplacePairs,
                ContentFindReplacePairs = selections.ContentFindReplacePairs,
                SelectedTables = selections.SelectedTables,
                ExistingTagTableNames = selections.ExistingTagTableNames,
                ExistingBlockNames = selections.ExistingBlockNames,
                SourceSafetyPassword = ConvertToSecureString(selections.SourceSafetyPassword),
            };
        }

        // Get block numbers for a specific selected group and all its subgroups
        private static HashSet<int> GetBlockNumbersForGroup(string groupId)
        {
            var group = GetCachedObject<PlcBlockUserGroup>(groupId);
            if (group == null) return new HashSet<int>();
            
            var blockNumbers = new HashSet<int>();
            ExtractBlockNumbersFromGroup(group, blockNumbers);
            return blockNumbers;
        }

        // Check for prefix conflicts - must be called from STA thread
        public static List<int> CheckPrefixConflicts(int prefix, string selectedGroupId, HashSet<int> existingNumbers)
        {
            var selectedGroupNumbers = GetBlockNumbersForGroup(selectedGroupId);
            var conflicts = new List<int>();
            
            foreach (int originalNumber in selectedGroupNumbers)
            {
                // Apply the same logic as ModifyBlockNumber but for integers
                int rightmostTwoDigits = originalNumber % 100;
                int newNumber = (prefix * 100) + rightmostTwoDigits;
                
                if (existingNumbers.Contains(newNumber))
                {
                    conflicts.Add(newNumber);
                }
            }
            
            return conflicts;
        }

        // Get the first block from a selected group and all its subgroups - must be called from STA thread
        public static (string name, int number) GetFirstBlockFromGroup(string selectedGroupId)
        {
            var group = GetCachedObject<PlcBlockUserGroup>(selectedGroupId);
            if (group == null) return ("FB_Example_Block", 1234);
            
            return GetFirstBlockFromGroupRecursive(group);
        }

        private static (string name, int number) GetFirstBlockFromGroupRecursive(PlcBlockUserGroup group)
        {
            // Check blocks in current group first
            foreach (PlcBlock block in group.Blocks)
            {
                return (block.Name, block.Number);
            }
            
            // If no blocks in current group, check subgroups
            foreach (PlcBlockUserGroup subGroup in group.Groups)
            {
                var result = GetFirstBlockFromGroupRecursive(subGroup);
                if (!result.name.Equals("FB_Example_Block")) // Found a real block
                {
                    return result;
                }
            }
            
            // No blocks found anywhere, return default
            return ("FB_Example_Block", 1234);
        }
        
        private static void ExtractBlockNumbersFromGroup(PlcBlockUserGroup group, HashSet<int> blockNumbers)
        {
            // Extract block numbers from current group
            foreach (PlcBlock block in group.Blocks)
            {
                blockNumbers.Add(block.Number);
            }
            
            // Recursively extract from subgroups
            foreach (PlcBlockUserGroup subGroup in group.Groups)
            {
                ExtractBlockNumbersFromGroup(subGroup, blockNumbers);
            }
        }

        /// <summary>
        /// Extracts sample tag data from a specific tag table for configuration examples
        /// </summary>
        public static List<TagExample> ExtractSampleTagData(string tableId)
        {
            var tagTable = GetCachedObject<PlcTagTable>(tableId);
            if (tagTable == null) return new List<TagExample>();

            var sampleTags = new List<(string Name, string Address)>();
            
            // Get up to 50 tags to analyze (enough to find different digit sizes)
            int count = 0;
            foreach (PlcTag tag in tagTable.Tags)
            {
                if (count >= 50) break;
                
                if (!string.IsNullOrEmpty(tag.LogicalAddress))
                {
                    sampleTags.Add((tag.Name, tag.LogicalAddress));
                }
                count++;
            }

            // Use the analyzer to process the tags and find examples of different sizes
            return TagAddressAnalyzer.AnalyzeTags(sampleTags);
        }

        /// <summary>
        /// Extracts active culture names from a TIA Portal project
        /// </summary>
        private static HashSet<string> ExtractActiveCultures(Project project)
        {
            var activeCultures = new HashSet<string>();
            
            try
            {
                var languageSettings = project.LanguageSettings;
                var activeLanguages = languageSettings.ActiveLanguages;
                
                foreach (var language in activeLanguages)
                {
                    activeCultures.Add(language.Culture.Name);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not extract active cultures from project {project.Name}: {ex.Message}");
            }
            
            return activeCultures;
        }

        /// <summary>
        /// Loads a TIA Portal archive and extracts PLC discovery data from it
        /// Archive is opened in Secondary mode to avoid conflicts with primary projects
        /// </summary>
        public static (bool success, Project retrievedProject, DiscoveryData discoveryData) LoadAndExtractArchive(
            TiaPortal tiaPortal,
            string archivePath,
            System.IO.DirectoryInfo retrieveDirectory)
        {
            try
            {
                Logger.LogInfo($"Loading file from: {archivePath}");

                // Determine if this is an archive (.zap*) or a project file (.ap*)
                var fileExtension = System.IO.Path.GetExtension(archivePath).ToLowerInvariant();
                var isArchiveFile = fileExtension.StartsWith(".zap");
                Project loadedProject;

                if (isArchiveFile)
                {
                    // Archive file (.zap*) - use Retrieve
                    Logger.LogInfo("Detected archive file (.zap*) - using Projects.Retrieve()");

                    // Create unique subdirectory for this archive
                    // Use short name to avoid TIA Portal's 143 character path limit
                    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    var uniqueDirName = $"Archive_{timestamp}";
                    var retrieveDir = new System.IO.DirectoryInfo(System.IO.Path.Combine(retrieveDirectory.FullName, uniqueDirName));
                    retrieveDir.Create();

                    Logger.LogInfo($"Archive will be retrieved to: {retrieveDir.FullName}");

                    // Retrieve the archive in SECONDARY mode (critical to avoid primary project conflicts)
                    loadedProject = tiaPortal.Projects.Retrieve(
                        new System.IO.FileInfo(archivePath),
                        retrieveDir);
                }
                else
                {
                    // Project file (.ap*) - use Open
                    Logger.LogInfo("Detected project file (.ap*) - using Projects.Open()");
                    loadedProject = tiaPortal.Projects.Open(new System.IO.FileInfo(archivePath));
                }

                if (loadedProject == null)
                {
                    Logger.LogError("Failed to load file - returned null");
                    return (false, null, null);
                }

                Logger.LogInfo($"File loaded successfully: {loadedProject.Name}");

                // Cache the TIA Portal instance and project
                var instanceId = CacheObject(tiaPortal);
                CacheObject(loadedProject);

                // Extract discovery data from loaded project using existing method
                var archiveFileName = System.IO.Path.GetFileName(archivePath);
                var discoveryData = ExtractDiscoveryData(loadedProject, null, instanceId, isArchive: true, archiveFileName);

                return (true, loadedProject, discoveryData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading file: {ex.Message}");
                return (false, null, null);
            }
        }
    }
    
    public struct WorkflowConfiguration
    {
        // Source PLC (where content is copied FROM)
        public PlcSoftware SourcePlc { get; set; }
        public Device SourceDevice { get; set; }
        public PlcBlockUserGroup SelectedGroup { get; set; }
        public SecureString SourceSafetyPassword { get; set; }

        // Target PLC (where content is copied TO)
        public PlcSoftware TargetPlc { get; set; }

        // Processing configuration
        public int PrefixNumber { get; set; }
        public List<FindReplacePair> FindReplacePairs { get; set; }
        public List<FindReplacePair> ContentFindReplacePairs { get; set; }
        public List<TagTableConfig> SelectedTables { get; set; }
        public HashSet<string> ExistingTagTableNames { get; set; }
        public HashSet<string> ExistingBlockNames { get; set; }
    }
}