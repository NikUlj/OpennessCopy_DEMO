using System;

namespace OpennessCopy.Models
{
    public class WorkflowProgress
    {
        public string Status { get; set; }
        public int PercentComplete { get; set; }
        public WorkflowProgressType Type { get; set; }
        public Exception Exception { get; set; }

        public WorkflowProgress(string status, int percentComplete = 0, WorkflowProgressType type = WorkflowProgressType.Info)
        {
            Status = status;
            PercentComplete = Math.Max(0, Math.Min(100, percentComplete));
            Type = type;
        }

        private WorkflowProgress(Exception exception, string status = "Error occurred")
        {
            Status = status;
            PercentComplete = 0;
            Type = WorkflowProgressType.Error;
            Exception = exception;
        }

        public static WorkflowProgress Success(string status, int percentComplete = 100)
        {
            return new WorkflowProgress(status, percentComplete, WorkflowProgressType.Success);
        }

        public static WorkflowProgress Warning(string status, int percentComplete = 0)
        {
            return new WorkflowProgress(status, percentComplete, WorkflowProgressType.Warning);
        }

        public static WorkflowProgress Error(string status, Exception exception = null)
        {
            return new WorkflowProgress(exception, status);
        }

        public static WorkflowProgress Cancelled(string status = "Workflow cancelled", int percentComplete = 0)
        {
            return new WorkflowProgress(status, percentComplete, WorkflowProgressType.Cancelled);
        }
    }

    public enum WorkflowProgressType
    {
        Info,
        Success,
        Warning,
        Error,
        Cancelled,
        CleanupComplete
    }
}