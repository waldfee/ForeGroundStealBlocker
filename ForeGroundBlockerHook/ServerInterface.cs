using System;

namespace ForeGroundBlockerHook
{
    /// <summary>
    /// Provides an interface for communicating from the client (target) to the server (injector)
    /// </summary>
    public class ServerInterface : MarshalByRefObject
    {
        #region Private Fields

        private readonly Action<string> _logger;

        #endregion

        #region Initialization

        public ServerInterface(Action<string> logger)
        {
            _logger = logger;
        }

        #endregion

        #region Public Methods

        public void IsInstalled(int clientPID)
        {
            _logger($"Injected Focus Steal Blocker Hook into process {clientPID}");
        }

        /// <summary>
        /// Output the message to the console.
        /// </summary>
        public void ReportMessages(string[] messages)
        {
            foreach (string message in messages)
            {
                _logger(message);
            }
        }

        public void ReportMessage(string message)
        {
            _logger(message);
        }

        /// <summary>
        /// Report exception
        /// </summary>
        /// <param name="e"></param>
        public void ReportException(Exception e)
        {
            _logger(e.ToString());
        }

        #endregion
    }
}