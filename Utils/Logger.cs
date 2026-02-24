using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace OpennessCopy.Utils
{
    public static class Logger
    {
        // Debug flag - set to true to enable file logging
        public static bool DebugTxtOn { get; set; }
        
        private static Action<string, Color> _logMessageCallback;
        private static Control _uiControl;
        private static bool _initialized;
        
        // Thread-safe queue for non-blocking logging
        private static readonly ConcurrentQueue<(string message, Color color)> MessageQueue = new ConcurrentQueue<(string, Color)>();
        private static volatile bool _useQueue;
        
        // Debug file logging - next to exe
        private static readonly string DebugFilePath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", 
            "debug.txt");
        private static readonly object DebugFileLock = new object();

        public static void Initialize(Action<string, Color> logMessageCallback, Control uiControl)
        {
            _logMessageCallback = logMessageCallback;
            _uiControl = uiControl;
            _initialized = true;
            
            // Clear debug file at start if debug mode is enabled
        }

        /// <summary>
        /// Enable queued logging mode - used when UI thread is blocked
        /// </summary>
        public static void EnableQueuedLogging()
        {
            _useQueue = true;
        }

        /// <summary>
        /// Disable queued logging mode - return to immediate display
        /// </summary>
        public static void DisableQueuedLogging()
        {
            _useQueue = false;
        }

        /// <summary>
        /// Flush all queued messages to the UI
        /// </summary>
        public static void FlushQueuedMessages()
        {
            while (MessageQueue.TryDequeue(out var queuedMessage))
            {
                // Display the queued message immediately (bypass queue)
                LogMessageDirect(queuedMessage.message, queuedMessage.color);
            }
        }

        public static void LogInfo(string message, bool showMessageBox = false)
        {
            LogMessage(message, Color.White);
            if (showMessageBox)
            {
                ShowMessageBoxSafe(message, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static void LogWarning(string message, bool showMessageBox = false)
        {
            LogMessage($"WARNING: {message}", Color.Yellow);
            if (showMessageBox)
            {
                ShowMessageBoxSafe(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static void LogError(string message, bool showMessageBox = true)
        {
            LogMessage($"ERROR: {message}", Color.Red);
            if (showMessageBox)
            {
                ShowMessageBoxSafe(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void LogSuccess(string message, bool showMessageBox = false)
        {
            LogMessage(message, Color.LightGreen);
            if (showMessageBox)
            {
                ShowMessageBoxSafe(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static void ClearDebugFile()
        {
            if (!DebugTxtOn) return;
            
            try
            {
                lock (DebugFileLock)
                {
                    if (File.Exists(DebugFilePath))
                    {
                        File.Delete(DebugFilePath);
                    }
                }
            }
            catch
            {
                // Ignore any clearing errors
            }
        }

        public static void WriteToDebugFile(string timestampedMessage)
        {
            if (!DebugTxtOn) return;
            
            try
            {
                lock (DebugFileLock)
                {
                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    var logEntry = $"[Thread {threadId}] {timestampedMessage}";
                    
                    File.AppendAllText(DebugFilePath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore any logging errors to prevent recursive issues
            }
        }

        private static void LogMessage(string message, Color color)
        {
            if (!_initialized)
            {
                // Fallback to console if logger not initialized
                Console.WriteLine(message);
                return;
            }

            // Calculate timestamp on the calling thread (STA thread) for accurate timing
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var timestampedMessage = $"[{timestamp}] {message}";

            WriteToDebugFile(timestampedMessage);

            // If queuing is enabled (UI thread blocked), queue the message
            if (_useQueue)
            {
                MessageQueue.Enqueue((timestampedMessage, color));
                return;
            }

            // Otherwise display immediately
            LogMessageDirect(timestampedMessage, color);
        }

        private static void LogMessageDirect(string timestampedMessage, Color color)
        {
            // Thread-safe callback invocation
            if (_uiControl is { InvokeRequired: true })
            {
                try
                {
                    _uiControl.Invoke(new Action(() => _logMessageCallback?.Invoke(timestampedMessage, color)));
                }
                catch (Exception ex)
                {
                    // Fallback to console if UI invoke fails
                    Console.WriteLine($"Logger UI invoke failed: {ex.Message}");
                    Console.WriteLine($"Original message: {timestampedMessage}");
                }
            }
            else
            {
                _logMessageCallback?.Invoke(timestampedMessage, color);
            }
        }

        private static void ShowMessageBoxSafe(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (_uiControl is { InvokeRequired: true })
            {
                try
                {
                    _uiControl.Invoke(new Action(() => MessageBox.Show(message, caption, buttons, icon)));
                }
                catch (Exception ex)
                {
                    // Fallback to console if UI invoke fails
                    Console.WriteLine($"MessageBox invoke failed: {ex.Message}");
                    Console.WriteLine($"MessageBox content: {caption}: {message}");
                }
            }
            else
            {
                MessageBox.Show(message, caption, buttons, icon);
            }
        }
    }
}