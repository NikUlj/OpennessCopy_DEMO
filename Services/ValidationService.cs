using System;
using System.Security;
using System.Xml;
using OpennessCopy.Utils;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Safety;

namespace OpennessCopy.Services;

public static class ValidationService
{
    /// <summary>
    /// Compiles a TIA Portal Openness object and retries after safety login when required.
    /// Supports: Device, DeviceItem, CodeBlock, DataBlock, HmiTarget, PlcSoftware, PlcType, PlcBlockSystemGroup, PlcBlockUserGroup, PlcTypeSystemGroup, PlcTypeUserGroup
    /// </summary>
    public static void CompileSafe<T>(T compilableObject, SafetyAdministration safetyAdministration = null, SecureString safetyPassword = null) where T : class
    {
        string objectType = typeof(T).Name;
        Logger.LogInfo($"Starting {objectType} compilation...");
        
        // Try to get the compilation service from the object using dynamic to avoid generic parameter issues
        ICompilable compileService;
        try
        {
            dynamic dynamicObject = compilableObject;
            compileService = dynamicObject.GetService<ICompilable>();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get compilation service from {objectType}: {ex.Message}");
        }
        
        if (compileService == null)
        {
            throw new Exception($"Compilation service not available for {objectType}");
        }
        
        CompilerResult result;
        bool retryWithSafetyLogin = false;
        
        try
        {
            result = compileService.Compile();
        }
        catch (Exception ex) when (IsSafetyPermissionError(ex))
        {
            Logger.LogInfo($"Safety permission error detected: {ex.Message}");
            retryWithSafetyLogin = true;
            result = null;
        }
        
        // If we got a safety permission error, try to handle it
        if (retryWithSafetyLogin)
        {
            if (safetyAdministration != null)
            {
                if (safetyPassword != null)
                {
                    Logger.LogInfo("Attempting safety login with provided password...");
                    try 
                    {
                        // Use the pre-captured password for safety login
                        safetyAdministration.LoginToSafetyOfflineProgram(safetyPassword);
                        Logger.LogInfo("Safety login successful, retrying compilation...");
                        result = compileService.Compile();
                    }
                    catch (Exception loginEx)
                    {
                        throw new Exception($"Cannot compile: Safety login failed with provided password: {loginEx.Message}");
                    }
                }
                else
                {
                    throw new Exception("Safety login failed - password provided is null");
                }
            }
            else
            {
                throw new Exception("Safety login failed - safety administration not available");
            }
        }
        
        if (result == null)
        {
            throw new Exception("Compilation result is null - unexpected error");
        }
        
        Logger.LogInfo($"Compilation State: {result.State}");
        Logger.LogInfo($"Warning Count: {result.WarningCount}");
        Logger.LogInfo($"Error Count: {result.ErrorCount}");
        
        if (result.State == CompilerResultState.Success)
        {
            Logger.LogInfo("Compilation completed successfully!");
        }
        else if (result.State == CompilerResultState.Warning)
        {
            Logger.LogInfo("Compilation completed with warnings");
        }
        else if (result.State == CompilerResultState.Error)
        {
            Logger.LogError("Compilation failed with errors!", false);
        }

        if (result.State == CompilerResultState.Error)
        {
            throw new Exception("Compilation failed with errors");
        }

        Logger.LogInfo($"{objectType} compiled successfully!");
    }

    /// <summary>
    /// Validates that the XML content is well-formed
    /// </summary>
    public static bool ValidateXml(string xmlContent)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if an exception is related to safety program permission issues
    /// </summary>
    private static bool IsSafetyPermissionError(Exception ex)
    {
        if (ex == null) return false;
        
        string message = ex.Message?.ToLower() ?? "";
        
        return message.Contains("permission to modify the safety program is missing") ||
               message.Contains("safety program") ||
               message.Contains("f-cpu") ||
               message.Contains("fail-safe") ||
               message.Contains("safety") && (message.Contains("access") || message.Contains("permission"));
    }
}
