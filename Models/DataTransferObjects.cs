using System.Collections.Generic;
using OpennessCopy.Forms.BlockCopy;
using Siemens.Engineering.HW;

namespace OpennessCopy.Models
{
    public class PLCInfo
    {
        public string Name { get; set; }
        public string DeviceName { get; set; }
        public string PlcId { get; set; }
        public string DeviceId { get; set; }
        public string TiaInstanceId { get; set; }
        public string ProjectName { get; set; }
        public bool IsArchive { get; set; }
        public HashSet<string> ActiveCultures { get; set; } = new HashSet<string>();
        public SafetyPasswordData SafetyPasswordData { get; set; }
        public BlockGroupSelectionData BlockGroupData { get; set; }
        public TagTableSelectionData TagTableData { get; set; }
    }

    public class BlockGroupInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int BlockCount { get; set; }
        public int SubGroupCount { get; set; }
        public string GroupId { get; set; }
        public List<BlockGroupInfo> SubGroups { get; set; } = new List<BlockGroupInfo>();
    }

    public class TagTableInfo
    {
        public string Name { get; set; }
        public int TagCount { get; set; }
        public string TableId { get; set; }
    }

    public class TagExample
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public int DigitCount { get; set; }
    }

    public class SafetyAdminInfo
    {
        public bool IsSafetyOfflineProgramPasswordSet { get; set; }
    }

    public class DiscoveryData
    {
        public List<PLCInfo> PLCs { get; set; } = new List<PLCInfo>();
    }

    public class UserSelections
    {
        public string SourcePlcId { get; set; }
        public string SourceDeviceId { get; set; }
        public string SelectedGroupId { get; set; }
        
        public string TargetPlcId { get; set; }
        public string TargetDeviceId { get; set; }
        
        public string SourceSafetyPassword { get; set; }
        
        public bool IsSourceTargetSamePLC => SourcePlcId == TargetPlcId && SourceDeviceId == TargetDeviceId;
        
        public int PrefixNumber { get; set; }
        public List<FindReplacePair> FindReplacePairs { get; set; } = new List<FindReplacePair>();
        public List<FindReplacePair> ContentFindReplacePairs { get; set; } = new List<FindReplacePair>();
        public List<TagTableConfig> SelectedTables { get; set; } = new List<TagTableConfig>();
        public HashSet<string> ExistingBlockNames { get; set; } = new HashSet<string>();
        public HashSet<string> ExistingTagTableNames { get; set; } = new HashSet<string>();
    }

    public class BlockGroupSelectionData
    {
        public List<BlockGroupInfo> RootGroups { get; set; } = new List<BlockGroupInfo>();
        public HashSet<string> ExistingBlockNames { get; set; } = new HashSet<string>();
        public HashSet<int> ExistingBlockNumbers { get; set; } = new HashSet<int>();
        public string PlcName { get; set; }
    }

    public class TagTableSelectionData
    {
        public List<TagTableInfo> TagTables { get; set; } = new List<TagTableInfo>();
        public HashSet<string> ExistingTableNames { get; set; } = new HashSet<string>();
    }

    public class SafetyPasswordData
    {
        public SafetyAdminInfo SafetyAdmin { get; set; }
        public string DeviceName { get; set; }
    }

    public class HardwareDeviceInfo
    {
        public string Name { get; set; }
        public string ItemName { get; set; }
        public string DeviceType { get; set; }
        public string DeviceId { get; set; }
        public string ProjectId { get; set; }
        public List<string> IpAddresses { get; set; } = new List<string>();
        public int? DeviceNumber { get; set; }
        public string IoSystemId { get; set; }
        public int? IoSystemHash { get; set; }
        public int NetworkPortCount { get; set; }

        public bool IsETDevice { get; set; }
        public List<DeviceAddressInfo> AddressModules { get; set; } = new List<DeviceAddressInfo>();
        public string ControllingPlcName { get; set; }
    }

    public class DeviceAddressInfo
    {
        public string ModuleName { get; set; }
        public List<AddressInfo> AddressInfos { get; set; } = new List<AddressInfo>();
    }

    public class AddressInfo
    {
        public int StartAddress { get; set; }
        public int Length { get; set; }
        public AddressIoType Type { get; set; }
    }

    public class TiaPortalInstanceInfo
    {
        public string ProjectName { get; set; }
        public string ProjectId { get; set; }
        public string InstanceId { get; set; }
        public int ProcessId { get; set; }
        public bool IsArchive { get; set; }
        public List<HardwareDeviceInfo> HardwareDevices { get; set; } = new List<HardwareDeviceInfo>();
    }

    public class HardwareDiscoveryData
    {
        public List<TiaPortalInstanceInfo> TiaInstances { get; set; } = new List<TiaPortalInstanceInfo>();
    }

    public class IoSystemInfo
    {
        public string Name { get; set; }
        public string SubnetName { get; set; }
        public string IoSystemId { get; set; }
        public string SubnetId { get; set; }
        public string NetworkAddress { get; set; }
        public string SubnetMask { get; set; }
        public int IoSystemHash { get; set; }
        public string ControllingPlcName { get; set; }
    }

    public class HardwareUserSelections
    {
        public string SourceProjectId { get; set; }
        public string SourceInstanceId { get; set; }
        public string TargetProjectId { get; set; }
        public string TargetInstanceId { get; set; }

        public bool IsSourceTargetSameProject => SourceProjectId == TargetProjectId;

        public List<HardwareDeviceInfo> SelectedDevices { get; set; } = new List<HardwareDeviceInfo>();
        public List<FindReplacePair> DeviceNameFindReplacePairs { get; set; } = new List<FindReplacePair>();
        public int IpAddressOffset { get; set; }
        public List<TagAddressReplacePair> ETAddressReplacements { get; set; } = new List<TagAddressReplacePair>();
        public IoSystemInfo SelectedIoSystem { get; set; }
    }
}
