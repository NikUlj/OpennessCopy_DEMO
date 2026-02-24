namespace OpennessCopy.Services;

/// <summary>
/// Common interface for both PLC and hardware workflow threads
/// Provides standardized control methods for workflow management
/// </summary>
public interface IWorkflowThread
{
    /// <summary>
    /// Starts the workflow thread execution
    /// </summary>
    void Start();

    /// <summary>
    /// Cancels the currently running workflow
    /// </summary>
    void Cancel();

    /// <summary>
    /// Requests cleanup of workflow resources
    /// </summary>
    void RequestCleanup();

    /// <summary>
    /// Waits for the workflow thread to complete within the specified timeout
    /// </summary>
    /// <param name="timeout">Maximum time to wait for completion</param>
    void Join(System.TimeSpan timeout);
}