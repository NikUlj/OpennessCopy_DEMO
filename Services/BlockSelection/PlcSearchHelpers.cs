#nullable enable
using System;
using System.Collections.Generic;
using OpennessCopy.Models;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace OpennessCopy.Services.BlockSelection;

public static class PlcSearchHelpers
{
    public static PlcBlock? FindBlockByName(
        PlcSoftware plcSoftware,
        string blockName,
        HashSet<PlcBlockKind>? allowedKinds = null)
    {
        if (plcSoftware == null) throw new ArgumentNullException(nameof(plcSoftware));
        if (string.IsNullOrWhiteSpace(blockName)) throw new ArgumentException("Block name must not be empty.", nameof(blockName));

        allowedKinds ??= new HashSet<PlcBlockKind>();
        var block = FindBlockRecursive(plcSoftware.BlockGroup, blockName);
        if (block == null)
        {
            return null;
        }

        var kind = Classify(block);
        if (allowedKinds.Count > 0 && !allowedKinds.Contains(kind))
        {
            return null;
        }

        return block;
    }

    public static bool TryFindBlockByName(
        PlcSoftware plcSoftware,
        string blockName,
        out PlcBlock? block,
        out PlcBlockKind kind,
        HashSet<PlcBlockKind>? allowedKinds = null)
    {
        block = null;
        kind = PlcBlockKind.Unknown;

        var found = FindBlockByName(plcSoftware, blockName, allowedKinds);
        if (found == null)
        {
            return false;
        }

        block = found;
        kind = Classify(found);
        return true;
    }

    private static PlcBlock? FindBlockRecursive(PlcBlockGroup group, string blockName)
    {
        var block = group.Blocks.Find(blockName);
        if (block != null)
        {
            return block;
        }

        foreach (PlcBlockUserGroup subGroup in group.Groups)
        {
            var found = FindBlockRecursive(subGroup, blockName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static PlcBlockKind Classify(PlcBlock block)
    {
        return block switch
        {
            OB => PlcBlockKind.OB,
            FB => PlcBlockKind.FB,
            FC => PlcBlockKind.FC,
            InstanceDB => PlcBlockKind.InstanceDb,
            GlobalDB => PlcBlockKind.GlobalDb,
            ArrayDB => PlcBlockKind.ArrayDb,
            _ => PlcBlockKind.Unknown
        };
    }
}
