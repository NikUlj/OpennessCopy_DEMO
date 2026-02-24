#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Forms.Conveyor;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms;

public partial class MainForm
{
    private PLCInfo? RequestConveyorPlcSelection(IReadOnlyList<PLCInfo> plcInfos)
    {
        if (InvokeRequired)
        {
            return (PLCInfo?)Invoke(new Func<PLCInfo?>(() => RequestConveyorPlcSelection(plcInfos)));
        }

        using var dialog = new SimplePLCSelectionForm(plcInfos.ToList());
        var result = dialog.ShowDialog(this) == DialogResult.OK
            ? dialog.SelectedPlc
            : null;

        if (dialog.DialogResult == DialogResult.Cancel && _currentWorkflow != null)
        {
            _currentWorkflow.Cancel();
        }

        return result;
    }

    private ConveyorConfig? RequestConveyorConfiguration(Func<PlcBlockInfo?>? blockSelector = null)
    {
        if (InvokeRequired)
        {
            return (ConveyorConfig?)Invoke(new Func<ConveyorConfig?>(() => RequestConveyorConfiguration(blockSelector)));
        }

        using var dialog = new ConveyorConfigForm();
        if (blockSelector != null)
        {
            dialog.RequestBlockSelection = () =>
            {
                var selected = blockSelector();
                dialog.SetSelectedDb(selected);
            };
        }

        var result = dialog.ShowDialog(this) == DialogResult.OK
            ? dialog.Result
            : null;

        if (dialog.DialogResult == DialogResult.Cancel && _currentWorkflow != null)
        {
            _currentWorkflow.Cancel();
        }

        return result;
    }

    private PlcBlockInfo? RequestConveyorBlockSelection(IReadOnlyList<PlcBlockInfo> blocks)
    {
        if (InvokeRequired)
        {
            return (PlcBlockInfo?)Invoke(new Func<PlcBlockInfo?>(() => RequestConveyorBlockSelection(blocks)));
        }

        var allowedKinds = new HashSet<PlcBlockKind>
        {
            PlcBlockKind.GlobalDb,
            PlcBlockKind.ArrayDb
        };

        using var dialog = new BlockSelectionForm(blocks.ToList(), allowedKinds);
        var result = dialog.ShowDialog(this) == DialogResult.OK
            ? dialog.SelectedBlock
            : null;

        if (dialog.DialogResult == DialogResult.Cancel && _currentWorkflow != null)
        {
            _currentWorkflow.Cancel();
        }

        return result;
    }

    private void OnConveyorWorkflowCompleted()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(OnConveyorWorkflowCompleted));
            return;
        }

        Logger.LogInfo("Conveyor workflow completed.");
        ResetUIState();
    }
}
