using System;
using System.Collections.Generic;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.TagTables;

public enum TagTableOperation
{
    CreateNew,
    AppendToExisting
}

public sealed class PlcTagTablePlan
{
    public PlcTagTablePlan(string tableName, TagTableOperation operation, IReadOnlyList<PlcTagEntry> tags)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name must be provided.", nameof(tableName));
        }

        TableName = tableName;
        Operation = operation;
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
    }

    public string TableName { get; }

    public TagTableOperation Operation { get; }

    public IReadOnlyList<PlcTagEntry> Tags { get; }
}

public sealed class PlcTagEntry
{
    public PlcTagEntry(
        string name,
        string dataType,
        string? logicalAddress,
        MultiLingualText? comment,
        bool? externalAccessible,
        bool? externalVisible,
        bool? externalWritable,
        bool replaceExisting)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name must be provided.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(dataType))
        {
            throw new ArgumentException("Tag data type must be provided.", nameof(dataType));
        }

        Name = name;
        DataType = dataType;
        LogicalAddress = logicalAddress;
        Comment = comment;
        ExternalAccessible = externalAccessible;
        ExternalVisible = externalVisible;
        ExternalWritable = externalWritable;
        ReplaceExisting = replaceExisting;
    }

    public string Name { get; }
    public string DataType { get; }
    public string? LogicalAddress { get; }
    public MultiLingualText? Comment { get; }
    public bool? ExternalAccessible { get; }
    public bool? ExternalVisible { get; }
    public bool? ExternalWritable { get; }
    public bool ReplaceExisting { get; }
}

public sealed class PlcTagTablePlanBuilder
{
    private readonly string _tableName;
    private readonly TagTableOperation _operation;
    private readonly List<PlcTagEntry> _entries = new();

    private PlcTagTablePlanBuilder(string tableName, TagTableOperation operation)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name must be provided.", nameof(tableName));
        }

        _tableName = tableName;
        _operation = operation;
    }

    public static PlcTagTablePlanBuilder ForCreation(string tableName)
    {
        return new PlcTagTablePlanBuilder(tableName, TagTableOperation.CreateNew);
    }

    public static PlcTagTablePlanBuilder ForAppend(string tableName)
    {
        return new PlcTagTablePlanBuilder(tableName, TagTableOperation.AppendToExisting);
    }

    public PlcTagTablePlanBuilder AddTag(
        string name,
        string dataType,
        string? logicalAddress = null,
        MultiLingualText? comment = null,
        bool? externalAccessible = true,
        bool? externalVisible = true,
        bool? externalWritable = null,
        bool replaceExisting = false)
    {
        _entries.Add(new PlcTagEntry(
            name,
            dataType,
            logicalAddress,
            comment,
            externalAccessible,
            externalVisible,
            externalWritable,
            replaceExisting));

        return this;
    }

    public PlcTagTablePlan Build()
    {
        if (_entries.Count == 0)
        {
            throw new InvalidOperationException($"Tag table '{_tableName}' must define at least one tag.");
        }

        return new PlcTagTablePlan(_tableName, _operation, _entries.ToArray());
    }
}
