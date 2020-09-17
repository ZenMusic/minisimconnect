using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MiniSimconnect
{
    public enum DUMMYENUM
    {
        Dummy = 0
    }

    /// exe -->    C:\Users\davidmc\source\repos\minisimconnect\MiniSimconnect\obj\Release
    /// required -->    Microsoft.FlightSimulator.SimConnect.dll
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static int idxCounter = 1;
        static int idx_ap_Master = idxCounter++;
        static int idx_approach = idxCounter++;
        static int idx_altitude = idxCounter++;
        static int idx_airSpeed = idxCounter++;
        static int idx_next_wp_alt = idxCounter++;
        static int idx_autoThrottle = idxCounter++;
        static int idx_Vr = idxCounter++;
        static int idx_parkingBrake = idxCounter++;
        static int idx_throttlePosition = idxCounter++;
        static int idx_GroundAltitude = idxCounter++;

        /// <summary>
        /// Contains the list of all the SimConnect properties we will read, the unit is separated by coma by our own code.
        /// </summary>
        Dictionary<int, string> simConnectProperties = new Dictionary<int, string>
        {
            {idx_ap_Master,"AUTOPILOT MASTER,Bool" },
            {idx_approach,"AUTOPILOT APPROACH HOLD,Bool" },
            {idx_parkingBrake,"BRAKE PARKING POSITION,Position" },
            {idx_autoThrottle,"AUTOTHROTTLE ACTIVE,boolean" },
            {idx_altitude,"PLANE ALTITUDE,feet" },
            {idx_airSpeed,"AIRSPEED INDICATED,knots" },
            {idx_next_wp_alt,"GPS WP NEXT ALT,feet" },  //
            {idx_Vr,"DESIGN SPEED MIN ROTATION,knots" },
            {idx_throttlePosition,"GENERAL ENG THROTTLE LEVER POSITION:1,percent" },
            {idx_GroundAltitude,"GROUND ALTITUDE,feet" }
        };
        // GENERAL ENG PCT MAX RPM ,number
        Dictionary<int, string> was = new Dictionary<int, string>
        {
            {1,"PLANE LONGITUDE,degree" },
            {2,"PLANE LATITUDE,degree" },
            {3,"PLANE HEADING DEGREES MAGNETIC,degree" },
            {4,"PLANE ALTITUDE,feet" },
            {5,"AIRSPEED INDICATED,knots" },
            {6,"AUTOTHROTTLE ACTIVE,boolean" },
        };
        /// User-defined win32 event => put basically any number?
        public const int WM_USER_SIMCONNECT = 0x0402;

        SimConnect MySim;

        /// <summary>
        ///  Direct reference to the window pointer
        /// </summary>
        /// <returns></returns>
        protected HwndSource GetHWinSource()
        {
            return PresentationSource.FromVisual(this) as HwndSource;
        }
        

        void Test()
        {
           // MySim.MapClientEventToSimEvent(KEY_GEAR_TOGGLE, GEAR_TOGGLE);

        }
        /// <summary>
        /// Returns a label based on a uid number
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        private Label GetLabelForUid(int uid)
        {
            if (uid > 107)
                uid = 107;
            return (Label)mainGrid.Children
                 .Cast<UIElement>()
                 .First(row => row.Uid == uid.ToString());
        }

        public MainWindow()
        {
            InitializeComponent();
            this.Topmost = true;
            // Starts our connection and poller
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick; 
            timer.Start();
        }
        DateTime takeOffTime;
        TimeSpan ts;

        FLT_STAGE fltStage = FLT_STAGE.Start;
        int error = 0;

       // bool bTakeOff = false;
       // bool bLanded = false;
        bool bRotateMessageSent = false;

        bool bTimer = false;

        bool bSetElevation = false;
        int altitudePlane = 0;
        int elevation = 0;
        int vRotate = 0;

        public void Reset()
        {
            takeOffTime = DateTime.Now;
            // ts = 0;
            bTimer = false;
            //fltStage = FLT_STAGE.Start;
            ChangeFltState(FLT_STAGE.Start);
            error = 0;

            //bTakeOff = false;
            //bLanded = false;
            bRotateMessageSent = false;

            //bSetElevation = false;
            altitudePlane = 0;
            //elevation = 0;
            vRotate = 0;
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (MySim != null)
            {
                try
                {
                    foreach (var toConnect in simConnectProperties)
                        MySim.RequestDataOnSimObjectType((DUMMYENUM)toConnect.Key, (DUMMYENUM)toConnect.Key, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                }
                catch
                {
                    Disconnect();
                }
            }
            if (bTimer)
            {
                ts = DateTime.Now - takeOffTime;
                btTimer.Content = ts.ToString();
                if (ts.TotalSeconds > 90)
                {
                    btTimer.Background = Brushes.Red;
                }
            }
        }
        /// <summary>
        /// We received a disconnection from SimConnect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void Sim_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            this.Title = "Disconnected";
        }

        /// <summary>
        /// We received a connection from SimConnect.
        /// Let's register all the properties we need.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void Sim_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            this.Title = "Connected";

            foreach (var toConnect in simConnectProperties)
            {
                var values = toConnect.Value.Split(new char[] { ',' });
                /// Define a data structure
                MySim.AddToDataDefinition((DUMMYENUM)toConnect.Key, values[0], values[1], SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
               // GetLabelForUid(100 + toConnect.Key).Content = values[1];
                /// IMPORTANT: Register it with the simconnect managed wrapper marshaller
                /// If you skip this step, you will only receive a uint in the .dwData field.
                MySim.RegisterDataDefineStruct<double>((DUMMYENUM)toConnect.Key);
            }
        }




        public enum FLT_STAGE
        {
            Start,
            TaxiBeforeTakeOff,
            RunUp,
            TakeOff,  // first time over > 40 K
            Climb,
            Cruise,
            Descent,
            Approach,
            GoAround,
            Landed,  //   < 40 K
            TaxiAfterLanding,
            Parked,
            none
        }
        public FLT_STAGE ChangeFltState(FLT_STAGE newState , bool bConfirm = false)
        {
            if (newState < fltStage && bConfirm)
            {
                if (fltStage > FLT_STAGE.Start)
                {
                    fltStage = newState;
                }
            }
            if (newState < fltStage && newState != FLT_STAGE.Start)
            {
                ++error;
                TextBoxStatus.Text = $"{error}";
                return fltStage;
            }
            DisplayState(newState);
            if (newState == FLT_STAGE.Landed)
            {
                newState = FLT_STAGE.TaxiAfterLanding;
            }
            //
            fltStage = newState;
            DisplayState(newState);
            //tbAtState.Text = newState.ToString();
            btFlightStage.Content = fltStage.ToString();
            switch (fltStage)
            {
                case FLT_STAGE.TaxiAfterLanding:
                case FLT_STAGE.TaxiBeforeTakeOff:
                    btFlightStage.Background = Brushes.LightBlue;
                    break;
                case FLT_STAGE.Climb:
                case FLT_STAGE.Approach:
                    btFlightStage.Background = Brushes.LightGreen;
                    break;
                case FLT_STAGE.Cruise:
                    btFlightStage.Background = Brushes.LightCyan;
                    break;
            }
            return newState;
        }

        public void DisplayState(FLT_STAGE state)
        {
        }


        /// <summary>
        /// Try to connect to the Sim, and in case of success register the hooks
        /// </summary>
        private void Connect()
        {
            if (MySim != null)
                return;
            /// The constructor is similar to SimConnect_Open in the native API
            try
            {
                // Pass the self defined ID which will be returned on WndProc
                MySim = new SimConnect(this.Title, GetHWinSource().Handle, WM_USER_SIMCONNECT, null, 0);
                MySim.OnRecvOpen += Sim_OnRecvOpen;
                MySim.OnRecvQuit += Sim_OnRecvQuit;
                MySim.OnRecvSimobjectDataBytype += Sim_OnRecvSimobjectDataBytype;
            }
            catch
            {
                this.Title = "Err connecting";
                MySim = null;
            }
        }

        /// <summary>
        /// Received data from SimConnect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        //       {3,"GENERAL ENG THROTTLE LEVER POSITION ,number" },
        //       {4,"BRAKE PARKING POSITION,Position" },
        //       {5,"AIRSPEED INDICATED,knots" },
        //       {6,"AUTOTHROTTLE ACTIVE,boolean" },
        // ---------------------------------------------------------------------
        private void Sim_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            int iRequest = (int)data.dwRequestID;
            double dValue = (double)data.dwData[0];
            if (iRequest == idx_ap_Master)
            {
                if (dValue > 0)
                    btAutoPilot.Background = Brushes.LightGreen;
                else
                    btAutoPilot.Background = Brushes.LightGray;
            }
            else if (iRequest == idx_approach)
            {
                if (dValue > 0)
                {
                    btApproach.Background = Brushes.LightGreen;
                    ChangeFltState(FLT_STAGE.Approach);
                }
                else
                    btApproach.Background = Brushes.LightGray;
            }
            else if (iRequest == idx_airSpeed)
            {
                int speed = Convert.ToInt32(dValue);
                btSpeed.Content = speed.ToString();
                if (fltStage < FLT_STAGE.TaxiBeforeTakeOff)
                {
                    if (speed > 4)
                        ChangeFltState(FLT_STAGE.TaxiBeforeTakeOff);
                }
                if (fltStage == FLT_STAGE.RunUp)
                    if (!bRotateMessageSent && vRotate > 0 && speed >= (vRotate - 2))
                    {
                        bRotateMessageSent = true;
                        btSpeed.Background = Brushes.Green;
                        //.Background = Brushes.LightGreen;
                    }
                if (fltStage < FLT_STAGE.TakeOff)
                {

                    if (altitudePlane > (20 + elevation))
                    {
                        ChangeFltState(FLT_STAGE.TakeOff);
                        //bTakeOff = true;
                        bTimer = true;
                        takeOffTime = DateTime.Now.AddSeconds(-5);
                    }
                }
                if (fltStage > FLT_STAGE.TakeOff && speed < 40 && altitudePlane < (50 + elevation))
                {
                    //bLanded = true;
                    ChangeFltState(FLT_STAGE.Landed);
                    ChangeFltState(FLT_STAGE.TaxiAfterLanding);
                }
                if (fltStage == FLT_STAGE.TaxiBeforeTakeOff || fltStage == FLT_STAGE.TaxiAfterLanding)
                {
                    if (speed > 50)
                    {
                        btSpeed.Background = Brushes.LightBlue;
                        ChangeFltState(FLT_STAGE.RunUp);
                    }
                    else if (speed > 28)
                        btSpeed.Background = Brushes.Red;
                    else if (speed > 20)
                        btSpeed.Background = Brushes.LightCoral;
                    else if (speed > 2)
                    {
                        btSpeed.Background = Brushes.LightGreen;
                    }
                    else
                    {
                        btSpeed.Background = Brushes.LightGray;
                        btSpeed.Content = "0";
                    }
                }
            }
            else if (iRequest == idx_autoThrottle)
            {
                if (dValue == 1)
                    btAutoThrust.Background = Brushes.LightGreen;
                else
                    btAutoThrust.Background = Brushes.LightGray;
            }
            else if (iRequest == idx_parkingBrake)
            {
                //lblAutoThrust.Content = dValue.ToString();
                if (dValue == 1)
                    btBrake.Background = Brushes.Red;
                else
                    btBrake.Background = Brushes.LightGray;
            }
            else if (iRequest == idx_Vr) //set Vr
            {
                if (vRotate == 0)
                {
                    SetV1Speed((int)dValue);
                }
            }
            else if (iRequest == idx_altitude)
            {
                altitudePlane = (int)dValue;
                lblAltitude.Content = altitudePlane.ToString();
                if (!bSetElevation)
                {
                    elevation = altitudePlane;
                    lblElevation.Content = $"{altitudePlane}";
                    bSetElevation = true;
                }
                else if (fltStage == FLT_STAGE.TakeOff)
                {
                    if (altitudePlane > elevation + 999)
                    {
                        btSpeed.Background = Brushes.LightGreen;
                        ChangeFltState(FLT_STAGE.Climb);
                    }
                }
            }
            else if (iRequest == idx_throttlePosition)
            {
                tbPosition.Text = ((int)dValue).ToString();
            }
            else if (iRequest == idx_next_wp_alt)
            {
                lblNextWpAlt.Content = ((int)dValue).ToString();
            }
            else if (iRequest == idx_GroundAltitude)
            {
                int alt = (int)dValue;
                lblGroundAlt.Content = alt.ToString();
            }
            //       if (iRequest < 8)
            //      GetLabelForUid(iRequest).Content = dValue.ToString();
        }
        public void SetV1Speed(int x) // rotate
        {
            vRotate = x;
            tbV1Speed.Text = vRotate.ToString();
        }
//-----------------------------------------------------------------------------
        public void ReceiveSimConnectMessage()
        {
            MySim?.ReceiveMessage();
            btConnect.Background = Brushes.LightGreen;
        }

        /// <summary>
        /// Let's disconnect from SimConnect
        /// </summary>
        public void Disconnect()
        {
            if (MySim != null)
            {
                MySim.Dispose();
                MySim = null;
                this.Title = "Disconnected";
                btConnect.Background = Brushes.LightGray;
                btConnect.IsEnabled = false;
                btDisconnect.Background = Brushes.LightCoral;
            }
        }

        /// <summary>
        /// Handles Windows events directly, for example to grab the SimConnect connection
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="iMsg"></param>
        /// <param name="hWParam"></param>
        /// <param name="hLParam"></param>
        /// <param name="bHandled"></param>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hWnd, int iMsg, IntPtr hWParam, IntPtr hLParam, ref bool bHandled)
        {
            try
            {
                if (iMsg == WM_USER_SIMCONNECT)
                    ReceiveSimConnectMessage();
            }
            catch (IOException e)
            {
                TextBoxStatus.Text = e.ToString();
               // Disconnect();
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Once the window is loaded, let's hook to the WinProc
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var windowsSource = GetHWinSource();
            windowsSource.AddHook(WndProc);

          //  Connect();
        }

        /// <summary>
        /// Called while the window is closed, dispose SimConnect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
           Disconnect();
        }

        private void btExit_Clicked(object sender, RoutedEventArgs e)
        {
            if (MySim == null)
                this.Close();
            else
            {
                Disconnect();
                btDisconnect.Content = "Exit";
            }
        }

        private void btConnect_Clicked(object sender, RoutedEventArgs e)
        {
            if (MySim == null)
                Connect();
        }

        private void btSpeedReset(object sender, RoutedEventArgs e)
        {
            btSpeed.Background = Brushes.LightGray;
            btTimer.Background = Brushes.LightGray;
            bTimer = false;
        }

        private void btResetClick(object sender, RoutedEventArgs e)
        {
            Reset();
        }

        private void btAdvanceClicked(object sender, RoutedEventArgs e)
        {
            ChangeFltState(fltStage + 1);
        }

        private void btBackupClicked(object sender, RoutedEventArgs e)
        {
            ChangeFltState(fltStage - 1, true);
        }

        private void btDecV1Clicked(object sender, RoutedEventArgs e)
        {
            vRotate -= 1;
            SetV1Speed(vRotate);
        }

        private void btIncV1Clicked(object sender, RoutedEventArgs e)
        {
            vRotate += 1;
            SetV1Speed(vRotate);
        }

        private void btBackupClicked2(object sender, RoutedEventArgs e)
        {
            ChangeFltState(fltStage - 1, true);
        }

        private void btAdvanceClicked2(object sender, RoutedEventArgs e)
        {
            ChangeFltState(fltStage + 1);
        }
    }
}
