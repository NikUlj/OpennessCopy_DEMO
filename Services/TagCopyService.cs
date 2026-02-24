using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpennessCopy.Forms;
using OpennessCopy.Forms.BlockCopy;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;

namespace OpennessCopy.Services
{
    public static class TagCopyService
    {
        public class TagCopyInfo
        {
            public string OriginalName { get; set; }
            public string NewName { get; set; }
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
        }

        public class TableReplacementInfo
        {
            public TagTableReference TableInfo { get; set; }
            public List<FindReplacePair> NameReplacements { get; set; } = new List<FindReplacePair>();
            public List<TagAddressReplacePair> AddressReplacements { get; set; } = new List<TagAddressReplacePair>();
            public List<TagCopyInfo> CopiedTags { get; } = new();
        }

        /// <summary>
        /// Creates copies of all tags in the specified tables with string replacement
        /// </summary>
        private static void CreateTagCopies(List<TableReplacementInfo> replacementInfos, PlcSoftware plcSoftware, CancellationToken cancellationToken, HashSet<string> existingTagTableNames = null)
        {
            Logger.LogInfo("");
            Logger.LogInfo("========================================");
            Logger.LogInfo("        CREATING TAG COPIES");
            Logger.LogInfo("========================================\n");
            
            foreach (var replacementInfo in replacementInfos)
            {
                Logger.LogInfo($"Processing table: {replacementInfo.TableInfo.Path}");
                Logger.LogInfo("----------------------------------------");

                int nameCount = replacementInfo.NameReplacements.Count;
                int addressCount = replacementInfo.AddressReplacements.Count;
                
                if (nameCount == 0 && addressCount == 0)
                {
                    Logger.LogInfo("Mode: Adding _1 suffix to all tags (no replacements configured)");
                }
                else
                {
                    Logger.LogInfo($"Mode: {nameCount} name + {addressCount} address replacement(s) configured:");
                    
                    if (nameCount > 0)
                    {
                        Logger.LogInfo("  Name Replacements:");
                        for (int i = 0; i < replacementInfo.NameReplacements.Count; i++)
                        {
                            var pair = replacementInfo.NameReplacements[i];
                            Logger.LogInfo($"    {i + 1}. Replace '{pair.FindString}' with '{pair.ReplaceString}'");
                        }
                    }
                    
                    if (addressCount > 0)
                    {
                        Logger.LogInfo("  Address Replacements:");
                        for (int i = 0; i < replacementInfo.AddressReplacements.Count; i++)
                        {
                            var pair = replacementInfo.AddressReplacements[i];
                            Logger.LogInfo($"    {i + 1}. Replace '{pair.FindString}' with '{pair.ReplaceString}' at position {pair.DigitPosition} (length filter: {pair.LengthFilter})");
                        }
                    }
                }

                ProcessTagTable(replacementInfo, plcSoftware, cancellationToken, existingTagTableNames);
                
                // Summary for this table
                int successCount = replacementInfo.CopiedTags.Count(t => t.Success);
                int failCount = replacementInfo.CopiedTags.Count(t => !t.Success);
                
                Logger.LogInfo($"Results: {successCount} succeeded, {failCount} failed\n");
                
                if (failCount > 0)
                {
                    Logger.LogWarning("Failed tags:");
                    foreach (var failed in replacementInfo.CopiedTags.Where(t => !t.Success))
                    {
                        Logger.LogWarning($"  - {failed.OriginalName}: {failed.ErrorMessage}");
                    }
                }
            }
        }

        /// <summary>
        /// Process a single tag table and create copies of all tags in a new tag table
        /// </summary>
        private static void ProcessTagTable(TableReplacementInfo replacementInfo, PlcSoftware plcSoftware, CancellationToken cancellationToken, HashSet<string> existingTagTableNames = null)
        {
            var sourceTable = replacementInfo.TableInfo.Table;
            
            // Create the destination tag table (avoid name collisions).
            string baseTableName = sourceTable.Name + "_Copy";

            // Track names we've already created to avoid collisions.
            existingTagTableNames ??= new HashSet<string>();
            
            var uniqueTableName = FindUniqueTagTableName(existingTagTableNames, baseTableName);
            existingTagTableNames.Add(uniqueTableName);
            
            if (uniqueTableName != baseTableName)
            {
                Logger.LogWarning($"  Name conflict detected, using unique name: {baseTableName} -> {uniqueTableName}");
            }
            
            PlcTagTable copyTable;
            try
            {
                // Create the new copy table with unique name
                copyTable = plcSoftware.TagTableGroup.TagTables.Create(uniqueTableName);
                Logger.LogInfo($"  Created new tag table: {uniqueTableName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"  Could not create copy table '{uniqueTableName}': {ex.Message}", false);
                return;
            }
            
            // Copy tags into the destination table.
            // Convert to list to avoid enumeration issues
            var tagsToProcess = sourceTable.Tags.ToList();
            
            foreach (PlcTag tag in tagsToProcess)
            {
                var copyInfo = new TagCopyInfo
                {
                    OriginalName = tag.Name,
                };
                
                try
                {
                    // Generate new tag name based on replacement settings
                    string baseNewName = GenerateNewTagName(tag.Name, replacementInfo.NameReplacements);
                    
                    // Process tag address based on replacement settings
                    string newAddress = ProcessTagAddress(tag.LogicalAddress, replacementInfo.AddressReplacements);
                    
                    // Create the new tag with conflict resolution
                    var newTag = CreateTagWithConflictResolution(copyTable, baseNewName, tag.DataTypeName, newAddress);
                    string uniqueNewName = newTag.Name;
                    
                    copyInfo.NewName = uniqueNewName;
                    
                    // Copy all properties from original tag
                    CopyTagProperties(tag, newTag, replacementInfo.NameReplacements);
                    
                    copyInfo.Success = true;

                    Logger.LogInfo(uniqueNewName != baseNewName
                        ? $"    Copied: {tag.Name} -> {uniqueNewName} (renamed due to conflict)"
                        : $"    Copied: {tag.Name} -> {uniqueNewName}");
                }
                catch (Exception ex)
                {
                    copyInfo.Success = false;
                    copyInfo.ErrorMessage = ex.Message;
                    Logger.LogError($"    FAILED: {tag.Name} -> {copyInfo.NewName}: {ex.Message}", false);
                }
                
                replacementInfo.CopiedTags.Add(copyInfo);

                cancellationToken.ThrowIfCancellationRequested();
            }
            
            // Copy user constants using the same naming rules.
            var constantsToProcess = sourceTable.UserConstants.ToList();
            
            foreach (PlcUserConstant constant in constantsToProcess)
            {
                try
                {
                    // Generate new constant name using same logic as tags
                    string baseNewConstantName = GenerateNewTagName(constant.Name, replacementInfo.NameReplacements);
                    
                    // Create the new constant with conflict resolution
                    var newConstant = CreateConstantWithConflictResolution(copyTable, baseNewConstantName);
                    string uniqueNewConstantName = newConstant.Name;
                    
                    // Set constant properties
                    newConstant.DataTypeName = constant.DataTypeName;
                    newConstant.Value = constant.Value;

                    Logger.LogInfo(uniqueNewConstantName != baseNewConstantName
                        ? $"    Copied constant: {constant.Name} -> {uniqueNewConstantName} (renamed due to conflict)"
                        : $"    Copied constant: {constant.Name} -> {uniqueNewConstantName}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"    FAILED to copy constant {constant.Name}: {ex.Message}", false);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static PlcTag CreateTagWithConflictResolution(PlcTagTable tagTable, string baseName, string dataTypeName, string logicalAddress)
        {
            string candidateName = baseName;
            int counter = 1;
            
            while (true)
            {
                try
                {
                    // Try to create the tag
                    return tagTable.Tags.Create(candidateName, dataTypeName, logicalAddress);
                }
                catch (Exception ex)
                {
                    // Check if the exception indicates the name already exists
                    if (ex.Message.Contains("already exists") || ex.Message.Contains("already defined") || 
                        ex.Message.Contains("duplicate") || ex.Message.Contains("name is not unique"))
                    {
                        // Try next candidate name
                        candidateName = $"{baseName}_{counter}";
                        counter++;
                        
                        // Safety check to avoid infinite loop
                        if (counter > 100)
                        {
                            throw new InvalidOperationException($"Could not find unique name after 100 attempts for base name: {baseName}");
                        }
                    }
                    else
                    {
                        // Different error, rethrow
                        throw;
                    }
                }
            }
        }

        private static PlcUserConstant CreateConstantWithConflictResolution(PlcTagTable tagTable, string baseName)
        {
            string candidateName = baseName;
            int counter = 1;
            
            while (true)
            {
                try
                {
                    // Try to create the constant
                    return tagTable.UserConstants.Create(candidateName);
                }
                catch (Exception ex)
                {
                    // Check if the exception indicates the name already exists
                    if (ex.Message.Contains("already exists") || ex.Message.Contains("already defined") || 
                        ex.Message.Contains("duplicate") || ex.Message.Contains("name is not unique"))
                    {
                        // Try next candidate name
                        candidateName = $"{baseName}_{counter}";
                        counter++;
                        
                        // Safety check to avoid infinite loop
                        if (counter > 1000)
                        {
                            throw new InvalidOperationException($"Could not find unique name after 1000 attempts for base name: {baseName}");
                        }
                    }
                    else
                    {
                        // Different error, rethrow
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Generate new tag name based on multiple replacement rules
        /// </summary>
        private static string GenerateNewTagName(string originalName, List<FindReplacePair> nameReplacements)
        {
            if (nameReplacements == null || nameReplacements.Count == 0)
            {
                // No replacements defined, add _1 suffix
                return originalName + "_1";
            }
            
            string currentName = originalName;
            bool anyReplacement = false;
            
            // Apply all replacements in sequence
            foreach (var pair in nameReplacements)
            {
                if (!string.IsNullOrEmpty(pair.FindString))
                {
                    if (currentName.Contains(pair.FindString))
                    {
                        currentName = currentName.Replace(pair.FindString, pair.ReplaceString ?? "");
                        anyReplacement = true;
                    }
                }
            }
            
            // If no replacements were applied, add _1 suffix
            if (!anyReplacement)
            {
                return originalName + "_1";
            }
            
            return currentName;
        }

        /// <summary>
        /// Process tag address with multiple replacement rules
        /// </summary>
        private static string ProcessTagAddress(string originalAddress, List<TagAddressReplacePair> addressReplacements)
        {
            if (addressReplacements == null || addressReplacements.Count == 0)
            {
                return originalAddress; // No address changes
            }

            string currentAddress = originalAddress;
            
            // Apply all address replacements in sequence
            foreach (var pair in addressReplacements)
            {
                if (!string.IsNullOrEmpty(pair.FindString))
                {
                    currentAddress = ProcessSingleAddressReplacement(currentAddress, pair);
                }
            }
            
            return currentAddress;
        }

        /// <summary>
        /// Apply a single address replacement rule to an address
        /// Supports addresses like %M127.0, %QW1234.7, %IW5.2, etc.
        /// </summary>
        private static string ProcessSingleAddressReplacement(string address, TagAddressReplacePair pair)
        {
            // Parse address format: %[Type][Digits].[Bit]
            if (address.Contains("%") && address.Contains("."))
            {
                int percentIndex = address.IndexOf('%');
                int dotIndex = address.IndexOf('.');
                
                if (dotIndex > percentIndex + 1)
                {
                    // Find where the prefix ends and digits begin
                    int digitStartIndex = percentIndex + 1;
                    while (digitStartIndex < dotIndex && !char.IsDigit(address[digitStartIndex]))
                    {
                        digitStartIndex++;
                    }
                    
                    if (digitStartIndex < dotIndex)
                    {
                        string prefix = address.Substring(0, digitStartIndex); // e.g., "%M", "%QW", "%MW"
                        string digits = address.Substring(digitStartIndex, dotIndex - digitStartIndex); // e.g., "127", "1234"
                        string suffix = address.Substring(dotIndex); // e.g., ".0", ".7"
                        
                        // Check length filter - only process addresses with specified digit count
                        if (digits.Length == pair.LengthFilter)
                        {
                            // Apply replacement to specific digit position (right-to-left counting)
                            if (pair.DigitPosition > 0 && pair.DigitPosition <= digits.Length && !string.IsNullOrEmpty(pair.FindString))
                            {
                                int findLength = pair.FindString.Length;
                                int rightmostIndex = digits.Length - pair.DigitPosition; // Where rightmost digit of find string should be
                                int leftmostIndex = rightmostIndex - findLength + 1; // Where leftmost digit of find string should be
                                
                                // Check if we have enough digits to the left for the find string
                                if (leftmostIndex >= 0 && rightmostIndex < digits.Length)
                                {
                                    // Extract the substring to compare
                                    string currentSubstring = digits.Substring(leftmostIndex, findLength);
                                    
                                    if (currentSubstring == pair.FindString)
                                    {
                                        // Replace the multi-digit sequence
                                        string beforeReplacement = digits.Substring(0, leftmostIndex);
                                        string afterReplacement = digits.Substring(rightmostIndex + 1);
                                        string replacement = pair.ReplaceString ?? "";
                                        
                                        digits = beforeReplacement + replacement + afterReplacement;
                                    }
                                }
                            }
                        }
                        
                        return prefix + digits + suffix;
                    }
                }
            }
            
            return address; // Return unchanged if parsing fails
        }

        private static void CopyTagProperties(PlcTag sourceTag, PlcTag targetTag, List<FindReplacePair> nameReplacements)
        {
            try
            {
                // Copy comment if it exists - apply the same find/replace logic to comments
                if (sourceTag.Comment != null)
                {
                    CopyMultilingualComment(sourceTag.Comment, targetTag.Comment, nameReplacements);
                }
                
                // Copy external access properties
                targetTag.ExternalAccessible = sourceTag.ExternalAccessible;
                targetTag.ExternalVisible = sourceTag.ExternalVisible;
                targetTag.ExternalWritable = sourceTag.ExternalWritable;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"      Could not copy all properties: {ex.Message}");
            }
        }

        private static void CopyMultilingualComment(MultilingualText sourceComment, MultilingualText targetComment, List<FindReplacePair> nameReplacements)
        {
            try
            {
                // Extract comment data as strings first to avoid cross-instance COM object issues
                var commentData = new List<(string Language, string Text)>();
                
                foreach (MultilingualTextItem sourceItem in sourceComment.Items)
                {
                    if (!string.IsNullOrEmpty(sourceItem.Text))
                    {
                        // Extract language and text as strings (safe for cross-instance copying)
                        string language = sourceItem.Language.Culture.Name;
                        string text = sourceItem.Text;
                        commentData.Add((language, text));
                    }
                }

                // Now apply transformations and copy to target using extracted string data
                foreach (var (language, originalText) in commentData)
                {
                    Logger.LogInfo($"      Original comment ({language}): '{originalText}'");
                    
                    // Apply the same find/replace transformations to the comment text
                    string updatedText = originalText;
                    
                    if (nameReplacements is { Count: > 0 })
                    {
                        // Apply all replacements in sequence (same as GenerateNewTagName logic)
                        foreach (var pair in nameReplacements)
                        {
                            if (!string.IsNullOrEmpty(pair.FindString) && updatedText.Contains(pair.FindString))
                            {
                                updatedText = updatedText.Replace(pair.FindString, pair.ReplaceString ?? "");
                                Logger.LogInfo($"        Applied replacement: '{pair.FindString}' -> '{pair.ReplaceString}'");
                            }
                        }
                    }
                    
                    // Find target language item by comparing language strings directly
                    try 
                    {
                        MultilingualTextItem targetItem = null;
                        
                        // Iterate through target comment items to find matching language
                        foreach (MultilingualTextItem item in targetComment.Items)
                        {
                            if (item.Language.Culture.Name == language)
                            {
                                targetItem = item;
                                break;
                            }
                        }
                        
                        if (targetItem != null)
                        {
                            Logger.LogInfo($"      Updated comment ({language}): '{updatedText}'");
                            targetItem.Text = updatedText;
                        }
                        else
                        {
                            Logger.LogWarning($"      Could not find target comment item for language: {language}");
                        }
                    }
                    catch (Exception langEx)
                    {
                        Logger.LogWarning($"      Could not set comment for language {language}: {langEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"      Could not copy comment: {ex.Message}");
            }
        }

        private static string FindUniqueTagTableName(HashSet<string> existingNames, string baseName)
        {
            string candidateName = baseName;
            int counter = 2;
            
            // Keep incrementing until we find a name that doesn't exist
            while (existingNames.Contains(candidateName))
            {
                candidateName = $"{baseName}_{counter}";
                counter++;
            }
            
            return candidateName;
        }
        
        public static List<TableReplacementInfo> ProcessGUITagTables(PlcSoftware plcSoftware,
            List<TagTableConfig> selectedTables,
            HashSet<string> existingTagTableNames,
            CancellationToken cancellationToken)
        {
            try
            {
                var replacementInfos = new List<TableReplacementInfo>();
                
                foreach (var tableConfig in selectedTables)
                {
                    // Resolve the actual PlcTagTable from the cached ID
                    var actualTable = DataCacheUtility.GetCachedObject<PlcTagTable>(tableConfig.TableId);
                    if (actualTable == null)
                    {
                        Logger.LogError($"Could not resolve tag table '{tableConfig.TableName}' from cache", false);
                        continue;
                    }
                    
                    var replacementInfo = new TableReplacementInfo
                    {
                        TableInfo = new TagTableReference
                        {
                            Table = actualTable,
                            Path = tableConfig.TableName
                        },
                        NameReplacements = [..tableConfig.NameReplacements],
                        AddressReplacements = new List<TagAddressReplacePair>(tableConfig.AddressReplacements)
                    };
                    
                    replacementInfos.Add(replacementInfo);
                }

                // Create tag copies with existing names for conflict resolution
                CreateTagCopies(replacementInfos, plcSoftware, cancellationToken, existingTagTableNames);

                return replacementInfos;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Tag processing error: {ex.Message}", false);
                throw;
            }
        }
    }
    
    public class TagTableReference
    {
        public string Path { get; set; }
        public PlcTagTable Table { get; set; }
    }
}
