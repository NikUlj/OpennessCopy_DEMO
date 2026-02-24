using System;
using System.Collections.Generic;
using OpennessCopy.Models;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using static OpennessCopy.Utils.DataCacheUtility;

namespace OpennessCopy.Services.BlockSelection;

/// <summary>
/// Enumerates PLC blocks on the STA thread and produces lightweight DTOs for UI consumption.
/// </summary>
public static class PlcBlockEnumerationService
{
    public static List<PlcBlockInfo> EnumerateBlocks(
        PlcSoftware plcSoftware,
        HashSet<PlcBlockKind> allowedKinds,
        bool includeInstanceDbs)
    {
        if (plcSoftware == null) throw new ArgumentNullException(nameof(plcSoftware));
        allowedKinds ??= new HashSet<PlcBlockKind>();

        var result = new List<PlcBlockInfo>();
        var rootGroup = plcSoftware.BlockGroup;

        // Root path is empty; child paths will be "Group/SubGroup"
        CollectBlocks(rootGroup, string.Empty, allowedKinds, includeInstanceDbs, result);
        return result;
    }

    private static void CollectBlocks(
        PlcBlockGroup group,
        string currentPath,
        HashSet<PlcBlockKind> allowedKinds,
        bool includeInstanceDbs,
        List<PlcBlockInfo> result)
    {
        // Blocks in the current group
        foreach (PlcBlock block in group.Blocks)
        {
            var isInstanceDb = block is InstanceDB;
            var isGlobalDb = block is GlobalDB;
            var isArrayDb = block is ArrayDB;
            string instanceOfName = block is InstanceDB idb ? idb.InstanceOfName : string.Empty;
            PlcBlockKind kind;

            if (block is OB)
            {
                kind = PlcBlockKind.OB;
            }
            else if (block is FB)
            {
                kind = PlcBlockKind.FB;
            }
            else if (block is FC)
            {
                kind = PlcBlockKind.FC;
            }
            else if (isInstanceDb)
            {
                kind = PlcBlockKind.InstanceDb;
            }
            else if (isGlobalDb)
            {
                kind = PlcBlockKind.GlobalDb;
            }
            else if (isArrayDb)
            {
                kind = PlcBlockKind.ArrayDb;
            }
            else
            {
                kind = PlcBlockKind.Unknown;
            }

            if (!includeInstanceDbs && kind == PlcBlockKind.InstanceDb)
            {
                continue;
            }

            bool isAllowedKind = allowedKinds.Count == 0 || allowedKinds.Contains(kind);
            if (!isAllowedKind)
            {
                continue;
            }

            result.Add(new PlcBlockInfo
            {
                Name = block.Name,
                BlockNumber = block.Number,
                Kind = kind,
                IsInstanceDb = isInstanceDb,
                InstanceOfName = instanceOfName,
                GroupPath = currentPath,
                BlockId = CacheObject(block)
            });
        }

        // Traverse subgroups
        foreach (PlcBlockUserGroup subgroup in group.Groups)
        {
            var path = string.IsNullOrEmpty(currentPath)
                ? subgroup.Name
                : $"{currentPath}/{subgroup.Name}";

            CollectBlocks(subgroup, path, allowedKinds, includeInstanceDbs, result);
        }
    }
}
