using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using static OpennessCopy.Services.TagCopyService;
using static OpennessCopy.Services.ValidationService;

namespace OpennessCopy.Services.BlockCopy
{
    public static class BlockXmlProcessingService
    {
        /// <summary>
        /// Updates all aspects of exported block XML files: block names, block numbers, block references, tag references, and content
        /// </summary>
        /// <param name="exportDirectory">Directory containing exported block XML files</param>
        /// <param name="tagReplacementInfos">Tag replacement mapping information</param>
        /// <param name="blockPrefixNumber">Block number prefix for renumbering</param>
        /// <param name="blockFindReplacePairs">Block name find/replace pairs</param>
        /// <param name="contentFindReplacePairs">Content find/replace pairs for variables and comments</param>
        /// <param name="existingBlockNames">Set of existing block names for conflict resolution</param>
        public static void UpdateExportedBlocks(string exportDirectory, 
            List<TableReplacementInfo> tagReplacementInfos, 
            int blockPrefixNumber, 
            List<FindReplacePair> blockFindReplacePairs, 
            List<FindReplacePair> contentFindReplacePairs,
            HashSet<string> existingBlockNames)
        {
            Logger.LogInfo("\n========================================");
            Logger.LogInfo("    UPDATING EXPORTED BLOCK FILES");
            Logger.LogInfo("========================================\n");
            
            if (!Directory.Exists(exportDirectory))
            {
                Logger.LogError($"Export directory not found: {exportDirectory}", false);
                return;
            }

            // Build replacement mappings
            var combinedMapping = BuildTagMapping(tagReplacementInfos);
            var fileContents = BuildBlockMappingAndLoadFiles(exportDirectory,
                    blockFindReplacePairs,
                    existingBlockNames,
                    ref combinedMapping);
            
            Logger.LogInfo($"Built combined mapping: {combinedMapping.Count} total replacements");
            Logger.LogInfo("Processing exported block files...\n");
            
            // Process all XML files using combined approach
            var (totalFiles, processedFiles, updatedFiles) = 
                ProcessAllFilesUnified(fileContents, blockPrefixNumber, combinedMapping, contentFindReplacePairs);
            
            // Display summary
            Logger.LogInfo("");
            Logger.LogInfo("----------------------------------------");
            Logger.LogInfo($"Unified Block Processing Summary:");
            Logger.LogInfo($"  Total XML files found: {totalFiles}");
            Logger.LogInfo($"  Files processed: {processedFiles}");
            Logger.LogInfo($"  Files with updates: {updatedFiles}");
            Logger.LogInfo("----------------------------------------");
        }

        /// <summary>
        /// Builds a mapping dictionary from original tag names to new tag names
        /// </summary>
        private static Dictionary<string, string> BuildTagMapping(List<TableReplacementInfo> replacementInfos)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            Logger.LogInfo("Building tag replacement mapping:");
            
            if (replacementInfos == null)
            {
                Logger.LogInfo("  No tag replacements configured (no tag tables selected)");
                return mapping;
            }
            
            foreach (var replacementInfo in replacementInfos)
            {
                Logger.LogInfo($"  Table: {replacementInfo.TableInfo.Path}");
                
                int addedFromTable = 0;
                foreach (var tagCopy in replacementInfo.CopiedTags)
                {
                    if (tagCopy.Success && !string.IsNullOrEmpty(tagCopy.OriginalName) && !string.IsNullOrEmpty(tagCopy.NewName))
                    {
                        if (!mapping.ContainsKey(tagCopy.OriginalName))
                        {
                            mapping[tagCopy.OriginalName] = tagCopy.NewName;
                            addedFromTable++;
                        }
                        else
                        {
                            Logger.LogWarning($"    Warning: Duplicate mapping for tag '{tagCopy.OriginalName}' - using first occurrence");
                        }
                    }
                }
                
                Logger.LogInfo($"    Added {addedFromTable} mappings from this table");
            }
            
            Logger.LogInfo($"Total unique tag mappings: {mapping.Count}\n");
            return mapping;
        }

        /// <summary>
        /// Applies name transformations using FindReplacePair logic with conflict resolution
        /// </summary>
        private static string ApplyBlockNameTransformations(string originalName, List<FindReplacePair> findReplacePairs, HashSet<string> existingBlockNames)
        {
            string currentName = originalName;
            bool anyReplacementMade = false;
            
            // Apply all replacement pairs in sequence
            if (findReplacePairs != null)
            {
                foreach (var pair in findReplacePairs)
                {
                    if (!string.IsNullOrEmpty(pair.FindString) && currentName.Contains(pair.FindString))
                    {
                        string newName = currentName.Replace(pair.FindString, pair.ReplaceString);
                        currentName = newName;
                        anyReplacementMade = true;
                    }
                }
            }

            // If no replacements were made, log it
            if (!anyReplacementMade)
            {
                Logger.LogInfo($"No applicable replacements found for block: {originalName}");
            }
        
            // Try to use the transformed name, with conflict resolution
            string finalName = currentName;
        
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (existingBlockNames.Add(finalName))
                {
                    return finalName;
                }

                // Name conflict - try with suffix
                if (attempt == 0)
                {
                    finalName += "_1";
                }
                else
                {
                    // Remove previous suffix and add new one
                    int lastUnderscoreIndex = finalName.LastIndexOf('_');
                    if (lastUnderscoreIndex > 0)
                    {
                        string baseWithoutSuffix = finalName.Substring(0, lastUnderscoreIndex);
                        finalName = baseWithoutSuffix + "_" + (attempt + 1);
                    }
                    else
                    {
                        finalName = currentName + "_" + (attempt + 1);
                    }
                }
            }

            throw new Exception($"Failed to generate unique name for '{originalName}' - too many naming conflicts");
        }

        /// <summary>
        /// Builds block name mapping and loads file contents (with optional preloaded content)
        /// </summary>
        private static Dictionary<string, string> BuildBlockMappingAndLoadFiles(string exportDirectory,
                List<FindReplacePair> findReplacePairs,
                HashSet<string> existingBlockNames, 
                ref Dictionary<string, string> blockMapping)
        {
            var fileContents = new Dictionary<string, string>();
            
            Logger.LogInfo("Building block name mapping:");
            
            var xmlFiles = Directory.GetFiles(exportDirectory, "*.xml", SearchOption.AllDirectories);
            
            foreach (var xmlFile in xmlFiles)
            {
                try
                {
                    string content = File.ReadAllText(xmlFile, Encoding.UTF8);
                    fileContents[xmlFile] = content;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"  Error reading file {Path.GetFileName(xmlFile)}: {ex.Message}");
                }
            }
            Logger.LogInfo($"Loaded {fileContents.Count} XML files into memory");
    

            
            // Build block mappings from file contents
            foreach (var kvp in fileContents)
            {
                try
                {
                    string originalName = ExtractBlockNameFromContent(kvp.Value);
                    if (!string.IsNullOrEmpty(originalName))
                    {
                        string transformedName = ApplyBlockNameTransformations(originalName, findReplacePairs, existingBlockNames);
                        
                        if (!string.Equals(originalName, transformedName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!blockMapping.ContainsKey(originalName))
                            {
                                blockMapping[originalName] = transformedName;
                                Logger.LogInfo($"  {originalName} -> {transformedName}");
                            }
                            else
                            {
                                Logger.LogWarning($"    Warning: Duplicate mapping for block '{originalName}' - using first occurrence");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"  Error processing {Path.GetFileName(kvp.Key)}: {ex.Message}");
                }
            }
            
            Logger.LogInfo($"Total unique block mappings: {blockMapping.Count}\n");
            return fileContents;
        }

        /// <summary>
        /// Extracts the block name from XML content by finding the Name element
        /// </summary>
        private static string ExtractBlockNameFromContent(string xmlContent)
        {
            try
            {
                var nameMatch = Regex.Match(xmlContent, @"<Name>(.*?)</Name>", RegexOptions.Singleline);
                if (nameMatch.Success)
                {
                    return nameMatch.Groups[1].Value.Trim();
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Processes all files using unified approach for all transformations
        /// </summary>
        private static (int totalFiles, int processedFiles, int updatedFiles) ProcessAllFilesUnified(
            Dictionary<string, string> fileContents,
            int blockPrefixNumber,
            Dictionary<string, string> combinedMapping,
            List<FindReplacePair> contentFindReplacePairs)
        {
            
            var totalFiles = fileContents.Count;
            int updatedFiles = 0;
            int  processedFiles = 0;

            // Build regex pattern once for all files (performance optimization)
            Regex combinedRegex = null;
            if (combinedMapping.Count > 0)
            {
                var escapedNames = combinedMapping.Keys.Select(Regex.Escape).ToArray();
                string namesPattern = string.Join("|", escapedNames);
                
                // ReSharper disable once GrammarMistakeInComment
                // Create pattern that matches both Name="..." and Datatype="&quot;...&quot;" patterns
                string pattern = $"(?:Name=\"({namesPattern})\"|Datatype=\"&quot;({namesPattern})&quot;\")";
                combinedRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                Logger.LogInfo($"Compiled regex pattern for {combinedMapping.Count} name replacements (Name and Datatype patterns)");
            }

            foreach (var kvp in fileContents)
            {
                string filePath = kvp.Key;
                string originalContent = kvp.Value;

                try
                {
                    bool fileWasUpdated = ProcessSingleFileUnified(filePath, originalContent, blockPrefixNumber, combinedMapping, combinedRegex, contentFindReplacePairs);
                    processedFiles++;

                    if (fileWasUpdated)
                    {
                        updatedFiles++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"  ERROR processing {Path.GetFileName(filePath)}: {ex.Message}", false);
                }
            }
            
            return (totalFiles, processedFiles, updatedFiles);
        }

        /// <summary>
        /// Processes a single file with unified approach for all transformations
        /// </summary>
        private static bool ProcessSingleFileUnified(
            string xmlFilePath,
            string originalContent,
            int blockPrefixNumber,
            Dictionary<string, string> combinedMapping,
            Regex combinedRegex,
            List<FindReplacePair> contentFindReplacePairs)
        {
            try
            {
                string updatedContent = originalContent;
                bool contentChanged = false;

                // Step 1: Update block name and number in this file's content
                var (nameNumberUpdatedContent, nameNumberChanged) = 
                    UpdateBlockNamesAndNumbers(updatedContent, blockPrefixNumber, combinedMapping, Path.GetFileName(xmlFilePath));
                if (nameNumberChanged)
                {
                    updatedContent = nameNumberUpdatedContent;
                    contentChanged = true;
                }

                // Step 2: Update content (variables and comments) - exclude protected names
                if (contentFindReplacePairs is { Count: > 0 })
                {
                    var (contentUpdatedContent, contentContentChanged) =
                        UpdateBlockContent(updatedContent, contentFindReplacePairs, combinedMapping, Path.GetFileName(xmlFilePath));
                    if (contentContentChanged)
                    {
                        updatedContent = contentUpdatedContent;
                        contentChanged = true;
                    }
                }

                // Step 3: Normalize remanence for safety blocks (must be done before import)
                if (IsSafetyBlock(originalContent))
                {
                    var (safetyUpdatedContent, safetyChanged) = NormalizeSafetyBlockRemanence(updatedContent, Path.GetFileName(xmlFilePath));
                    if (safetyChanged)
                    {
                        updatedContent = safetyUpdatedContent;
                        contentChanged = true;
                    }
                }

                // Step 4: Update all references (tags and blocks) - skip DB files for references only
                if (!IsDbFile(originalContent))
                {
                    var (refUpdatedContent, refChanged) = UpdateAllReferences(updatedContent, combinedRegex, combinedMapping, Path.GetFileName(xmlFilePath));
                    if (refChanged)
                    {
                        updatedContent = refUpdatedContent;
                        contentChanged = true;
                    }
                }

                // Step 5: If content changed, validate and write back
                if (contentChanged)
                {
                    // Validate the updated XML is well-formed
                    if (ValidateXml(updatedContent))
                    {
                        File.WriteAllText(xmlFilePath, updatedContent, Encoding.UTF8);
                        Logger.LogInfo($"  Updated: {Path.GetFileName(xmlFilePath)}");
                    }
                    else
                    {
                        Logger.LogWarning($"  Skipped: {Path.GetFileName(xmlFilePath)} (XML validation failed after update)");
                        return false;
                    }
                }
                else
                {
                    Logger.LogInfo($"  - No changes: {Path.GetFileName(xmlFilePath)}");
                }

                return contentChanged;
            }
            catch (Exception ex)
            {
                Logger.LogError($"  ERROR: Failed to process {Path.GetFileName(xmlFilePath)}: {ex.Message}", false);
                return false;
            }
        }

        /// <summary>
        /// Updates block name and number elements in XML content
        /// </summary>
        private static (string updatedContent, bool wasChanged) UpdateBlockNamesAndNumbers(
            string xmlContent,
            int prefixNumber,
            Dictionary<string, string> blockMapping,
            string fileName)
        {
            string updatedContent = xmlContent;
            bool wasChanged = false;

            try
            {
                // Update block name
                var nameMatch = Regex.Match(updatedContent, @"<Name>(.*?)</Name>", RegexOptions.Singleline);
                if (nameMatch.Success)
                {
                    string originalName = nameMatch.Groups[1].Value.Trim();
                    
                    // Look up the already-calculated transformation from the combined mapping
                    if (blockMapping != null && blockMapping.TryGetValue(originalName, out string newName))
                    {
                        updatedContent = updatedContent.Replace($"<Name>{originalName}</Name>", $"<Name>{newName}</Name>");
                        wasChanged = true;
                        Logger.LogInfo($"    Name: {originalName} -> {newName}");
                    }
                }

                // Update block number
                var numberMatch = Regex.Match(updatedContent, @"<Number>(\d+)</Number>", RegexOptions.Singleline);
                if (numberMatch.Success)
                {
                    int currentNumber = int.Parse(numberMatch.Groups[1].Value);
                    
                    // Apply prefix logic: extract rightmost two digits and add prefix
                    int rightmostTwoDigits = currentNumber % 100;
                    int newNumber = (prefixNumber * 100) + rightmostTwoDigits;
                    
                    if (currentNumber != newNumber)
                    {
                        updatedContent = updatedContent.Replace($"<Number>{currentNumber}</Number>", $"<Number>{newNumber}</Number>");
                        wasChanged = true;
                        Logger.LogInfo($"    Number: {currentNumber} -> {newNumber}");
                    }
                }

                // Update InstanceOfName (for InstanceDB files)
                var instanceMatch = Regex.Match(updatedContent, @"<InstanceOfName>(.*?)</InstanceOfName>", RegexOptions.Singleline);
                if (instanceMatch.Success)
                {
                    string originalInstanceOf = instanceMatch.Groups[1].Value.Trim();
                    if (blockMapping != null && blockMapping.TryGetValue(originalInstanceOf, out var newInstanceOf))
                    {
                        updatedContent = updatedContent.Replace($"<InstanceOfName>{originalInstanceOf}</InstanceOfName>", 
                                                              $"<InstanceOfName>{newInstanceOf}</InstanceOfName>");
                        wasChanged = true;
                        Logger.LogInfo($"    InstanceOfName: {originalInstanceOf} -> {newInstanceOf}");
                    }
                }

                return (updatedContent, wasChanged);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"    Error updating name/number in {fileName}: {ex.Message}");
                return (xmlContent, false);
            }
        }

        /// <summary>
        /// Updates all references (tags and blocks) using pre-compiled regex pattern
        /// </summary>
        private static (string updatedContent, bool wasChanged) UpdateAllReferences(string xmlContent, Regex combinedRegex, Dictionary<string, string> combinedMapping, string fileName)
        {
            if (combinedMapping.Count == 0 || combinedRegex == null)
            {
                return (xmlContent, false);
            }

            int totalReplacements = 0;
            
            // Dictionary to track replacements per name for logging
            var replacementCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in combinedMapping.Keys)
            {
                replacementCounts[key] = 0;
            }
            
            // Perform all replacements in a single pass using a match evaluator
            string updatedContent = combinedRegex.Replace(xmlContent, match =>
            {
                // Check which capture group matched (Group 1 = Name pattern, Group 2 = Datatype pattern)
                string matchedName = null;
                bool isNamePattern = false;
                
                if (!string.IsNullOrEmpty(match.Groups[1].Value))
                {
                    matchedName = match.Groups[1].Value; // Name="..." pattern
                    isNamePattern = true;
                }
                else if (!string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    matchedName = match.Groups[2].Value; // Datatype="&quot;...&quot;" pattern
                }
                
                if (matchedName != null && combinedMapping.TryGetValue(matchedName, out var replacement))
                {
                    totalReplacements++;
                    replacementCounts[matchedName]++;
                    
                    // Return appropriate replacement format based on which pattern matched
                    if (isNamePattern)
                    {
                        return $"Name=\"{replacement}\"";
                    }

                    return $"Datatype=\"&quot;{replacement}&quot;\"";
                }
                
                return match.Value; // No replacement found, return original
            });
            
            // Log replacement details
            foreach (var kvp in replacementCounts.Where(kvp => kvp.Value > 0))
            {
                string newName = combinedMapping[kvp.Key];
                Logger.LogInfo($"    {kvp.Key} -> {newName} ({kvp.Value} references)");
            }
            
            if (totalReplacements > 0)
            {
                Logger.LogInfo($"    Total reference replacements in {fileName}: {totalReplacements}");
            }
            
            return (updatedContent, totalReplacements > 0);
        }


        /// <summary>
        /// Checks if the given XML content represents a DB (InstanceDB or GlobalDB) block
        /// </summary>
        /// <param name="xmlContent">Pre-loaded XML content</param>
        /// <returns>True if the content is a DB file, false otherwise</returns>
        private static bool IsDbFile(string xmlContent)
        {
            return xmlContent.Contains("SW.Blocks.InstanceDB") || xmlContent.Contains("SW.Blocks.GlobalDB");
        }

        /// <summary>
        /// Checks if the given XML content represents a safety block (F_FBD, F_LAD, F_STL, etc.)
        /// </summary>
        /// <param name="xmlContent">Pre-loaded XML content</param>
        /// <returns>True if the content is a safety block, false otherwise</returns>
        private static bool IsSafetyBlock(string xmlContent)
        {
            try
            {
                var langMatch = Regex.Match(xmlContent, @"<ProgrammingLanguage>(.*?)</ProgrammingLanguage>", RegexOptions.Singleline);
                if (langMatch.Success)
                {
                    string language = langMatch.Groups[1].Value.Trim();
                    return language.StartsWith("F_", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Normalizes remanence attributes in safety blocks by replacing all values with NonRetain
        /// </summary>
        /// <param name="xmlContent">XML content of a safety block</param>
        /// <param name="fileName">File name for logging purposes</param>
        /// <returns>Tuple of updated content and whether changes were made</returns>
        private static (string updatedContent, bool wasChanged) NormalizeSafetyBlockRemanence(string xmlContent, string fileName)
        {
            try
            {
                int replacementCount = 0;

                // Pattern to match Remanence="..." attributes in Member elements
                // Captures the remanence value so we can log what was changed
                string pattern = @"(<Member[^>]*)\s+Remanence=""([^""]+)""";

                string updatedContent = Regex.Replace(xmlContent, pattern, match =>
                {
                    string memberStart = match.Groups[1].Value;
                    string remanenceValue = match.Groups[2].Value;

                    // Only replace if it's not already NonRetain
                    if (!string.Equals(remanenceValue, "NonRetain", StringComparison.OrdinalIgnoreCase))
                    {
                        replacementCount++;
                        return $"{memberStart} Remanence=\"NonRetain\"";
                    }

                    return match.Value; // Already NonRetain, no change needed
                }, RegexOptions.IgnoreCase);

                if (replacementCount > 0)
                {
                    Logger.LogInfo($"    Safety block: Normalized {replacementCount} remanence attribute(s) to NonRetain");
                }

                return (updatedContent, replacementCount > 0);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"    Error normalizing remanence in {fileName}: {ex.Message}");
                return (xmlContent, false);
            }
        }

        /// <summary>
        /// Updates block content (variables and comments) using sequential find/replace pairs,
        /// while excluding any names that exist in the combined mapping (protected names)
        /// </summary>
        private static (string updatedContent, bool wasChanged) UpdateBlockContent(
            string xmlContent,
            List<FindReplacePair> contentFindReplacePairs,
            Dictionary<string, string> combinedMapping,
            string fileName)
        {
            string updatedContent = xmlContent;
            bool wasChanged = false;
            int totalReplacements = 0;

            try
            {
                // Apply content find/replace pairs sequentially (like block name processing)
                foreach (var pair in contentFindReplacePairs)
                {
                    if (string.IsNullOrEmpty(pair.FindString)) continue;

                    int replacementsThisPair = 0;

                    // Process Name="..." attributes (variables) - separate regex for simplicity
                    var namePattern = @"Name=""([^""]*)""";
                    updatedContent = Regex.Replace(updatedContent, namePattern, match =>
                    {
                        string fullName = match.Groups[1].Value;
                        
                        // Skip if this name is protected (exists in combined mapping)
                        if (combinedMapping.ContainsKey(fullName)) return match.Value;
                        
                        // Apply find/replace if the name contains the find string
                        if (fullName.Contains(pair.FindString))
                        {
                            string newName = fullName.Replace(pair.FindString, pair.ReplaceString);
                            replacementsThisPair++;
                            return $"Name=\"{newName}\"";
                        }
                        
                        return match.Value;
                    }, RegexOptions.IgnoreCase);

                    // Process <Text>...</Text> elements (comments) - separate regex for simplicity
                    var textPattern = @"<Text>([^<]*)</Text>";
                    updatedContent = Regex.Replace(updatedContent, textPattern, match =>
                    {
                        string textContent = match.Groups[1].Value;
                        
                        // Apply find/replace if the text contains the find string
                        if (textContent.Contains(pair.FindString))
                        {
                            string newText = textContent.Replace(pair.FindString, pair.ReplaceString);
                            replacementsThisPair++;
                            return $"<Text>{newText}</Text>";
                        }
                        
                        return match.Value;
                    }, RegexOptions.IgnoreCase);

                    if (replacementsThisPair > 0)
                    {
                        Logger.LogInfo($"    Content: '{pair.FindString}' -> '{pair.ReplaceString}' ({replacementsThisPair} occurrences)");
                        totalReplacements += replacementsThisPair;
                        wasChanged = true;
                    }
                }

                if (totalReplacements > 0)
                {
                    Logger.LogInfo($"    Total content replacements in {fileName}: {totalReplacements}");
                }

                return (updatedContent, wasChanged);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"    Error updating content in {fileName}: {ex.Message}");
                return (xmlContent, false);
            }
        }

    }
}
