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
        //These timers allow for a 2 second interval to await waist measure BT input before focusing on next measurement field.
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
                //Missing external service
            }
            //MonitorConnection();
            this.Topmost = true;
          
            Keyboard.Focus(Waist1Measurement);

            StartBleDeviceWatcher();
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);

        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //Focuses on Waist1 field to await first measurement
            Keyboard.Focus(Waist1Measurement);

        }

        //Button to submit Bluetooth collected results of first two measurement fields. Success determined by no 1% difference.
        private void button_Click(object sender, RoutedEventArgs e)
        {
            decimal measurement1;
            decimal measurement2;
            //CSV conversion must go here with appropriate handling. Currently checking for decimal point at string position 2
            try
            {

                //This checks for a decimal place in the 2nd or 3rd array index positions.

               //This checking could maybe be improved.
                if ((arrayMeasurements[1, 1][2] == '.' || arrayMeasurements[1, 1][3] == '.') && (arrayMeasurements[2, 1][2] == '.' || arrayMeasurements[2, 1][3] == '.'))
                {
                    measurement1 = ConvertStrToDec(arrayMeasurements[1, 1]);
                    measurement2 = ConvertStrToDec(arrayMeasurements[2, 1]);
                    arrayMeasurements[1, 6] = "BluetoothInput";
                    arrayMeasurements[2, 6] = "BluetoothInput";

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
                        MessageBox.Show("Third measurement required.\n\nPlease take 10 seconds to re-position yourself for re-taking measurement.\n\n" +
                        "3rd measurement will be enabled after 10 seconds of closing this message.");
                        Thread.Sleep(10000);
                        Waist1Measurement.IsEnabled = false;
                        Waist2Measurement.IsEnabled = false;
                        clear1.IsEnabled = false;
                        clear2.IsEnabled = false;
                        button.IsEnabled = false;
                        button.Visibility = Visibility.Hidden;
                        textBlock6.Visibility = Visibility.Visible;
                        textBlock5.Visibility = Visibility.Visible;
                        textBlock8.Visibility = Visibility.Visible;
                        Waist3Measurement.Visibility = Visibility.Visible;
                        clear3.Visibility = Visibility.Visible;
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

        //Button to submit third measurement, includes the already populated first two measurements after 1% margin check fails.
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //bool notEmpty = String.IsNullOrEmpty(arrayMeasurements[1, 1].Substring(0, 2));
                //bool notEmpty1 = String.IsNullOrEmpty(arrayMeasurements[2, 1].Substring(0, 2));
                //Update to accomodate PIE format. Conversions might be neccessary

                if ((arrayMeasurements[3, 1][2] == '.' )|| (arrayMeasurements[3, 1][3] == '.'))//Checking for decimal point. Maybe this could be improved.
                {
                    arrayMeasurements[3, 6] = "BluetoothInput";
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
                //array indexing exception, user has entered either no data or some invalid data.
                MessageBox.Show("Please enter some measurements.\n\nEnsure your measurements are equal.\n\n You may replace a measurement by clearing it and trying again.\n\n" +
                    "If entering manually, 1 decimal place is expected.\nFor Example 70 cm must be input as 70.0");
            }
        }

        //Button handles any manual measurement cases. Only enabled and visible for manual measurements. Checks all values in text box and adds to measurement.
        private void button3_Click(object sender, RoutedEventArgs e)
        {
            //In the case of button3 click, manualmeasurement == true. So all existing string input must be converted to decimal and added appropriately to arrayMeasurements.
            //Once added to arrayMeasurements, run the necessary checks in the exact same manner that BT measurements are checked. If greater than 1% diff then button4
            //must be enabled which allows submission of third manual measurement. Button3 and Button4 must be disabled upon manualMeasurement unchecked i.e. manualmeasurement == false
            decimal measurement1;
            decimal measurement2;

            try
            {
                //Set arrayMeasurements to equal what surveyor has put manually into text fields. Set Manual Input
                arrayMeasurements[1, 0] = "WA";
                arrayMeasurements[1, 1] = Waist1Measurement.Text;
                arrayMeasurements[2, 0] = "WA";
                arrayMeasurements[2, 1] = Waist2Measurement.Text;
                arrayMeasurements[1, 6] = "ManualInput";
                arrayMeasurements[2, 6] = "ManualInput";

                //Checking for decimal point for all weight possibilities. Using same verification as BT measurement.
                if ((arrayMeasurements[1, 1][2] == '.' || arrayMeasurements[1, 1][3] == '.') && (arrayMeasurements[2, 1][2] == '.' || arrayMeasurements[2, 1][3] == '.'))
                {
                    measurement1 = ConvertStrToDec(arrayMeasurements[1, 1]);
                    measurement2 = ConvertStrToDec(arrayMeasurements[2, 1]);
                    if (CheckGreaterOnePercentDiff(measurement1, measurement2) == false)//Checking that there is a less than 1% difference between two measurements
                    {
                        string csv = ArrayToCsv(arrayMeasurements);
                        WriteCSVFile(csv);
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        //Disable first two measurement boxes. Enable third measurement box, shift focus to third measurement, disable Done measuring Box, 
                        //enable submit final measurements.
                        MessageBox.Show("Third measurement required.\n\nPlease take 10 seconds to re-position yourself for re-taking measurement.\n\n" +
                        "3rd measurement will be enabled after 10 seconds.");
                        Thread.Sleep(10000);
                        Waist1Measurement.IsEnabled = false;
                        Waist2Measurement.IsEnabled = false;
                        button.IsEnabled = false;
                        button.Visibility = Visibility.Hidden;
                        button1.IsEnabled = false;
                        button1.Visibility = Visibility.Hidden;
                        button3.IsEnabled = false;
                        button3.Visibility = Visibility.Hidden;
                        textBlock6.Visibility = Visibility.Visible;
                        textBlock5.Visibility = Visibility.Visible;
                        textBlock8.Visibility = Visibility.Visible;
                        Waist3Measurement.Visibility = Visibility.Visible;
                        clear3.Visibility = Visibility.Visible;
                        button4.Visibility = Visibility.Visible;
                        button4.IsEnabled = true;
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
            {   //array indexing exception, user has entered either no data or some invalid data.
                MessageBox.Show("Please enter some measurements.\n\nEnsure your measurements are equal.\n\n You may replace a measurement by clearing it and trying again.\n\n" +
                    "If entering manually, 1 decimal place is expected.\nFor Example 70 cm must be input as 70.0");
            }
        }


        private void button4_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Update to accomodate PIE format. Conversions might be neccessary

                //Set measurements to be obtained from manual entry and set manual input type
                arrayMeasurements[3, 0] = "WA";
                arrayMeasurements[3, 1] = Waist3Measurement.Text;
                arrayMeasurements[3, 6] = "ManualInput";

                if ((arrayMeasurements[3, 1][2] == '.') || (arrayMeasurements[3, 1][3] == '.'))
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
            {   //array indexing exception, user has entered either no data or some invalid data.
                MessageBox.Show("Please enter some measurements.\n\nEnsure your measurements are equal.\n\n You may replace a measurement by clearing it and trying again.\n\n" +
                    "If entering manually, 1 decimal place is expected.\nFor Example 70 cm must be input as 70.0");
            }
        }

        bool manualMeasurement = false;
        bool regexOverride = false;//allows usage of text box clear operations to delte old results by not having regex applied to user input
        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            manualMeasurement = true;
            Application.Current.Dispatcher.Invoke(() => { Waist1Measurement.Clear(); Waist2Measurement.Clear(); Waist3Measurement.Clear(); });
            MessageBox.Show("You are now entering measurements manually.\n\n" +
                "Please ensure measurements are of 1 decimal place format\n\n" +
                "For example, 80 cm should be inout as 80.0\n" +
                "140 cm should be input as 140.0");
            //////
            RunCleanUp();
            Waist1Measurement.Focus();
            ///////
            regexOverride = false;

        }

        //Decalres a BT measurement
        private void checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            manualMeasurement = false;
            Application.Current.Dispatcher.Invoke(() => { Waist1Measurement.Clear(); Waist2Measurement.Clear(); Waist3Measurement.Clear(); });
            MessageBox.Show("You are now entering measurements with Bluetooth.");
            //////
            RunCleanUp();
            Waist1Measurement.Focus();
            ////////
            regexOverride = false;
        }

        //Clearing measurements from individual fields
        private void clear1_Click(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            Application.Current.Dispatcher.Invoke(() => {Waist1Measurement.Clear();});
            Waist1Measurement.Focus();
            regexOverride = false;
        }

        private void clear2_Click(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            Application.Current.Dispatcher.Invoke(() => { Waist2Measurement.Clear(); });
            Waist2Measurement.Focus();
            regexOverride = false;
        }

        private void clear3_Click(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            Application.Current.Dispatcher.Invoke(() => { Waist3Measurement.Clear(); });
            Waist3Measurement.Focus();
            regexOverride = false;
        }

        //Clears everything in the case of switching between manual input and bluetooth measurements.
        public void RunCleanUp()
        {
            //reset all measruements
            arrayMeasurements[1, 1] = null;
            arrayMeasurements[2, 1] = null;
            arrayMeasurements[3, 1] = null;
            arrayMeasurements[1, 6] = null;
            arrayMeasurements[2, 6] = null;
            arrayMeasurements[3, 6] = null;

            //enable first 2 measurement fields
            Waist1Measurement.IsEnabled = true;
            Waist2Measurement.IsEnabled = true;

            if (manualMeasurement == true) //Enable the manualMeasurement == true button to perform submission calcs using the manually entered measurements and not timer entered measurements.
            {
                button.IsEnabled = false;
                button.Visibility = Visibility.Hidden;
                button3.IsEnabled = true;
                button3.Visibility = Visibility.Visible;
            }
            else //Bluetooth measuring so setting initial button again.
            {
                button.IsEnabled = true;
                button.Visibility = Visibility.Visible;
                button3.IsEnabled = false;
                button3.Visibility = Visibility.Hidden;
            }

            //clear visibility of all things related to taking the third measurement
            textBlock6.Visibility = Visibility.Hidden;
            Waist3Measurement.Visibility = Visibility.Hidden;
            button1.Visibility = Visibility.Hidden;
            textBlock7.Visibility = Visibility.Hidden;
            textBlock8.Visibility = Visibility.Hidden;
            clear3.Visibility = Visibility.Hidden;          
            Waist3Measurement.IsEnabled = false;

            //Set focus to first measurement again
            Waist1Measurement.Focus();

            //Previous input used in Regex expressions for only allowing certain char input. Clearing these avoids duplication of previous inout values.
            previousInput = "";
            previousInput1 = "";
            previousInput2 = "";
        }

        //Updating the connection status.
        public void updateConnectionStatus(string text)
        {
            if (text == "CONNECTED")
            {
                Application.Current.Dispatcher.Invoke(() => { Connectionstatus.Text = text; Connectionstatus.Foreground = Brushes.Green; });
            }
            if (text == "Disconnected")
            {
                Application.Current.Dispatcher.Invoke(() => { Connectionstatus.Text = text; Connectionstatus.Foreground = Brushes.Black; });
            }
        }

        //Text updates for potential rounding purposes
        public void updateH1Text(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { Waist1Measurement.Text = text; });
        }

        public void updateH2Text(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { Waist2Measurement.Text = text; });
        }


        //BleWatcher continuously polls devices on the BLE advertisement stack.
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

        //Any new BT device added to the observable list this is fired. Taken from microsoft BLE client open source and modified.
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

        //Any new BT device updated in the observable list this is fired. Taken from microsoft BLE client open source and modified.
        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {

            //if contains PIE and PIE is connectable stop all other handlers and connect  
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
                            // Device is already being displayed, Update infor with most recent data
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

                        //left here as residual from orginal microsoft UWP app. Needs this code to properly function.
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

        //Takes the ID of found device and returns the displayDeviceType
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

        //Initialising all fields to be used in final csv data save.
        string[,] arrayMeasurements = new string[4, 7];
        private void initialiseSurveyorInfo()
        {
            arrayMeasurements[0, 0] = "MeasureType";
            arrayMeasurements[0, 1] = "Measurement";
            arrayMeasurements[0, 2] = "Qtr";
            arrayMeasurements[0, 3] = "MB";
            arrayMeasurements[0, 4] = "HHID";
            arrayMeasurements[0, 5] = "RespondentID";
            arrayMeasurements[0, 6] = "MeasurementInputType";
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

        //Retrieve SM generated identifiers from txt file
        private string[] GetRespondentIdentifiers()
        {
            string respIDs = File.ReadLines(@"C:\NZHS\surveyinstructions\MeasurementInfo.txt").First();
            string[] respIDSplit = respIDs.Split('+');//Split each field with '+' so txt file must be saved in that format
            return respIDSplit;
        }

        //A timer is fired if input is done via BT. this event corresponds to timer interval for operations such as focus change and prompts to surveyor to reset measurement.
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
                MessageBox.Show("Please take 10 seconds to re-position yourself for re-taking measurement.\n\n" +
                    "2nd measurement will be enabled after 10 seconds.");
                Thread.Sleep(10000);
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

        //Only permit numbers and decimal points in fields and start timer to fire data logging if it is a BT input.
        string waist1orwaist2 = null;
        string previousInput = "";
        private void Waist1Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false)
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
            }
            if (manualMeasurement == false)
            {
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

        }

        string previousInput1 = "";
        private void Waist2Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false)
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
            }
            if (manualMeasurement == false)
            {
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

        }

        string previousInput2 = "";
        private void Waist3Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false)
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
            }
            if (manualMeasurement == false)
            {
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
        }

        //Stores any 2d array into a csv file
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

        //Writes dynamic date csv file
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

        //Percentage check which is global for all BT mesurements.
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

        //handle external windows with this class. left here in case other external processes are integrated.
        public class WindowControl
        {
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
