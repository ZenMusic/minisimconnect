using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Contains the list of all the SimConnect properties we will read, the unit is separated by coma by our own code.
        /// </summary>
        Dictionary<int, string> simConnectProperties = new Dictionary<int, string>
        {
            {1,"AUTOPILOT MASTER,Bool" },
            {2,"AUTOPILOT APPROACH HOLD,Bool" },
            {3,"GENERAL ENG THROTTLE LEVER POSITION:index, Percent" },
            {4,"BRAKE PARKING POSITION,Position" },
            {5,"AIRSPEED INDICATED,knots" },
            {6,"AUTOTHROTTLE ACTIVE,boolean" },
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

        SimConnect sim;

        /// <summary>
        ///  Direct reference to the window pointer
        /// </summary>
        /// <returns></returns>
        protected HwndSource GetHWinSource()
        {
            return PresentationSource.FromVisual(this) as HwndSource;
        }
        
        /// <summary>
        /// Returns a label based on a uid number
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        private Label GetLabelForUid(int uid)
        {
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
            timer.Tick += Timer_Tick; ;
            timer.Start();
        }
        DateTime takeOffTime;
        TimeSpan ts;
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (sim != null)
            {
                try
                {
                    foreach (var toConnect in simConnectProperties)
                        sim.RequestDataOnSimObjectType((DUMMYENUM)toConnect.Key, (DUMMYENUM)toConnect.Key, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                }
                catch
                {
                    Disconnect();
                }
            }
            if (bTakeOff && bTimer)
            {
                ts = DateTime.Now - takeOffTime;
                btAtOff.Content = ts.ToString();
                if (ts.TotalSeconds > 100)
                {
                    btAtOff.Background = Brushes.Red;
                }
            }
        }
        bool bTimer = true;
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
                sim.AddToDataDefinition((DUMMYENUM)toConnect.Key, values[0], values[1], SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                GetLabelForUid(100 + toConnect.Key).Content = values[1];
                /// IMPORTANT: Register it with the simconnect managed wrapper marshaller
                /// If you skip this step, you will only receive a uint in the .dwData field.
                sim.RegisterDataDefineStruct<double>((DUMMYENUM)toConnect.Key);
            }
        }

        bool bActiveV4 = false;
        FLT_STAGE fltStage = FLT_STAGE.none;

        public enum FLT_STAGE
        {
            Start,
            TaxiBeforeTakeOff,
            TakeOff,  // first time over > 40 K
            Climb,
            Cruize,
            Descent,
            Approach,
            Landed,  //   < 40 K
            GoAround,
            TaxiAfterLanding,
            Parked,
            none
        }
        public FLT_STAGE ChangeFltState(FLT_STAGE newState)
        {
            DisplayState(newState);
            if (newState == FLT_STAGE.Landed)
            {
                newState = FLT_STAGE.TaxiAfterLanding;
            }
            //
            fltStage = newState;
            DisplayState(newState);
            //tbAtState.Text = newState.ToString();
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
            if (sim != null)
                return;
            /// The constructor is similar to SimConnect_Open in the native API
            try
            {
                // Pass the self defined ID which will be returned on WndProc
                sim = new SimConnect(this.Title, GetHWinSource().Handle, WM_USER_SIMCONNECT, null, 0);
                sim.OnRecvOpen += Sim_OnRecvOpen;
                sim.OnRecvQuit += Sim_OnRecvQuit;
                sim.OnRecvSimobjectDataBytype += Sim_OnRecvSimobjectDataBytype;
            }
            catch
            {
                sim = null;
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
        bool bTakeOff = false;
        bool bLanded = false;
        bool Vrotate = false;
        private void Sim_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            int iRequest = (int)data.dwRequestID;
            double dValue = (double)data.dwData[0];
            if (iRequest == 5)
            {
                int speed = Convert.ToInt32(dValue);
                btSpeed.Content = speed.ToString();
                if (!bTakeOff)
                {
                    if (speed > 40)
                    {
                        bTakeOff = true;
                        bTimer = true;
                        takeOffTime = DateTime.Now.AddSeconds(-5);
                    }                    
                }
                if (!Vrotate && speed > 140)
                {
                    Vrotate = true;
                    btSpeed.Background = Brushes.Green;
                }
                else if (bTakeOff && speed < 35)
                    bLanded = true;
                if (bLanded)
                {
                    if (speed > 28)
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
            else if (iRequest == 6)
            {
                if (dValue == 1)
                    btAutoThrust.Background = Brushes.Red;
                else
                    btAutoThrust.Background = Brushes.LightGray;
            }
            else if (iRequest == 4)
            {
                if (dValue == 1)
                    btBrake.Background = Brushes.Red;
                else
                    btBrake.Background = Brushes.LightGray;
            }
            else if (iRequest== 3)
            {
                btAutoThrust.Content = dValue.ToString();
            }
            GetLabelForUid(iRequest).Content = dValue.ToString();
        }
//-----------------------------------------------------------------------------
        public void ReceiveSimConnectMessage()
        {
            sim?.ReceiveMessage();
        }

        /// <summary>
        /// Let's disconnect from SimConnect
        /// </summary>
        public void Disconnect()
        {
            if (sim != null)
            {
                sim.Dispose();
                sim = null;
                this.Title = "Disconnected";
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
            catch
            {
                Disconnect();
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
            if (sim == null)
                this.Close();
            else
            {
                Disconnect();
                btDisconnect.Content = "Exit";
            }
        }

        private void btConnect_Clicked(object sender, RoutedEventArgs e)
        {
            if (sim == null)
                Connect();
        }

        private void btSpeedReset(object sender, RoutedEventArgs e)
        {
            btSpeed.Background = Brushes.LightGray;
            btAtOff.Background = Brushes.LightGray;
            bTimer = false;
        }        
    }
}
