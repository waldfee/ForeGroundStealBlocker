using System;
using System.Runtime.Remoting;

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

        #region Destruction

        ~ServerInterface()
        {
            RemotingServices.Disconnect(this);
        }

        #endregion

        #region Public Methods

        public void ReportMessage(string message)
        {
            _logger(message);
        }

        #endregion
    }
}