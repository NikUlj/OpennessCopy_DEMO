using System;
using System.Collections.Generic;
using System.Threading;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;

#nullable enable

namespace OpennessCopy.Services.CodeBuilders.TagTables;

public static class TagTableGenerationService
{
    public static void ApplyPlans(
        PlcSoftware plcSoftware,
        IEnumerable<PlcTagTablePlan> plans,
        CancellationToken cancellationToken,
        Action<string>? infoLogger = null,
        Action<string>? errorLogger = null)
    {
        if (plcSoftware == null)
        {
            throw new ArgumentNullException(nameof(plcSoftware));
        }

        if (plans == null)
        {
            throw new ArgumentNullException(nameof(plans));
        }

        var tagTables = plcSoftware.TagTableGroup.TagTables;

        foreach (var plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var tagTable = GetOrCreateTagTable(tagTables, plan, infoLogger);

                foreach (var entry in plan.Tags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    UpsertTag(tagTable, entry, infoLogger);
                }
            }
            catch (Exception ex)
            {
                errorLogger?.Invoke($"Tag table '{plan.TableName}' processing failed: {ex.Message}");
                throw;
            }
        }
    }

    private static PlcTagTable GetOrCreateTagTable(
        PlcTagTableComposition tagTables,
        PlcTagTablePlan plan,
        Action<string>? infoLogger)
    {
        var existing = FindTagTable(tagTables, plan.TableName);

        switch (plan.Operation)
        {
            case TagTableOperation.CreateNew:
                if (existing != null)
                {
                    throw new InvalidOperationException($"Tag table '{plan.TableName}' already exists.");
                }

                infoLogger?.Invoke($"Creating tag table '{plan.TableName}'.");
                return tagTables.Create(plan.TableName);

            case TagTableOperation.AppendToExisting:
                if (existing == null)
                {
                    throw new InvalidOperationException($"Tag table '{plan.TableName}' does not exist for append operation.");
                }

                infoLogger?.Invoke($"Appending to existing tag table '{plan.TableName}'.");
                return existing;

            default:
                throw new ArgumentOutOfRangeException(nameof(plan.Operation), plan.Operation, "Unsupported tag table operation.");
        }
    }

    private static PlcTagTable? FindTagTable(PlcTagTableComposition tables, string name)
    {
        foreach (PlcTagTable table in tables)
        {
            if (table.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return table;
            }
        }

        return null;
    }

    private static void UpsertTag(PlcTagTable tagTable, PlcTagEntry entry, Action<string>? infoLogger)
    {
        var existing = FindTag(tagTable, entry.Name);
        if (existing != null)
        {
            if (!entry.ReplaceExisting)
            {
                infoLogger?.Invoke($"Tag '{entry.Name}' already exists in '{tagTable.Name}', skipping.");
                return;
            }

            infoLogger?.Invoke($"Tag '{entry.Name}' exists in '{tagTable.Name}', replacing.");
            existing.Delete();
        }

        var logicalAddress = entry.LogicalAddress ?? string.Empty;
        var newTag = tagTable.Tags.Create(entry.Name, entry.DataType, logicalAddress);

        ApplyComment(newTag, entry.Comment, infoLogger);
        ApplyExternalAccess(newTag, entry);
    }

    private static PlcTag? FindTag(PlcTagTable tagTable, string name)
    {
        foreach (PlcTag tag in tagTable.Tags)
        {
            if (tag.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return tag;
            }
        }

        return null;
    }

    private static void ApplyComment(PlcTag tag, MultiLingualText? comment, Action<string>? infoLogger)
    {
        if (comment == null || comment.IsEmpty)
        {
            return;
        }

        var multilingualComment = tag.Comment;
        if (multilingualComment == null)
        {
            infoLogger?.Invoke($"Tag '{tag.Name}' does not expose multilingual comments; skipping text assignment.");
            return;
        }

        foreach (var translation in comment.Translations)
        {
            var targetItem = FindCommentItem(multilingualComment, translation.Key);
            if (targetItem != null)
            {
                targetItem.Text = translation.Value;
            }
            else
            {
                infoLogger?.Invoke($"Tag '{tag.Name}' comment: language '{translation.Key}' not available, skipping text.");
            }
        }
    }

    private static MultilingualTextItem? FindCommentItem(MultilingualText comment, string language)
    {
        foreach (MultilingualTextItem item in comment.Items)
        {
            if (item.Language.Culture.Name.Equals(language, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private static void ApplyExternalAccess(PlcTag tag, PlcTagEntry entry)
    {
        if (entry.ExternalAccessible.HasValue)
        {
            tag.ExternalAccessible = entry.ExternalAccessible.Value;
        }

        if (entry.ExternalVisible.HasValue)
        {
            tag.ExternalVisible = entry.ExternalVisible.Value;
        }

        if (entry.ExternalWritable.HasValue)
        {
            tag.ExternalWritable = entry.ExternalWritable.Value;
        }
    }
}
