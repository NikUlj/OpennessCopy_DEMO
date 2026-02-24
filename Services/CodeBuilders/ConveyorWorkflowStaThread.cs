#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpennessCopy.Models;
using OpennessCopy.Services.CodeBuilders.TagTables;
using OpennessCopy.Services.BlockSelection;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;

namespace OpennessCopy.Services.CodeBuilders
{
    public sealed class ConveyorWorkflowStaThread(
        string exportRoot,
        string templateRoot,
        TiaPortalVersion tiaVersion,
        Func<IReadOnlyList<PLCInfo>, PLCInfo?>? plcSelectionCallback,
        Func<IReadOnlyList<PlcBlockInfo>, PlcBlockInfo?>? blockSelectionCallback,
        Func<Func<PlcBlockInfo?>?, ConveyorConfig?>? configCallback,
        Action<string> infoLogger,
        Action<string> errorLogger,
        Action onCompleted)
        : IWorkflowThread
    {
        private Thread? _thread;
        private readonly CancellationTokenSource _cts = new();
        private WorkflowResourceManager? _resourceManager;
        private StaTaskQueue? _taskQueue;

        public void Start()
        {
            if (_thread != null)
            {
                throw new InvalidOperationException("Workflow already started.");
            }

            _taskQueue = new StaTaskQueue();
            _taskQueue.TryAdd(() => ExecuteWorkflow(_cts.Token));

            _thread = new Thread(ProcessStaQueue)
            {
                Name = "ConveyorWorkflowSTAThread",
                IsBackground = true
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public void Cancel()
        {
            _cts.Cancel();
            _taskQueue?.Complete();
        }

        public void RequestCleanup()
        {
            _cts.Cancel();
            _taskQueue?.Complete();
        }

        public void Join(TimeSpan timeout)
        {
            _thread?.Join(timeout);
        }

        private void ProcessStaQueue()
        {
            try
            {
                if (!DependencyManagementService.VerifyDependencies(tiaVersion))
                {
                    errorLogger("Conveyor workflow cancelled due to missing Siemens dependencies.");
                    return;
                }

                while (!_cts.IsCancellationRequested &&
                       _taskQueue != null &&
                       _taskQueue.TryTake(out var workItem, _cts.Token))
                {
                    try
                    {
                        workItem?.Invoke();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        errorLogger($"Conveyor workflow failed: {ex.Message}");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                infoLogger("Conveyor workflow cancelled.");
            }
            catch (Exception ex)
            {
                errorLogger($"Conveyor workflow failed: {ex.Message}");
            }
            finally
            {
                _taskQueue?.Complete();
                _taskQueue?.Dispose();
                _resourceManager?.Dispose();
                onCompleted();
            }
        }

        private void ExecuteWorkflow(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            _resourceManager = new WorkflowResourceManager(exportRoot);

            var tiaProjects = PlcManagementService.GetAllProjectsFromAllInstances();
            if (tiaProjects.Count == 0)
            {
                errorLogger("No running TIA Portal instances found.");
                return;
            }

            _resourceManager.AllProjects = tiaProjects;

            var plcInfos = ExtractPlcInfos(tiaProjects);
            if (plcInfos.Count == 0)
            {
                errorLogger("No PLCs discovered in connected TIA Portal projects.");
                return;
            }

            var selectedPlcInfo = RequestTargetPlc(plcInfos);
            if (selectedPlcInfo == null)
            {
                infoLogger("PLC selection cancelled. Conveyor workflow aborted.");
                return;
            }

            var targetPlc = DataCacheUtility.GetCachedObject<PlcSoftware>(selectedPlcInfo.PlcId);
            if (targetPlc == null)
            {
                errorLogger("Selected PLC is no longer available. Please retry.");
                return;
            }

            var dbBlocks = PlcBlockEnumerationService.EnumerateBlocks(
                targetPlc,
                [PlcBlockKind.GlobalDb, PlcBlockKind.ArrayDb],
                includeInstanceDbs: false);

            var conveyorConfig = RequestConveyorConfig(BlockSelector);
            if (conveyorConfig == null)
            {
                infoLogger("Conveyor configuration cancelled. Conveyor workflow aborted.");
                return;
            }
            infoLogger("Conveyor configuration captured; applying DB settings from per-DB configuration entries.");

            Directory.CreateDirectory(exportRoot);
            var basePath = Path.Combine(exportRoot, "Conveyor");
            Directory.CreateDirectory(basePath);

            const string baseArtifactPath = "Conveyor";
            List<GeneratedArtifact> artifacts = null;
            // Removed for demo build
            //var artifacts = ConveyorBuilder.BuildArtifacts(conveyorConfig, templateRoot, baseArtifactPath);
            //artifacts.AddRange(ConveyorDbProcessor.Process(targetPlc, conveyorConfig, exportRoot, baseArtifactPath, infoLogger, errorLogger));

            GeneratedArtifactImportService.WriteFilesAndImport(
                targetPlc,
                exportRoot,
                artifacts,
                infoLogger,
                errorLogger,
                token);

            IEnumerable<PlcTagTablePlan> tagTablePlans = null;
            // Removed for demo build
            //var tagTablePlans = ConveyorBuilder.BuildTagTablePlans(conveyorConfig);
            TagTableGenerationService.ApplyPlans(targetPlc, tagTablePlans, token, infoLogger, errorLogger);
            
            infoLogger($"Conveyor artifacts written to {basePath} and imported into PLC '{targetPlc.Name}'.");

            _taskQueue?.Complete();
            return;

            PlcBlockInfo? BlockSelector()
            {
                var picked = RequestBlockSelection(dbBlocks);
                return picked;
            }
        }

        private PLCInfo? RequestTargetPlc(IReadOnlyList<PLCInfo> plcInfos)
        {
            try
            {
                return plcSelectionCallback != null ? plcSelectionCallback(plcInfos) : plcInfos.FirstOrDefault();
            }
            catch (Exception ex)
            {
                errorLogger($"Failed to request PLC selection: {ex.Message}");
                return null;
            }
        }

        private static List<PLCInfo> ExtractPlcInfos(IReadOnlyList<(TiaPortal portal, Project project)> allProjects)
        {
            var result = new List<PLCInfo>();
            for (var index = 0; index < allProjects.Count; index++)
            {
                var (_, project) = allProjects[index];
                var instanceId = $"instance_{index}";

                foreach (Device device in project.Devices)
                {
                    foreach (DeviceItem deviceItem in device.DeviceItems)
                    {
                        var softwareContainer = deviceItem.GetService<SoftwareContainer>();
                        if (softwareContainer?.Software is not PlcSoftware plcSoftware)
                        {
                            continue;
                        }

                        var plcId = DataCacheUtility.CacheObject(plcSoftware);
                        var deviceId = DataCacheUtility.CacheObject(device);

                        result.Add(new PLCInfo
                        {
                            Name = plcSoftware.Name,
                            DeviceName = device.Name,
                            PlcId = plcId,
                            DeviceId = deviceId,
                            TiaInstanceId = instanceId,
                            ProjectName = project.Name,
                            IsArchive = false
                        });
                    }
                }
            }

            return result;
        }

        private ConveyorConfig? RequestConveyorConfig(Func<PlcBlockInfo?>? blockSelector)
        {
            try
            {
                return configCallback?.Invoke(blockSelector);
            }
            catch (Exception ex)
            {
                errorLogger($"Failed to request conveyor configuration: {ex.Message}");
                return null;
            }
        }

        private PlcBlockInfo? RequestBlockSelection(IReadOnlyList<PlcBlockInfo> blocks)
        {
            try
            {
                return blockSelectionCallback?.Invoke(blocks);
            }
            catch (Exception ex)
            {
                errorLogger($"Failed to request block selection: {ex.Message}");
                return null;
            }
        }
    }
}
