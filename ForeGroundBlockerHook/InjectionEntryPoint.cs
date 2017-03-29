using System;
using System.Collections.Generic;
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

        #endregion

        #region Initialization

        public InjectionEntryPoint(RemoteHooking.IContext context, string channelName)
        {
            _server = RemoteHooking.IpcConnectClient<ServerInterface>(channelName);
        }

        #endregion

        #region Public Methods

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

        public void Run(RemoteHooking.IContext context, string channelName)
        {
            Log($"Injected Focus Steal Blocker Hook into process {RemoteHooking.GetCurrentProcessId()}");

            LocalHook setForegroundWindowHook = LocalHook.Create(
                LocalHook.GetProcAddress("User32.dll", "SetForegroundWindow"),
                new SetForegroundWindow_Delegate(SetForegroundWindow_Hook),
                this);

            setForegroundWindowHook.ThreadACL.SetExclusiveACL(new[] {0});

            Log($"SetForegroundWindow hook installed for PID {RemoteHooking.GetCurrentProcessId()}");

            RemoteHooking.WakeUpProcess();

            try
            {
                while (true)
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

        ~InjectionEntryPoint()
        {
            Log($"PID {RemoteHooking.GetCurrentProcessId()} shutting down");
        }

        #endregion

        #region Private Methods

        private bool SetForegroundWindow_Hook(IntPtr hWnd)
        {
            Log($"SetForegroundWindow called from PID {RemoteHooking.GetCurrentProcessId()}");

            FLASHWINFO flashwinfo = new FLASHWINFO();
            flashwinfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(flashwinfo));
            flashwinfo.hwnd = hWnd;
            flashwinfo.dwFlags = 2;
            flashwinfo.uCount = 2;
            flashwinfo.dwTimeout = 0;
            FlashWindowEx(ref flashwinfo);

            return false;
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