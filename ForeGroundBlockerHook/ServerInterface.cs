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
        private readonly Action<int> _removeServer;
        private readonly Func<bool> _shouldAbort;
        private int _pid;

        #endregion

        #region Initialization

        public ServerInterface(Action<string> logger, Action<int> removeServer, Func<bool> shouldAbort)
        {
            _logger = logger;
            _removeServer = removeServer;
            _shouldAbort = shouldAbort;
        }

        #endregion

        #region Destruction

        ~ServerInterface()
        {
            _removeServer(_pid);
            RemotingServices.Disconnect(this);
        }

        #endregion

        #region Public Methods

        public void ReportMessage(string message)
        {
            _logger(message);
        }

        public bool ShouldAbort()
        {
            return _shouldAbort();
        }

        #endregion

        public void SetPid(int pid)
        {
            _pid = pid;
        }
    }
}