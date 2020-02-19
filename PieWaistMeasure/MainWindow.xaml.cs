using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace PieWaistMeasure
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                

               //Any external services we need to do


            }
            catch
            {
                MessageBox.Show("Could not find EXTERNAL service for Bluetooth transfer");
            }
            //MonitorConnection();
            this.Topmost = true;
            Keyboard.Focus(Waist1Measurement);

            StartBleDeviceWatcher();
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);

        }

        Process process = new Process();
        void OpenLeicaService()
        {

            string fileName = @"C:\Program Files (x86)\DISTO transfer 60\DistoTransfer.exe";

            process.StartInfo.Arguments = null;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            process.StartInfo.FileName = fileName;
            process.StartInfo.UseShellExecute = true;
            process.Start();

        }

        private void MinimizeLeicaService()
        {

            WindowControl DistoTransfer = new WindowControl();
            DistoTransfer.AppName = "DistoTransfer.exe";
            DistoTransfer.Minimize();
        }


        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            Keyboard.Focus(Waist1Measurement);

        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            //CSV conversion must go here with appropriate handling. Currently checking for decimal point at string position 2
            try
            {
                //Update to accomodate PIE format. Conversions might be neccessary
                if (arrayMeasurements[0, 1].Substring(0, 2).Contains(".") && arrayMeasurements[1, 1].Substring(0, 2).Contains("."))
                {
                    string csv = ArrayToCsv(arrayMeasurements);
                    WriteCSVFile(csv);
                    WindowControl DistoTransfer = new WindowControl();
                    DistoTransfer.AppName = "DistoTransfer";
                    DistoTransfer.Close();
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("Incorrect height format. \n\n Please ensure you've collected results using Bluetooth waist measure");
                }
            }
            catch
            {
                MessageBox.Show("Please enter some measurements");
            }

        }



        public void updateConnectionStatus(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { Connectionstatus.Text = text; });
        }

        public void updateH1Text(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { Waist1Measurement.Text = text; });
        }

        public void updateH2Text(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { Waist2Measurement.Text = text; });
        }

        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();

        private DeviceWatcher deviceWatcher;
        private void StartBleDeviceWatcher()
        {
            // Additional properties we would like about the device.
            // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

            // BT_Code: Example showing paired and non-paired in a single query.
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            //deviceWatcher.Removed += DeviceWatcher_Removed;
            //deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            //deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            KnownDevices.Clear();



            // Start the watcher. Active enumeration is limited to approximately 30 seconds.
            // This limits power usage and reduces interference with other Bluetooth activities.
            // To monitor for the presence of Bluetooth LE devices for an extended period,
            // use the BluetoothLEAdvertisementWatcher runtime class. See the BluetoothAdvertisement
            // sample for an example.
            deviceWatcher.Start();
        }
        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            await Task.Run(async () =>
            {
                lock (this)
                {

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Make sure device isn't already present in the list.
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name != string.Empty)
                            {
                                // If device has a friendly name display it immediately.
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                            }
                            else
                            {
                                // Add it to a list in case the name gets updated later. 
                                UnknownDevices.Add(deviceInfo);
                            }
                        }

                    }
                }
            });

        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {

            //if contains salter and salter is connectable stop all other handlers and connect  
            await Task.Run(async () =>
            {
                lock (this)
                {


                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            DeviceInformation updatedDevice = bleDeviceDisplay.DeviceInformation;
                            //IsConnectable will be established once updated accordingly here. So function needs to be added that handles all devices.
                            if (bleDeviceDisplay.IsConnected && bleDeviceDisplay.Name.Contains("PIE"))
                            {
                                updateConnectionStatus("CONNECTED");

                            }
                            if (bleDeviceDisplay.IsConnected == false && bleDeviceDisplay.Name.Contains("PIE"))
                            {
                                updateConnectionStatus("Disconnected");
                            }

                            return;
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            deviceInfo.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfo.Name != String.Empty)
                            {
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                UnknownDevices.Remove(deviceInfo);
                            }
                        }
                    }
                }
            });

        }

        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (DeviceInformation bleDeviceInfo in UnknownDevices)
            {
                if (bleDeviceInfo.Id == id)
                {
                    return bleDeviceInfo;
                }
            }
            return null;
        }

        public class BluetoothLEDeviceDisplay : INotifyPropertyChanged
        {
            public BluetoothLEDeviceDisplay(DeviceInformation deviceInfoIn)
            {
                DeviceInformation = deviceInfoIn;

            }

            public DeviceInformation DeviceInformation { get; private set; }

            public string Id => DeviceInformation.Id;
            public string Name => DeviceInformation.Name;
            public bool IsPaired => DeviceInformation.Pairing.IsPaired;
            public bool IsConnected => (bool?)DeviceInformation.Properties["System.Devices.Aep.IsConnected"] == true;
            public bool IsConnectable => (bool?)DeviceInformation.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true;

            public IReadOnlyDictionary<string, object> Properties => DeviceInformation.Properties;



            public event PropertyChangedEventHandler PropertyChanged;

            public void Update(DeviceInformationUpdate deviceInfoUpdate)
            {
                DeviceInformation.Update(deviceInfoUpdate);

                OnPropertyChanged("Id");
                OnPropertyChanged("Name");
                OnPropertyChanged("DeviceInformation");
                OnPropertyChanged("IsPaired");
                OnPropertyChanged("IsConnected");
                OnPropertyChanged("Properties");
                OnPropertyChanged("IsConnectable");

            }


            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }


        string[,] arrayMeasurements = new string[3, 6];
        private void initialiseSurveyorInfo()
        {
            arrayMeasurements[0, 0] = "MeasureType";
            arrayMeasurements[0, 1] = "Measurement";
            arrayMeasurements[0, 2] = "Qtr";
            arrayMeasurements[0, 3] = "MB";
            arrayMeasurements[0, 4] = "HHID";
            arrayMeasurements[0, 5] = "RespondentID";
            string[] respondentInfo = GetRespondentIdentifiers();
            arrayMeasurements[1, 2] = respondentInfo[0];
            arrayMeasurements[1, 3] = respondentInfo[1];
            arrayMeasurements[1, 4] = respondentInfo[2];
            arrayMeasurements[1, 5] = respondentInfo[3];
            arrayMeasurements[2, 2] = respondentInfo[0];
            arrayMeasurements[2, 3] = respondentInfo[1];
            arrayMeasurements[2, 4] = respondentInfo[2];
            arrayMeasurements[2, 5] = respondentInfo[3];


        }

        private string[] GetRespondentIdentifiers()
        {
            string respIDs = File.ReadLines(@"C:\NZHS\surveyinstructions\MeasurementInfo.txt").First();
            string[] respIDSplit = respIDs.Split('+');
            return respIDSplit;
        }

        private void H1Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (Waist1Measurement.Text.Length > 5)
            {
                string rounded = Waist1Measurement.Text.Substring(0, 5);
                arrayMeasurements[1, 0] = "WA";
                arrayMeasurements[1, 1] = rounded;
                updateH1Text(rounded.ToString());
                Keyboard.Focus(Waist2Measurement);
            }
        }

        private void H2Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Waist2Measurement.Text.Length > 5)
            {
                string rounded = Waist2Measurement.Text.Substring(0, 5);
                arrayMeasurements[2, 0] = "WA";
                arrayMeasurements[2, 1] = rounded;
                updateH2Text(rounded.ToString());
                Keyboard.Focus(Waist1Measurement);
            }
        }

        

        static string ArrayToCsv(string[,] values)
        {
            // Get the bounds.
            int num_rows = values.GetUpperBound(0) + 1;
            int num_cols = values.GetUpperBound(1) + 1;

            // Convert the array into a CSV string.
            StringBuilder sb = new StringBuilder();
            for (int row = 0; row < num_rows; row++)
            {
                // Add the first field in this row.
                sb.Append(values[row, 0]);

                // Add the other fields in this row separated by commas.
                for (int col = 1; col < num_cols; col++)
                    sb.Append("," + values[row, col]);

                // Move to the next line.
                sb.AppendLine();
            }

            // Return the CSV format string.
            return sb.ToString();
        }

        private void WriteCSVFile(string csvMeasurements)
        {

            System.IO.Directory.CreateDirectory(@"C:\BodyMeasurements\WaistMeasurements");
            string CSVFileName = @"C:\BodyMeasurements\WaistMeasurements\" + "WaistMeasurements_" + DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss") + ".csv";

            System.IO.File.WriteAllText(CSVFileName, csvMeasurements);


        }

        public class WindowControl
        {
            //Set-up to declare which application we want to perform controls on. Vary for chrome and 
            //LaptopShowcards minimisation.
            //"chrome"
            //"BluetoothTestClient"
            private string appName;  // the name field
            public string AppName    // the Name property
            {
                get
                {
                    return appName;
                }
                set
                {
                    appName = value;
                }
            }

            [DllImport("user32.dll")]
            private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            private const int ForceMinimize = 11;
            private const int RestoreMaximization = 9;
            public void Minimize()
            {
                Process[] processlist = Process.GetProcesses();

                foreach (Process process in processlist.Where(process => process.ProcessName == appName))
                {
                    ShowWindow(Process.GetProcessById(process.Id).MainWindowHandle, ForceMinimize);
                }
            }
            public void Restore()
            {
                Process[] processlist = Process.GetProcesses();

                foreach (Process process in processlist.Where(process => process.ProcessName == appName))
                {
                    ShowWindow(Process.GetProcessById(process.Id).MainWindowHandle, RestoreMaximization);
                }
            }

            public void Close()
            {
                if (Process.GetProcessesByName(appName).Length > 0)
                {
                    foreach (Process proc in Process.GetProcessesByName(appName))
                    {
                        proc.Kill();
                    }
                }
            }

        }
    }
}
