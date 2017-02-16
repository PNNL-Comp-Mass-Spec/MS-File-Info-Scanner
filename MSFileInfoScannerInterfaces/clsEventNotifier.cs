using System;

namespace MSFileInfoScannerInterfaces
{
    public abstract class clsEventNotifier : IEventNotifier
    {

        #region "Events and Event Handlers"

        /// <summary>
        /// Debug event
        /// </summary>
        public event DebugEventEventHandler DebugEvent;

        /// <summary>
        /// Error event
        /// </summary>
        public event ErrorEventEventHandler ErrorEvent;

        /// <summary>
        /// Progress update event
        /// </summary>
        public event ProgressUpdateEventHandler ProgressUpdate;

        /// <summary>
        /// Status event
        /// </summary>
        public event StatusEventEventHandler StatusEvent;

        /// <summary>
        /// Warning event
        /// </summary>
        public event WarningEventEventHandler WarningEvent;

        /// <summary>
        /// Report a debug message
        /// </summary>
        /// <param name="strMessage"></param>
        protected void OnDebugEvent(string strMessage)
        {
            DebugEvent?.Invoke(strMessage);
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="strMessage"></param>
        protected void OnErrorEvent(string strMessage)
        {
            ErrorEvent?.Invoke(strMessage, null);
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="ex">Exception (allowed to be nothing)</param>
        protected void OnErrorEvent(string strMessage, Exception ex)
        {
            ErrorEvent?.Invoke(strMessage, ex);
        }

        /// <summary>
        /// Progress udpate
        /// </summary>
        /// <param name="progressMessage">Progress message</param>
        /// <param name="percentComplete">Value between 0 and 100</param>
        protected void OnProgressUpdate(string progressMessage, float percentComplete)
        {
            ProgressUpdate?.Invoke(progressMessage, percentComplete);
        }

        /// <summary>
        /// Report a status message
        /// </summary>
        /// <param name="strMessage"></param>
        protected void OnStatusEvent(string strMessage)
        {
            StatusEvent?.Invoke(strMessage);
        }

        /// <summary>
        /// Report a warning
        /// </summary>
        /// <param name="strMessage"></param>
        protected void OnWarningEvent(string strMessage)
        {
            WarningEvent?.Invoke(strMessage);
        }

        /// <summary>
        /// Use this method to chain events between classes
        /// </summary>
        /// <param name="oProcessingClass"></param>
        protected void RegisterEvents(IEventNotifier oProcessingClass)
        {
            oProcessingClass.DebugEvent += OnDebugEvent;
            oProcessingClass.StatusEvent += OnStatusEvent;
            oProcessingClass.ErrorEvent += OnErrorEvent;
            oProcessingClass.WarningEvent += OnWarningEvent;
            oProcessingClass.ProgressUpdate += OnProgressUpdate;
        }

        #endregion
    }
}
