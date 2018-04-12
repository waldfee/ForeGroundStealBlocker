using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

using EasyHook;

using ForeGroundBlockerHook;

namespace ForeGroundStealBlocker
{
    public partial class Form1 : Form
    {
        #region Private Fields

        private string _injectionLibraryPath;
        private string _currentUser;
        private bool _shouldAbort;
        private readonly ConcurrentDictionary<int, ServerInterface> _servers = new ConcurrentDictionary<int, ServerInterface>();
        private readonly ConcurrentBag<string> _crashList = new ConcurrentBag<string>();

        private readonly List<string> _whiteList = new List<string>
                                                   {
                                                       "EasyHook32Svc.exe",
                                                       "EasyHook64Svc.exe"
                                                   };

        private ManagementEventWatcher _managementEventWatcher;

        #endregion

        #region Initialization

        public Form1()
        {
            InitializeComponent();
        }

        #endregion

        #region Destruction

        ~Form1()
        {
            Abort();
        }

        #endregion

        #region Private Methods

        private void Form1_Load(object sender, EventArgs e)
        {
            EnsureReference<InjectionEntryPoint>();

            Process currentProcess = Process.GetCurrentProcess();
            _currentUser = GetProcessUser(currentProcess);
            _whiteList.Add(currentProcess.MainModule.ModuleName);

            // Get the full path to the assembly we want to inject into the target process
            _injectionLibraryPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ForeGroundBlockerHook.dll");

            _managementEventWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            _managementEventWatcher.EventArrived += StartWatchOnEventArrived;
            _managementEventWatcher.Start();
        }

        private static void EnsureReference<T>()
        {
            string name = typeof(T).Name;
        }

        private void InjectIntoProcess(int targetPID, string targetProcessName)
        {
            if (_crashList.Contains(targetProcessName))
                return;

            Thread.Sleep(1000);

            try
            {
                Log($"Attempting to inject into process {targetProcessName}, PID {targetPID}");

                string channelName = null;
                ServerInterface server = new ServerInterface(Log, RemoveServer, ShouldAbort);
                RemoteHooking.IpcCreateServer(ref channelName, WellKnownObjectMode.Singleton, server);

                RemoteHooking.Inject(
                    targetPID,
                    InjectionOptions.DoNotRequireStrongName,
                    _injectionLibraryPath,
                    _injectionLibraryPath,
                    channelName);

                _servers.AddOrUpdate(targetPID, server, (pid, s) => server);
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
                _crashList.Add(targetProcessName);
            }
        }

        private void RemoveServer(int pid)
        {
            _servers.TryRemove(pid, out ServerInterface server);
        }

        private bool ShouldAbort()
        {
            return _shouldAbort;
        }

        private void Log(string message)
        {
            if (listBoxLog.InvokeRequired)
            {
                listBoxLog.Invoke((UpdateLogDelegate)Log, message);
            }
            else
            {
                listBoxLog.Items.Add($"{DateTime.Now.ToShortTimeString()} {message}");
                if (listBoxLog.Items.Count > 500)
                {
                    listBoxLog.Items.RemoveAt(0);
                }

                listBoxLog.SelectedIndex = listBoxLog.Items.Count - 1;
                listBoxLog.ClearSelected();
            }
        }

        private void StartWatchOnEventArrived(object sender, EventArrivedEventArgs eventArrivedEventArgs)
        {
            int processId = (int)(uint)eventArrivedEventArgs.NewEvent.Properties["ProcessID"].Value;
            string processName = (string)eventArrivedEventArgs.NewEvent.Properties["ProcessName"].Value;
            Process process;

            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException) // process already closed
            {
                return;
            }

            if (process.HasExited)
                return;

            string user = GetProcessUser(process);

            if (_whiteList.Contains(processName) || user != _currentUser)
                return;

            InjectIntoProcess(processId, processName);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == WindowState)
            {
                notifyIcon.Visible = true;
                Hide();
            }
            else if (FormWindowState.Normal == WindowState)
            {
                notifyIcon.Visible = false;
            }
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private static string GetProcessUser(Process process)
        {
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                OpenProcessToken(process.Handle, 8, out processHandle);
                WindowsIdentity wi = new WindowsIdentity(processHandle);
                return wi.Name;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Abort();
        }

        private void Abort()
        {
            _shouldAbort = true;
            Thread.Sleep(1000);
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        #endregion

        #region Nested type: UpdateLogDelegate

        private delegate void UpdateLogDelegate(string message);

        #endregion
    }
}