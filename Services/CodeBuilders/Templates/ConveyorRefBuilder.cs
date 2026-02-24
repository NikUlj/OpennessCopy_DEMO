using System.Collections.Generic;
using System.Linq;

namespace OpennessCopy.Services.CodeBuilders.Templates;

public class ConveyorRefBuilder
{
    public string NamePrefix { get; }
    public IReadOnlyDictionary<string, string[]> TsegDict { get; }
    public string[] TrsNames;
    public string[] TsegNamesOrdered;
    
    ConveyorRefBuilder(IReadOnlyDictionary<string, string[]> tsegDict, string namePrefix)
    {
        NamePrefix = namePrefix;
        TsegDict = tsegDict;
        
        TrsNames = TsegDict.Values.SelectMany(x => x).ToArray();
        TsegNamesOrdered = TsegDict.Keys.OrderBy(x => x).ToArray();
    }
}