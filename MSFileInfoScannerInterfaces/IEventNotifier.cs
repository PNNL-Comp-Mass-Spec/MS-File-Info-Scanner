
using System;

namespace MSFileInfoScannerInterfaces
{
    /// <summary>
    /// Debug event delegate
    /// </summary>
    /// <param name="message">Debug message</param>
    public delegate void DebugEventEventHandler(string message);

    /// <summary>
    /// Error event delegate
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="ex">Exception; can be null</param>
    public delegate void ErrorEventEventHandler(string message, Exception ex);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="progressMessage"></param>
    /// <param name="percentComplete"></param>
    public delegate void ProgressUpdateEventHandler(string progressMessage, float percentComplete);

    /// <summary>
    /// Status event delegate
    /// </summary>
    /// <param name="message">Status message</param>
    public delegate void StatusEventEventHandler(string message);

    /// <summary>
    /// Warning event delegate
    /// </summary>
    /// <param name="message">Warning message</param>
    public delegate void WarningEventEventHandler(string message);

    /// <summary>
    /// Interface for classes that implement events Debug, Error, ProgressUpdate, Status, and Warning
    /// </summary>
    public interface IEventNotifier
    {

        /// <summary>
        /// Debug event
        /// </summary>
        event DebugEventEventHandler DebugEvent;

        /// <summary>
        /// Error event
        /// </summary>
        event ErrorEventEventHandler ErrorEvent;

        /// <summary>
        /// Progress update event
        /// </summary>
        event ProgressUpdateEventHandler ProgressUpdate;

        /// <summary>
        /// Status event
        /// </summary>
        event StatusEventEventHandler StatusEvent;

        /// <summary>
        /// Warning event
        /// </summary>
        event WarningEventEventHandler WarningEvent;
    }
}
