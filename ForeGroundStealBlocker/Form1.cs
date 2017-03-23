using System;
using System.Collections.Specialized;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;
using System.Windows.Forms;

using EasyHook;

using ForeGroundBlockerHook;

using ForeGroundStealBlocker.Properties;

namespace ForeGroundStealBlocker
{
    public partial class Form1 : Form
    {
        #region Private Fields

        private string _injectionLibraryPath;

        private StringCollection _blackList;

        #endregion

        #region Initialization

        public Form1()
        {
            InitializeComponent();
        }

        #endregion

        #region Private Methods

        private void Form1_Load(object sender, EventArgs e)
        {
            EnsureReference<InjectionEntryPoint>();

            _blackList = Settings.Default.BlackList;

            // Get the full path to the assembly we want to inject into the target process
            _injectionLibraryPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ForeGroundBlockerHook.dll");

            ManagementEventWatcher startWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            startWatch.EventArrived += StartWatchOnEventArrived;
            startWatch.Start();
        }

        private static void EnsureReference<T>()
        {
            string name = typeof(T).Name;
        }

        private void InjectIntoProcess(int targetPID, string targetProcessName)
        {
            Thread.Sleep(1000);

            try
            {
                Log($"Attempting to inject into process {targetProcessName}, PID {targetPID}");

                string channelName = null;
                ServerInterface server = new ServerInterface(Log);
                RemoteHooking.IpcCreateServer(ref channelName, WellKnownObjectMode.Singleton, server);

                RemoteHooking.Inject(
                    targetPID,
                    InjectionOptions.DoNotRequireStrongName,
                    _injectionLibraryPath,
                    _injectionLibraryPath,
                    channelName);
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
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
            string processName = (string)eventArrivedEventArgs.NewEvent.Properties["ProcessName"].Value;
            uint processId = (uint)eventArrivedEventArgs.NewEvent.Properties["ProcessID"].Value;

            if (!_blackList.Contains(processName))
                return;

            InjectIntoProcess((int)processId, processName);
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

        #endregion

        #region Nested type: UpdateLogDelegate

        private delegate void UpdateLogDelegate(string message);

        #endregion
    }
}