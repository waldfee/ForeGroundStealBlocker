using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using EasyHook;

namespace ForeGroundBlockerHook
{
    public class InjectionEntryPoint : IEntryPoint
    {
        #region Private Fields

        private readonly ServerInterface _server;
        private readonly Queue<string> _messageQueue = new Queue<string>();
        private string _processName;
        private int _pid;

        //private const int FLASHW_STOP = 0;
        private const int FLASHW_TRAY = 2;

        #endregion

        #region Initialization

        public InjectionEntryPoint(RemoteHooking.IContext context, string channelName)
        {
            _server = RemoteHooking.IpcConnectClient<ServerInterface>(channelName);
        }

        #endregion

        #region Destruction

        ~InjectionEntryPoint()
        {
            Log($"{GetProcessDescription()} shutting down");
        }

        #endregion

        #region Public Methods

        public void Run(RemoteHooking.IContext context, string channelName)
        {
            _pid = RemoteHooking.GetCurrentProcessId();
            _processName = Path.GetFileName(Process.GetProcessById(_pid).MainModule.FileName);

            _server.SetPid(_pid);

            Log($"Injected Focus Steal Blocker Hook into process {GetProcessDescription()}");

            LocalHook setForegroundWindowHook = LocalHook.Create(
                LocalHook.GetProcAddress("User32.dll", "SetForegroundWindow"),
                new SetForegroundWindow_Delegate(SetForegroundWindow_Hook),
                this);

            setForegroundWindowHook.ThreadACL.SetExclusiveACL(new[] {0});

            Log($"SetForegroundWindow hook installed for {GetProcessDescription()}");

            RemoteHooking.WakeUpProcess();

            try
            {
                while (_server != null && !_server.ShouldAbort())
                {
                    Thread.Sleep(500);

                    string[] queued;

                    lock (_messageQueue)
                    {
                        queued = _messageQueue.ToArray();
                        _messageQueue.Clear();
                    }

                    if (queued.Length > 0)
                    {
                        foreach (string message in queued)
                        {
                            Log(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            // Remove hooks
            setForegroundWindowHook.Dispose();

            // Finalise cleanup of hooks
            LocalHook.Release();
        }

        #endregion

        #region Private Methods

        private void Log(string message)
        {
            try
            {
                _server?.ReportMessage(message);
            }
            catch
            {
                // exception here kills the host process. we do not want this.
            }
        }

        private string GetProcessDescription()
        {
            return $"{_processName};{Process.GetProcessById(_pid).MainWindowTitle};{_pid}";
        }

        private bool SetForegroundWindow_Hook(IntPtr hWnd)
        {
            Log($"SetForegroundWindow called from {GetProcessDescription()}");

            FLASHWINFO flash = CreateFlashwinfo(hWnd, 2, FLASHW_TRAY);
            FlashWindowEx(ref flash);

            return false;
        }

        private static FLASHWINFO CreateFlashwinfo(IntPtr hWnd, uint count, uint dwFlags)
        {
            FLASHWINFO flashwinfo = new FLASHWINFO();
            flashwinfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(flashwinfo));
            flashwinfo.hwnd = hWnd;
            flashwinfo.dwFlags = dwFlags;
            flashwinfo.uCount = count;
            flashwinfo.dwTimeout = 0;

            return flashwinfo;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        #endregion

        #region Nested type: FLASHWINFO

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        #endregion

        #region Nested type: SetForegroundWindow_Delegate

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool SetForegroundWindow_Delegate(IntPtr hWnd);

        #endregion
    }
}