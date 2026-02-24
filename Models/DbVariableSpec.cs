using System.Collections.Generic;

namespace OpennessCopy.Models;

public class DbVariableSpec(List<string> variables, string structName = null)
{
    /// <summary>
    /// Target struct name; empty or null means root VAR scope.
    /// </summary>
    public string StructName { get; set; } = structName;

    /// <summary>
    /// Variable lines to append (already formatted for SD, e.g., "MY_VAR : Bool;").
    /// </summary>
    public List<string> VariableLines { get; set; } = variables;
}
