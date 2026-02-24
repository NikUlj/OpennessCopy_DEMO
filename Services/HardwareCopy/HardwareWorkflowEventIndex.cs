namespace OpennessCopy.Services.HardwareCopy
{
    /// <summary>
    /// Enum for WaitAny event indices in the hardware device copy workflow event loop
    /// </summary>
    internal enum HardwareWorkflowEventIndex
    {
        /// <summary>
        /// User made selections and is ready to proceed with workflow execution
        /// </summary>
        UserSelection = 0,

        /// <summary>
        /// Cleanup requested - workflow should exit
        /// </summary>
        CleanupRequested = 1,

        /// <summary>
        /// Device extraction (lightweight or full) requested from UI thread
        /// </summary>
        DeviceExtraction = 2,

        /// <summary>
        /// Device enrichment with full details requested from UI thread
        /// </summary>
        DeviceEnrichment = 3,

        /// <summary>
        /// Safety password validation requested from UI thread
        /// </summary>
        PasswordValidation = 4,

        /// <summary>
        /// PLC safety data extraction from IoSystem requested from UI thread
        /// </summary>
        PlcSafetyFromIoSystem = 5,

        /// <summary>
        /// Cancellation token signaled - workflow should exit
        /// </summary>
        Cancellation = 6,

        /// <summary>
        /// Archive loading requested from background task
        /// </summary>
        ArchiveLoad = 7,

        /// <summary>
        /// IoSystem extraction from project requested from UI thread
        /// </summary>
        IoSystemExtraction = 8
    }
}
