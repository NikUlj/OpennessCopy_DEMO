using System;
using System.Collections.Generic;
using System.Threading;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering.HW;
using Siemens.Engineering.Safety;

namespace OpennessCopy.Services;

public static class SecurityManagementService
{
    /// <summary>
    /// Generate common password candidates based on project name
    /// DISCLAIMER: Removed for demo build
    /// </summary>
    public static List<string> GeneratePasswordCandidates(string projectName)
    {
        var candidates = new List<string>();

        // Removed for demo build

        return candidates;
    }

    /// <summary>
    /// Find safety administration specifically for a given device
    /// </summary>
    public static SafetyAdministration FindSafetyAdministrationForDevice(Device device)
    {
        try
        {
            foreach (DeviceItem deviceItem in device.DeviceItems)
            {
                if (deviceItem.Classification == DeviceItemClassifications.CPU)
                {
                    var safetyAdmin = deviceItem.GetService<SafetyAdministration>();
                    if (safetyAdmin != null)
                    {
                        Logger.LogInfo($"Found safety administration on device: {device.Name}");
                        return safetyAdmin;
                    }
                }
            }
            
            Logger.LogInfo($"No safety administration found for device: {device.Name}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error finding safety administration for device {device.Name}: {ex.Message}",false);
            return null;
        }
    }
    
    public static SafetyPasswordData ExtractSafetyPasswordData(string deviceId)
    {
        var device = DataCacheUtility.GetCachedObject<Device>(deviceId);
        if (device == null) return null;

        var safetyAdmin = FindSafetyAdministrationForDevice(device);
        if (safetyAdmin == null) return null;

        return new SafetyPasswordData
        {
            DeviceName = device.Name,
            SafetyAdmin = new SafetyAdminInfo
            {
                IsSafetyOfflineProgramPasswordSet = safetyAdmin.IsSafetyOfflineProgramPasswordSet
            }
        };
    }
    
    public static bool ValidateSafetyPassword(string deviceId, string password, bool isAutoCheck)
    {
        var currentThreadId = Thread.CurrentThread.ManagedThreadId;
        var validationStartTime = DateTime.Now;
        Logger.LogInfo($"[THREAD {currentThreadId}] ValidateSafetyPasswordOnSTAThread started for device {deviceId} at {validationStartTime:HH:mm:ss.fff}");
        
        System.Security.SecureString securePassword = null;
        try
        {
            Logger.LogInfo($"[THREAD {currentThreadId}] Looking up cached device object...");
            var device = DataCacheUtility.GetCachedObject<Device>(deviceId);
            if (device == null)
            {
                Logger.LogError($"[THREAD {currentThreadId}] Device not found in cache for safety password validation", false);
                return false;
            }

            Logger.LogInfo($"[THREAD {currentThreadId}] Found device: {device.Name}");
            Logger.LogInfo($"[THREAD {currentThreadId}] Finding safety administration for device...");
            var safetyAdmin = FindSafetyAdministrationForDevice(device);
            if (safetyAdmin == null)
            {
                Logger.LogInfo($"[THREAD {currentThreadId}] No safety administration found for device {device.Name} - no password required");
                return true; // No safety admin = no password required
            }

            Logger.LogInfo($"[THREAD {currentThreadId}] Safety administration found, checking if password is set...");
            if (!safetyAdmin.IsSafetyOfflineProgramPasswordSet)
            {
                Logger.LogInfo($"[THREAD {currentThreadId}] No safety password is set for device {device.Name} - no password required");
                return true; // No password set = no password required
            }

            Logger.LogInfo($"[THREAD {currentThreadId}] Safety password is required, converting to SecureString...");
            // Convert to SecureString
            securePassword = new System.Security.SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            securePassword.MakeReadOnly();

            // Try to authenticate
            Logger.LogInfo($"[THREAD {currentThreadId}] Validating safety password for device {device.Name}...");
            var authStartTime = DateTime.Now;
            
            safetyAdmin.LoginToSafetyOfflineProgram(securePassword);
            
            var authDuration = DateTime.Now - authStartTime;
            Logger.LogInfo($"[THREAD {currentThreadId}] Safety password validation successful in {authDuration.TotalMilliseconds:F0}ms");
            return true;
        }
        catch (Exception ex)
        {
            // Check if this is a wrong password error specifically
            if (ex.Message.Contains("Wrong") || ex.Message.Contains("Invalid") || ex.Message.Contains("password"))
            {
                if (isAutoCheck) return false;
                Logger.LogWarning($"[THREAD {currentThreadId}] Wrong password provided");
            }
            
            var validationDuration = DateTime.Now - validationStartTime;
            // Log the specific error for debugging
            Logger.LogError($"[THREAD {currentThreadId}] Safety password validation failed after {validationDuration.TotalMilliseconds:F0}ms: {ex.Message}", false);
            
            return false;
        }
        finally
        {
            // Properly dispose of SecureString
            securePassword?.Dispose();
            var totalDuration = DateTime.Now - validationStartTime;
            Logger.LogInfo($"[THREAD {currentThreadId}] ValidateSafetyPasswordOnSTAThread completed in {totalDuration.TotalMilliseconds:F0}ms");
        }
    }
}