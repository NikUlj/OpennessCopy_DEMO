using System;
using System.Collections.Generic;
using OpennessCopy.Utils;
using Siemens.Engineering;

namespace OpennessCopy.Services;

public static class PlcManagementService
{
    private static List<TiaPortal> ConnectToAllTiaPortalInstances()
    {
        try
        {
            var processes = TiaPortal.GetProcesses();
            var connectedInstances = new List<TiaPortal>();
            
            if (processes.Count == 0)
            {
                Logger.LogError("No running TIA Portal found! Please start TIA Portal with a project.");
                return connectedInstances;
            }
            
            Logger.LogInfo($"Found {processes.Count} TIA Portal instance(s)");
            
            foreach (var process in processes)
            {
                try
                {
                    var tiaPortalRef = process.Attach();
                    connectedInstances.Add(tiaPortalRef);
                    Logger.LogInfo($"Connected to TIA Portal instance (Process ID: {process.Id})");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to connect to TIA Portal instance (Process ID: {process.Id}): {ex.Message}");
                }
            }
            
            if (connectedInstances.Count == 0)
            {
                Logger.LogError("Failed to connect to any TIA Portal instances.");
            }
            
            return connectedInstances;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error connecting to TIA Portal instances: {ex.Message}", false);
            throw;
        }
    }

    public static List<(TiaPortal portal, Project project)> GetAllProjectsFromAllInstances()
    {
        var tiaPortalInstances = ConnectToAllTiaPortalInstances();
        var projectList = new List<(TiaPortal portal, Project project)>();
        
        foreach (var portal in tiaPortalInstances)
        {
            try
            {
                if (portal.Projects.Count > 0)
                {
                    // TODO: Maybe add support for multiple projects per instance
                    projectList.Add((portal, portal.Projects[0]));
                    Logger.LogInfo($"Found project '{portal.Projects[0].Name}' in TIA Portal instance");
                }
                else
                {
                    Logger.LogWarning("TIA Portal instance has no open projects");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error accessing projects from TIA Portal instance: {ex.Message}");
            }
        }
        
        return projectList;
    }

    public static Project GetProjectFromTiaPortal(TiaPortal tiaPortal)
    {
        try
        {
            if (tiaPortal.Projects.Count == 0)
            {
                Logger.LogError("No project is open in TIA Portal!");
                return null;
            }
                
            return tiaPortal.Projects[0];
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error accessing TIA Portal project: {ex.Message}", false);
            throw;
        }
    }
}