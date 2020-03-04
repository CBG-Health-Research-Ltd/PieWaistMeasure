using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace PieWaistMeasure
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Threading.DispatcherTimer _typingTimer;
        System.Windows.Threading.DispatcherTimer _typingTimer1;
        System.Windows.Threading.DispatcherTimer _typingTimer2;
        public MainWindow()
        {
            InitializeComponent();
            initialiseSurveyorInfo();
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
            decimal measurement1;
            decimal measurement2;
            //CSV conversion must go here with appropriate handling. Currently checking for decimal point at string position 2
            try
            {
                //bool notEmpty = String.IsNullOrEmpty(arrayMeasurements[1, 1].Substring(0, 2));
                //bool notEmpty1 = String.IsNullOrEmpty(arrayMeasurements[2, 1].Substring(0, 2));
                //Update to accomodate PIE format. Conversions might be neccessary
               
                if ((arrayMeasurements[1, 1][2] == '.' && arrayMeasurements[2, 1][2] == '.') || (arrayMeasurements[1, 1][3] == '.' && arrayMeasurements[2, 1][3] == '.'))
                {
                    measurement1 = ConvertStrToDec(arrayMeasurements[1, 1]);
                    measurement2 = ConvertStrToDec(arrayMeasurements[2, 1]);
                    if (CheckGreaterOnePercentDiff(measurement1, measurement2) == false)//Checking that there is a less than 1% difference between two measurements
                    {
                        string csv = ArrayToCsv(arrayMeasurements);
                        WriteCSVFile(csv);
                        Application.Current.Shutdown();
                    }
                    else //There is a greater than 1% difference, therefore get a 3rd measurement by enabling third measurement box.
                    {
                        //Disable first two measurement boxes. Enable third measurement box, shift focus to third measurement, disable Done measuring Box, 
                        //enable submit final measurements.
                        Waist1Measurement.IsEnabled = false;
                        Waist2Measurement.IsEnabled = false;
                        button.IsEnabled = false;
                        button.Visibility = Visibility.Hidden;
                        textBlock6.Visibility = Visibility.Visible;
                        textBlock5.Visibility = Visibility.Visible;
                        textBlock8.Visibility = Visibility.Visible;
                        Waist3Measurement.Visibility = Visibility.Visible;
                        button1.Visibility = Visibility.Visible;
                        textBlock7.Visibility = Visibility.Visible;
                        Waist3Measurement.IsEnabled = true;
                        Waist3Measurement.Focus();
                    }
                }
                else
                {
                    MessageBox.Show("Incorrect waist measurement format. \n\n Please ensure you've collected results using Bluetooth waist measure.\n\n" +
                        "If entering manually, 1 decimal place is expected.\nFor Example 70 cm must be input as 70.0");
                }
            }
            catch
            {
                MessageBox.Show("Please enter some measurements.\n\nEnsure your measurements are equal.\n\n You may replace a measurement by clearing it and trying again.\n\n" +
                    "If entering manually, 1 decimal place is expected.\nFor Example 70 cm must be input as 70.0");
            }

        }


        private void button1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //bool notEmpty = String.IsNullOrEmpty(arrayMeasurements[1, 1].Substring(0, 2));
                //bool notEmpty1 = String.IsNullOrEmpty(arrayMeasurements[2, 1].Substring(0, 2));
                //Update to accomodate PIE format. Conversions might be neccessary

                if ((arrayMeasurements[3, 1][2] == '.' )|| (arrayMeasurements[3, 1][3] == '.'))
                {
                    string csv = ArrayToCsv(arrayMeasurements);
                    WriteCSVFile(csv);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("Incorrect waist measurement format. \n\n Please ensure you've collected results using Bluetooth waist measure.\n\n" +
                        "If entering manually, 1 decimal place is expected.\nFor Example 70 cm must be input as 70.0");
                }
            }
            catch
            {
                MessageBox.Show("Please enter some measurements.\n\nEnsure your measurements are equal.\n\n You may replace a measurement by clearing it and trying again.\n\n" +
                    "If entering manually, 1 decimal place is expected.\nFor Example 70 cm must be input as 70.0");
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


        string[,] arrayMeasurements = new string[4, 6];
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
            arrayMeasurements[3, 2] = respondentInfo[0];
            arrayMeasurements[3, 3] = respondentInfo[1];
            arrayMeasurements[3, 4] = respondentInfo[2];
            arrayMeasurements[3, 5] = respondentInfo[3];


        }

        private string[] GetRespondentIdentifiers()
        {
            string respIDs = File.ReadLines(@"C:\NZHS\surveyinstructions\MeasurementInfo.txt").First();
            string[] respIDSplit = respIDs.Split('+');
            return respIDSplit;
        }

        private void handleTypingTimerTimeout(object sender, EventArgs e)
        {
            
            var timer = sender as DispatcherTimer; // WPF
            if (timer == null)
            {
                return;
            }
            //Do operation here
            if(waist1orwaist2 == "waist1")
            {
                arrayMeasurements[1, 0] = "WA";
                arrayMeasurements[1, 1] = Waist1Measurement.Text;
                Keyboard.Focus(Waist2Measurement);
            }
            else if(waist1orwaist2 == "waist2")
            {
                arrayMeasurements[2, 0] = "WA";
                arrayMeasurements[2, 1] = Waist2Measurement.Text;
                //Keyboard.Focus(Waist1Measurement);
            }
            else if(waist1orwaist2 == "waist3")
            {
                arrayMeasurements[3, 0] = "WA";
                arrayMeasurements[3, 1] = Waist3Measurement.Text;
            }

            // The timer must be stopped! We want to act only once per keystroke.
            timer.Stop();
            _typingTimer = null;
            _typingTimer1 = null;
            _typingTimer2 = null;


        }

        string waist1orwaist2 = null;
        string previousInput = "";
        private void Waist1Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // This is the main part, can be altered to match any desired form or limitations
            Match m = r.Match(Waist1Measurement.Text);
            if (m.Success)
            {
                previousInput = Waist1Measurement.Text;
            }
            else
            {
                Waist1Measurement.Text = previousInput;
            }
            if (_typingTimer == null)//Only use these functions for PIE input, for manual input user will have to toggle to second box. Bool manual == false else do nothing
            {
                _typingTimer = new DispatcherTimer();
                _typingTimer.Interval = TimeSpan.FromMilliseconds(2000);
                waist1orwaist2 = "waist1";
                _typingTimer.Tick += new EventHandler(this.handleTypingTimerTimeout);
            }
            _typingTimer.Stop(); // Resets the timer
            _typingTimer.Tag = (sender as TextBox).Text; // This should be done with EventArgs
            _typingTimer.Start();

        }

        string previousInput1 = "";
        private void Waist2Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // This is the main part, can be altered to match any desired form or limitations
            Match m = r.Match(Waist2Measurement.Text);
            if (m.Success)
            {
                previousInput1 = Waist2Measurement.Text;
            }
            else
            {
                Waist2Measurement.Text = previousInput1;
            }
            if (_typingTimer1 == null)//Only use these functions for PIE input, for manual input user will have to toggle to second box. Bool manual == false else do nothing
            {
                _typingTimer1 = new DispatcherTimer();
                _typingTimer1.Interval = TimeSpan.FromMilliseconds(2000);
                waist1orwaist2 = "waist2";
                _typingTimer1.Tick += new EventHandler(this.handleTypingTimerTimeout);
            }
            _typingTimer1.Stop(); // Resets the timer
            _typingTimer1.Tag = (sender as TextBox).Text; // This should be done with EventArgs
            _typingTimer1.Start();

        }

        string previousInput2 = "";
        private void Waist3Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // This is the main part, can be altered to match any desired form or limitations
            Match m = r.Match(Waist3Measurement.Text);
            if (m.Success)
            {
                previousInput2 = Waist3Measurement.Text;
            }
            else
            {
                Waist3Measurement.Text = previousInput2;
            }
            if (_typingTimer2 == null)//Only use these functions for PIE input, for manual input user will have to toggle to second box. Bool manual == false else do nothing
            {
                _typingTimer2 = new DispatcherTimer();
                _typingTimer2.Interval = TimeSpan.FromMilliseconds(2000);
                waist1orwaist2 = "waist3";
                _typingTimer2.Tick += new EventHandler(this.handleTypingTimerTimeout);
            }
            _typingTimer2.Stop(); // Resets the timer
            _typingTimer2.Tag = (sender as TextBox).Text; // This should be done with EventArgs
            _typingTimer2.Start();
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

        private decimal ConvertStrToDec(string value)
        {
            decimal convert = Convert.ToDecimal(value);
            return convert;
        }

        private bool CheckGreaterOnePercentDiff(decimal value1, decimal value2)
        {
            if (value1 > value2)
            {
                decimal percent = ((value1 / value2) * 100);
                if (percent > 101)
                {
                    return true; //true indicating that there is a higher than 1% difference
                }
                else
                {
                    return false; //false indicating that the difference is within 1%
                }
            }
            else if (value2 > value1)
            {
                decimal percent = ((value2 / value1) * 100);
                if (percent > 101)
                {
                    return true; //true indicating that there is a higher than 1% difference
                }
                else
                {
                    return false; //false indicating that the difference is within 1%
                }
            }
            else
            {
                return false; // All other cases false as value1 and value2 will be equal
            }
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
