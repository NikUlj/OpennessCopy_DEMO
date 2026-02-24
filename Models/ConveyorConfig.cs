#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace OpennessCopy.Models;

public class ConveyorConfig
{
    public string NamePrefix { get; set; } = string.Empty;

    public int StartBlockNumber { get; set; }

    public Dictionary<DbConfigKey, DbConfig> DbConfigs { get; set; } = new();

    public List<TsegConfig> TsegGroups { get; set; } = new();

    private IReadOnlyDictionary<string, string[]>? _activeTsegDict;
    private string[]? _activeTrsNames;
    private string[]? _activeTsegNamesOrdered;

    public IReadOnlyDictionary<string, string[]> ActiveTsegDictionary =>
        _activeTsegDict ??= BuildActiveTsegDictionary();

    public string[] ActiveTrsNames =>
        _activeTrsNames ??= BuildActiveTrsNames();

    public string[] ActiveTsegNamesOrdered =>
        _activeTsegNamesOrdered ??= BuildActiveTsegNamesOrdered();

    public void InvalidateDerived()
    {
        _activeTsegDict = null;
        _activeTrsNames = null;
        _activeTsegNamesOrdered = null;
    }

    private IReadOnlyDictionary<string, string[]> BuildActiveTsegDictionary()
    {
        var result = new Dictionary<string, string[]>();

        foreach (var tseg in TsegGroups)
        {
            var activeTrs = tseg.TrsList
                .Where(t => t.Active)
                .Select(t => t.Name)
                .ToArray();

            if (activeTrs.Length == 0)
            {
                continue;
            }

            result[tseg.Name] = activeTrs;
        }

        return result;
    }

    private string[] BuildActiveTrsNames()
    {
        return TsegGroups
            .SelectMany(tseg => tseg.TrsList)
            .Where(trs => trs.Active)
            .Select(trs => trs.Name)
            .ToArray();
    }

    private string[] BuildActiveTsegNamesOrdered()
    {
        return TsegGroups
            .Where(tseg => tseg.TrsList.Any(trs => trs.Active))
            .Select(tseg => tseg.Name)
            .OrderBy(name => name)
            .ToArray();
    }
}

public class TsegConfig
{
    public string Name { get; set; } = string.Empty;

    public List<TrsConfig> TrsList { get; set; } = new();
}

public class TrsConfig
{
    public string Name { get; set; } = string.Empty;

    public bool Active { get; set; } = true;
}
