using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows.Forms;
using System.Drawing;
using System.Data;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using iRacingSdkWrapper;
using iRSDKSharp;
using irAmbience;
using irAmbience.Properties;
using IrrKlang;
using Microsoft.Win32;

/*
 cadillacctsvr
c6r
stockcars impala
stockcars2 chevy
stockcars2 chevy cot
latemodel
trucks silverado
stockcars chevyss
dallara
fordv8sc
stockcars fordfusion
fordgt
fr500s
hpdarx01c
legends ford34c
legends ford34c rookie
lotus79
mx5 cup
mx5 roadster
mclarenmp4
solstice
solstice rookie
radical sr8
rileydp
silvercrown
skmodified
rt2000
specracer
sprint
formulamazda
streetstock
skmodified tour
jettatdi
williamsfw31
*/

namespace iRacingAmbience
{
    public partial class MainForm : Form
    {
        private SdkWrapper Wrapper;
        private State CarState;
        private float PitPosition;
        SoundEngine SoundObject;
        Thread SoundThread;
        string SessionType;
        string CarName;
        private List<Driver> drivers;
        private bool isUpdatingDrivers;
        private int CurrentSessionNum;

        private float EnvAmbienceMasterVol;
        private float AnnouncerMasterVol;
        private float PitLaneMasterVol;
        private float CrowdMasterVol;
        private float GunsMasterVol;
        private float SirenMasterVol;
        private float RepairMasterVol;
        private float GarageMasterVol;
        private float MusicMasterVol;
        private float FuelingMasterVol;
        private float MasterVol;

        // defaults, some cars may get overridden
        private float VELZ_LIFT = 0.1F;
        private float VELZ_DROP = -0.3F;
        private bool IsOvalTrack = true;
        private bool IsOvalCar = true;
        private JackType JT = JackType.NASCAR;

        private enum JackType
        {
            NASCAR = 0,
            Street = 1,
            GT = 2,
            TBD
        };

        private void MainForm_Load(object sender, EventArgs e)
        {
            Hotkey hk = new Hotkey();
            hk.KeyCode = Keys.K;
            hk.Control = true;
            hk.Pressed += delegate { toggleMasterVolume(); };

            if (!hk.GetCanRegister(this))
            {
                Console.WriteLine("Whoops, looks like attempts to register will fail or throw an exception.");
            }
            else
            {
                hk.Register(this);
            }

            // start minimized?
            bool min = Settings.Default.StartMinimized;
            if (min)
            {
                notifyIcon1.BalloonTipText = "irAmbience minimized";
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(200);
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
            else
            {
                notifyIcon1.Visible = false;
            }
        }

        public MainForm()
        {
            Thread.CurrentThread.Name = "Telemetry Processing Thread";
            InitializeComponent();
            soundThreadStartup();  // don't progrees till we pick an entry from the dropdown
            loadLabels();
            loadVolumes();
            SoundObject.SetMasterVolume(MasterVol); // this has to be seperate due to the order of loading

            // initialize carstate object
            CarState = new State();
            CarState.Initialize();
            PitPosition = 0;
            SessionType = "Practice";

            // Create a new instance of the SdkWrapper object - TODO pass in update freq via GUI later
            Wrapper = new SdkWrapper(10);

            // Tell it to raise events on the current thread (don't worry if you don't know what a thread is)
            Wrapper.EventRaiseType = SdkWrapper.EventRaiseTypes.CurrentThread;

            // Attach some useful events so you can respond when they get raised
            Wrapper.Connected += wrapper_Connected;
            Wrapper.Disconnected += wrapper_Disconnected;
            Wrapper.SessionInfoUpdated += wrapper_SessionInfoUpdated;
            Wrapper.TelemetryUpdated += wrapper_TelemetryUpdated;

            drivers = new List<Driver>();

            // auto-start the telemetry wrapper
            if (!Wrapper.IsRunning)
            {
                Wrapper.Start();
                StatusChanged();
            }
        }

        private void setConnectionStatus(string image_file)
        {
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Request that the worker thread stop itself:
            SoundObject.RequestStop();

            // Use the Join method to block the current thread 
            // until the object's thread terminates.
            SoundThread.Join();
            Debug.WriteLine("main thread: Worker thread has terminated.");

            Wrapper.Stop();
        }

        private void StatusChanged()
        {
            if (Wrapper.IsConnected)
            {
                if (Wrapper.IsRunning)
                {
                    resetSound(true);
                }
                else
                {
                }
            }
            else
            {
                if (Wrapper.IsRunning)
                {
                }
                else
                {
                }
            }
        }

        // Event handler called when the sdk wrapper connects (eg, you start it, or the sim is started)
        private void wrapper_Connected(object sender, EventArgs e)
        {
            this.StatusChanged();
        }

        // Event handler called when the sdk wrapper disconnects (eg, the sim closes)
        private void wrapper_Disconnected(object sender, EventArgs e)
        {
            SoundObject.appExit();
            this.StatusChanged();
        }

        private void wrapper_SessionInfoUpdated(object sender, SdkWrapper.SessionInfoUpdatedEventArgs e)
        {
            // Indicate that we are updating the drivers list
            isUpdatingDrivers = true;

            // Parse the Drivers section of the session info into a list of drivers
            this.ParseDrivers(e.SessionInfo);

            Driver me = drivers.FirstOrDefault(d => d.Id == Wrapper.DriverId);

            try
            {
                string session_info = e.SessionInfo;
                string pos = YamlParser.Parse(session_info, "DriverInfo:DriverPitTrkPct:");

                if (pos != null)
                {
                    if (pos.Length > 0)
                    {
                        PitPosition = (float)Convert.ToDecimal(pos);
                    }
                }

                SessionType = YamlParser.Parse(session_info, "SessionInfo:SessionType:");
                
                if (me != null)
                    CarName = me.CarPath;
                else
                    CarName = YamlParser.Parse(session_info, "DriverInfo:CarPath:");

                string category = YamlParser.Parse(session_info, "WeekendInfo:Category:");
                if (category == "Oval")
                    IsOvalTrack = true;
                else
                    IsOvalTrack = false;

                switch (CarName)    // by default cars are oval so only overwrite the road ones.
                {
                    case "c6r":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "cadillacctsvr":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "dallara":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "fordgt":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "formulamazda":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "fr500s":
                        IsOvalCar = false;
                        JT = JackType.Street;
                        break;
                    case "hpdarx01c":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "jettatdi":
                        IsOvalCar = false;
                        JT = JackType.TBD;
                        break;
                    case "kiaoptima":
                        IsOvalCar = false;
                        JT = JackType.TBD;
                        break;
                    case "latemodel":
                        JT = JackType.NASCAR;
                        break;
                    case "lotus49":
                        IsOvalCar = false;
                        JT = JackType.Street;
                        break;
                    case "lotus79":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "mclarenmp4":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "mx5 cup":
                        JT = JackType.Street;
                        IsOvalCar = false;
                        break;
                    case "mx5 roadster":
                        JT = JackType.Street;
                        IsOvalCar = false;
                        break;
                    case "radical sr8":
                        JT = JackType.GT;
                        IsOvalCar = false;
                        break;
                    case "rileydp":
                        JT = JackType.GT;
                        IsOvalCar = false;
                        break;
                    case "rt2000":
                        IsOvalCar = false;
                        JT = JackType.Street;
                        break;
                    case "specracer":
                        IsOvalCar = false;
                        JT = JackType.Street;
                        break;
                    case "stockcars chevyss":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "stockcars fordfusion":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "stockcars impala":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "stockcars2 chevy":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "stockcars2 chevy cot":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "streetstock":
                        JT = JackType.Street;
                        IsOvalCar = false;
                        break;
                    case "trucks silverado":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "williamsfw31":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "legends ford34c":
                        IsOvalCar = true;
                        JT = JackType.TBD;
                        break;
                    case "legends ford34c rookie":
                        IsOvalCar = true;
                        JT = JackType.TBD;
                        break;
                    case "solstice":
                        IsOvalCar = true;
                        JT = JackType.TBD;
                        break;
                    case "solstice rookie":
                        IsOvalCar = true;
                        JT = JackType.TBD;
                        break;
                    case "fordv8sc":
                        IsOvalCar = false;
                        JT = JackType.GT;
                        break;
                    case "silvercrown":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "skmodified":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "skmodified tour":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    case "sprint":
                        IsOvalCar = true;
                        JT = JackType.NASCAR;
                        break;
                    default:
                        IsOvalCar = true;
                        JT = JackType.TBD;
                        break;
                }
            }
            catch(Exception)
            {
                Console.Write("Caught exception in SessionInfoUpdated");
            }
            isUpdatingDrivers = false;
         }


        // Parse the YAML DriverInfo section that contains information such as driver id, name, license, car number, etc.
        private void ParseDrivers(string sessionInfo)
        {
            // This string is used for every property of the driver
            // {0} is replaced by the driver ID
            // {1} is replaced by the property key
            // The result is a string like:         DriverInfo:Drivers:CarIdx:{17}CarNumber: 
            // which is the correct way to request the property 'CarNumber' from the driver with id 17.
            const string driverYamlPath = "DriverInfo:Drivers:CarIdx:{{{0}}}{1}:";

            int id = 0;
            Driver driver;

            var newDrivers = new List<Driver>();

            // Loop through drivers until none are found
            do
            {
                driver = null;

                // Try to get the UserName of the driver (because its the first value given)
                // If the UserName value is not found (name == null) then we found all drivers
                string name = YamlParser.Parse(sessionInfo, string.Format(driverYamlPath, id, "UserName"));
                if (name != null)
                {
                    // Find this driver in the list
                    // This strange " => " syntax is called a lambda expression and is short for a loop through all drivers
                    // Read as: select the first driver 'd', if any, whose Name is equal to name.
                    driver = drivers.FirstOrDefault(d => d.Name == name);

                    if (driver == null)
                    {
                        // Or create a new Driver if we didn't find him before
                        driver = new Driver();
                        driver.Id = id;
                        driver.Name = name;
                        driver.CustomerId = int.Parse(YamlParser.Parse(sessionInfo, string.Format(driverYamlPath, id, "UserID")));
                        driver.Number = YamlParser.Parse(sessionInfo, string.Format(driverYamlPath, id, "CarNumber"));
                        driver.ClassId = int.Parse(YamlParser.Parse(sessionInfo, string.Format(driverYamlPath, id, "CarClassID")));
                        driver.CarPath = YamlParser.Parse(sessionInfo, string.Format(driverYamlPath, id, "CarPath"));
                        driver.CarClassRelSpeed = int.Parse(YamlParser.Parse(sessionInfo, string.Format(driverYamlPath, id, "CarClassRelSpeed")));
                        driver.Rating = int.Parse(YamlParser.Parse(sessionInfo, string.Format(driverYamlPath, id, "IRating")));
                    }
                    newDrivers.Add(driver);

                    id++;
                }
            } while (driver != null);

            // Replace old list of drivers with new list of drivers and update the grid
            drivers.Clear();
            drivers.AddRange(newDrivers);
        }

        private void wrapper_TelemetryUpdated(object sender, SdkWrapper.TelemetryUpdatedEventArgs e)
        {
            if (isUpdatingDrivers) return;
            
            try
            {
                if (e.TelemetryInfo.IsInGarage.Value)
                {
                    // play garage sounds loop
                    // start repair if not playing already
                    if (!SoundObject.isGaragePlaying())
                    {
                        SoundObject.startGarage(GarageMasterVol);
                    }
                }
                else if (e.TelemetryInfo.IsReplayPlaying.Value)
                {
                    if (SoundObject.isGaragePlaying())
                        SoundObject.stopGarage();

                    processTelemetry(e); // we still have to do updates here so we see the transition into and out of the world.
                }
                else if (e.TelemetryInfo.IsOnTrack.Value)
                {
                    if (SoundObject.isGaragePlaying())
                        SoundObject.stopGarage();

                    processTelemetry(e);
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("Caught exception in TelemetryUpdated");
            }
        }

        private void processTelemetry(SdkWrapper.TelemetryUpdatedEventArgs e)
        {
            Driver me = drivers.FirstOrDefault(d => d.Id == Wrapper.DriverId);

            if (me == null)
                return;

            // Get your own CarIdx:
            int myId = me.Id;

            // where is the car on track   
            TrackSurfaces[] surfaces = e.TelemetryInfo.CarIdxTrackSurface.Value;
            TrackSurfaces mySurface = surfaces[myId];

            bool on_track = e.TelemetryInfo.IsOnTrack.Value;

            bool[] OnPitRoad = e.TelemetryInfo.CarIdxOnPitRoad.Value;
            bool between_cones = OnPitRoad[myId];

            CurrentSessionNum = e.TelemetryInfo.SessionNum.Value;

            // keep it simple, super-basic SM
            State.CarState cur = CarState.updateState(mySurface, between_cones);
            bool trans = CarState.isTransition();
            
            // need the fuel level early
            float fuel = e.TelemetryInfo.FuelLevel.Value;
            bool is_in_garage = e.TelemetryInfo.IsInGarage.Value;

            // the distance to the pit is the volume control for the pitlane ambience
            float lap_pct = e.TelemetryInfo.LapDistPct.Value;
            
            // not working - PitLaneVol = Math.Floor(PitPosition * 100.0 / lap_pct + 0.5);
            float pit_dist = 0L;
            if (lap_pct > PitPosition)
                pit_dist = lap_pct - PitPosition;
            else
                pit_dist = PitPosition - lap_pct;

            // calculate crowd volume
            float crowd_dist = 0L;
            if (lap_pct > 0.5F)
                crowd_dist = 1 - lap_pct;
            else
                crowd_dist = lap_pct;

            float pit_vol = PitLaneMasterVol - (5*pit_dist);        // scale better at 5x
            float crowd_vol = CrowdMasterVol - (crowd_dist);

            // TODO put this back in once I figure out the crazy session info stuff
            // scale the crowd volume based on session type
            if (SessionType == "Offline Testing")
                crowd_vol = 0;  // no spectators
            //else if (SessionType == "Practice")
            //    crowd_vol /= 3;  
            //else if (SessionType == "Qualifying")
            //    crowd_vol /= 2;  
            //else if (SessionType == "Race")
            //    crowd_vol /= 1;  
            
            if (pit_vol < 0) pit_vol = 0;
            if (crowd_vol < 0) crowd_vol = 0;

            // we just transitioned state so this is where we can invoke a new operation or stop a current one
            switch (cur)
            {
                case State.CarState.NotInWorld:
                    {
                        if (trans)
                        {
                            CarState.Initialize();   // reset all vars back to zero since we are out of the world
                        }

                        // TODO if in the garage how do we know?
                        if (is_in_garage)
                        {
                            if (!SoundObject.isGaragePlaying())
                                SoundObject.startGarage(1.0F);
                        }
                        else
                        {
                            if (!SoundObject.isMusicPlaying())
                                SoundObject.startMusic(MusicMasterVol);
                            else
                                SoundObject.setMusicVolume(MusicMasterVol);
                        }
                    }
                    break;
                case State.CarState.EnteringWorld:
                    {
                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic();
                        
                        CarState.setDropTime();
                        CarState.resetFuel(fuel);
                        SoundObject.startAnnouncer(AnnouncerMasterVol); 
                        SoundObject.startPitLane(PitLaneMasterVol);
                        SoundObject.startEnvironment(EnvAmbienceMasterVol);
                        SoundObject.startCrowd(CrowdMasterVol);
                    }
                    break;
                case State.CarState.ExitingStall:
                    {
                        if (!on_track)
                            return;

                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic();

                        // stop pit activity sounds
                        SoundObject.resumePitLane();
                        SoundObject.setPitVolume(pit_vol);
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                        SoundObject.stopGuns();
                        SoundObject.stopFueling();
                        SoundObject.stopRepair();
                        CarState.setCurrentJackState(State.JackState.NotJacking);
                    }
                    break;
                case State.CarState.ExitingPits:
                    {
                        if (!on_track)
                            return;
                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic();

                        SoundObject.setPitVolume(pit_vol);
                        SoundObject.resumePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                    }
                    break;
                case State.CarState.EnteringTrack:
                    {
                        if (!on_track)
                            return;
                        
                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic();

                        SoundObject.pausePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                    }
                    break;
                case State.CarState.OnTrack:
                    {
                        if (!on_track)
                            return;
                        
                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic(); 
                        
                        SoundObject.pausePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                    }
                    break;
                case State.CarState.ExitingTrack:
                    {
                        if (!on_track)
                            return;
                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic(); 
                        
                        SoundObject.pausePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                    }
                    break;
                case State.CarState.EnteringPits:
                    {
                        if (!on_track)
                            return;

                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic(); 
                        
                        SoundObject.setPitVolume(pit_vol);
                        SoundObject.resumePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                    }
                    break;
                case State.CarState.EnteringStall:
                    {
                        if (!on_track)
                            return;

                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic(); 
                        
                        SoundObject.setPitVolume(pit_vol); 
                        SoundObject.resumePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                    }
                    break;
                case State.CarState.InStall:
                    {
                        if (!on_track)
                            return;

                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic(); 
                        
                        SoundObject.setPitVolume(pit_vol);
                        SoundObject.resumePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);

                        float repair_left = e.TelemetryInfo.PitRepairLeft.Value;
                        float opt_repair_left = e.TelemetryInfo.PitOptRepairLeft.Value;
                        
                        // keep the carstate up to date with the fuel situation
                        CarState.pitTick(fuel);
                        bool fueling = CarState.getFueling();
                        if (fueling)
                        {
                            // fueling up
                            if (!SoundObject.isFuelingPlaying())
                                SoundObject.startFueling(FuelingMasterVol);
                            Debug.WriteLine("Fueling!");
                        }
                        else
                        {
                            // stop fuel sound if it's playing since no longer fueling
                            if (SoundObject.isFuelingPlaying())
                                SoundObject.stopFueling();
                        }
                        if ((repair_left > 0) || (opt_repair_left > 0))
                        {
							// start repair if not playing already
                            if (!SoundObject.isRepairPlaying())
                                SoundObject.startRepair(RepairMasterVol);  
						}
						else
						{
							// stop repair if it's playing
                            if (SoundObject.isRepairPlaying())
                                SoundObject.stopRepair();
						}

                        // only look for jacking and tire changing if more than 1 second has passed since being dropped into world
                        if (CarState.isCarDoneDropping())
                        {
                            float velz = e.TelemetryInfo.VelocityZ.Value;   // shows velz when jacking
                            float lat = e.TelemetryInfo.LatAccel.Value;
                            float roll = e.TelemetryInfo.Roll.Value;
                            // TOGO LOG - string buf = velz.ToString() + "," + lat.ToString() + "," + roll.ToString();
                            //TODO LOG - FileLogger.Instance.Write(buf);

                            // check for different events based on current state
                            State.JackState jack_state = CarState.getCurrentJackState();

                            switch (jack_state)
                            {
                                case State.JackState.NotJacking:
                                    {
                                        bool expired = CarState.hasDropTimerExpired();
                                        if (expired)
                                        {
                                            // check for lifting ONLY, not dropping - note this value may have to change per car
                                            if (JT == JackType.TBD)
                                            {
                                            }
                                            else if (JT == JackType.GT)
                                            {
                                                if (velz > VELZ_LIFT)
                                                {
                                                    CarState.incrementLiftCounter();
                                                    if (CarState.isGenuineLift())
                                                    {
                                                        CarState.resetLiftCounter();
                                                        CarState.setCurrentJackState(State.JackState.Lifting);
                                                        CarState.startLiftTimer();
                                                        Debug.WriteLine("lifting");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                float neg_f = -0.05F;
                                                float pos_f = 0.05F;

                                                // look at roll, only in roll while on jacks
                                                if ((roll < neg_f) || (roll > pos_f))
                                                {
                                                    CarState.incrementLiftCounter();
                                                    if (CarState.isGenuineLift())
                                                    {
                                                        CarState.resetLiftCounter();
                                                        CarState.setCurrentJackState(State.JackState.Lifting);
                                                        CarState.startLiftTimer();
                                                        Debug.WriteLine("lifting");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case State.JackState.Lifting:
                                    {
                                        // looking for lift timer to expire then flipping state to inair
                                        bool expired = CarState.hasLiftTimerExpired();
                                        if (expired)
                                        {
                                            CarState.setCurrentJackState(State.JackState.InAir);
                                            Debug.WriteLine("in air");
                                        }
                                    }
                                    break;
                                case State.JackState.InAir:
                                    {
                                        // GUNS ARE GO
                                        if (!SoundObject.areGunsPlaying())
                                            SoundObject.startGuns(GunsMasterVol, IsOvalCar);    

                                        // watch for car being dropped - note this value may have to change per car
                                        if (JT == JackType.TBD)
                                        {
                                        }
                                        else if (JT == JackType.GT)
                                        {
                                            if (velz < VELZ_DROP)
                                            {
                                                CarState.setCurrentJackState(State.JackState.Dropping);
                                                Debug.WriteLine("dropping");
                                            }
                                        }
                                        else
                                        {
                                            // if roll is within the window then it's back on ground
                                            if ((roll > -0.05F) && (roll < 0.05F))
                                            {
                                                CarState.setCurrentJackState(State.JackState.Dropping);
                                                Debug.WriteLine("dropping");
                                            }
                                        }
                                    }
                                    break;
                                case State.JackState.Dropping:
                                    {
                                        SoundObject.stopGuns();
                                        CarState.setCurrentJackState(State.JackState.NotJacking);
                                        CarState.startDropTimer();
                                        Debug.WriteLine("dropped");
                                    }
                                    break;
                                default:
                                    Debug.WriteLine("Invalid Jack State");
                                    break;
                            }

                        }
                  
                    }
                    break;
                case State.CarState.PassingEntryCones:
                    {
                        if (!on_track)
                            return;

                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic();
                        
                        // play pit lane horn only o road courses
                        if (trans)
                        {
                            if ((!SoundObject.isSirenPlaying())&&(!IsOvalTrack))
                                SoundObject.startSiren(SirenMasterVol);	
                        }
                        SoundObject.setPitVolume(pit_vol);
                        SoundObject.resumePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                    }
                    break;
                case State.CarState.PassingExitCones:
                    {
                        if (!on_track)
                            return;
                        
                        // kill external sounds
                        SoundObject.stopGarage();
                        SoundObject.stopMusic();

                        SoundObject.setPitVolume(pit_vol);
                        SoundObject.resumePitLane();
                        SoundObject.setCrowdVolume(crowd_vol);
                        SoundObject.setAnnouncerVolume(AnnouncerMasterVol);
                        SoundObject.setEnvAmbienceVolume(EnvAmbienceMasterVol);
                    }
                    break;
                case State.CarState.ExitingWorld:
                    {
                        // stop all streams
                        SoundObject.stopAnnouncer();
                        SoundObject.stopPitLane();
                        SoundObject.stopEnvironment();
                        SoundObject.stopCrowd();
                        SoundObject.stopGuns();
                        SoundObject.stopSiren();
                        SoundObject.stopRepair();
                        SoundObject.stopFueling();
                    }
                    break;
                default:
                    {
                        Debug.WriteLine("Invalid CarState");
                    }
                    break;
            };
        }

        public bool soundThreadStartup()
        {
            // populate device list, pulling default from config file
            ISoundDeviceList sdl = new IrrKlang.ISoundDeviceList(IrrKlang.SoundDeviceListType.PlaybackDevice);
            //Add each device to a combo box.
            for (int i = 0; i < sdl.DeviceCount; i++)
            {
                string desc = sdl.getDeviceDescription(i);
                cbDevices.Items.Add(desc);
            }

            // take the value in the config file and set the drop-down to that
            cbDevices.SelectedIndex = Settings.Default.DeviceID;
            
            // fire up sound processing thread
            SoundObject = new SoundEngine();
            SoundThread = new Thread(SoundObject.Run);
            // Start the worker thread.
            SoundThread.Start();
            Debug.WriteLine("Telemetry processing thread: Starting sound thread...");
            while (!SoundThread.IsAlive);

            if (cbDevices.SelectedIndex == -1)
            {
                SoundObject.Initialize("0");  // using the default
            }
            else
            {
                string dev = sdl.getDeviceID(cbDevices.SelectedIndex);
                SoundObject.Initialize(dev);  // using the selected value
            }
            return true;
        }

        private void loadLabels()
        {
            bool MasterOn = Settings.Default.Master;
            bool MusicOn = Settings.Default.Music;
            bool PitLaneOn = Settings.Default.PitLaneAmbience;
            bool AnnouncerOn = Settings.Default.Announcer;
            bool CrowdOn = Settings.Default.CheeringCrowd;
            bool EnvAmbienceOn = Settings.Default.EnvironmentalAmbience;
            bool SirenOn = Settings.Default.PitLaneSirens;
            bool GunsOn = Settings.Default.WheelGuns;
            bool RepairOn = Settings.Default.Repair;
            bool GarageOn = Settings.Default.Garage;
            bool FuelOn = Settings.Default.Fueling;

            if (MasterOn)
            {
                lblMaster.Text = "ON";
                SoundObject.setMasterOn(true);
            }
            if (MusicOn)
            {
                lblmusic.Text = "ON";
                SoundObject.setMusicOn(true);
            }
            if (PitLaneOn)
            {
                lblPitAmbience.Text = "ON";
                SoundObject.setPitLaneOn(true);
            }
            if (AnnouncerOn)
            {
                lblAnnouncer.Text = "ON";
                SoundObject.setAnnouncerOn(true);
            }
            if (CrowdOn)
            {
                lblCrowd.Text = "ON";
                SoundObject.setCrowdOn(true);
            }
            if (EnvAmbienceOn)
            {
                lblAmbience.Text = "ON";
                SoundObject.setEnvAmbienceOn(true);
            }
            if (SirenOn)
            {
                lblPitSirens.Text = "ON";
                SoundObject.setSirenOn(true);
            }
            if (GunsOn)
            {
                lblWheelsGuns.Text = "ON";
                SoundObject.setGunsOn(true);
            }
            if (RepairOn)
            {
                lblRepair.Text = "ON";
                SoundObject.setRepairOn(true);
            }
            if (GarageOn)
            {
                lblGarage.Text = "ON";
                SoundObject.setGarageOn(true);
            }
            if (FuelOn)
            {
                lblFueling.Text = "ON";
                SoundObject.setFuelingOn(true);
            }      
        }

        private void loadVolumes()
        {
            musicVol.Value = Settings.Default.MusicVolume;
            pitAmbienceVol.Value = Settings.Default.PitAmbienceVolume;
            sirenVol.Value = Settings.Default.SirensVolume;
            gunsVol.Value = Settings.Default.GunsVolume;
            AnnouncerVol.Value = Settings.Default.AnnouncerVolume;
            envVol.Value = Settings.Default.EnvironmentVolume;
            crowdVol.Value = Settings.Default.CrowdVolume;
            repairVol.Value = Settings.Default.RepairVolume;
            garageVol.Value = Settings.Default.GarageVolume;
            fuelingVol.Value = Settings.Default.FuelingVolume;
            masterVol.Value = Settings.Default.MasterVolume;

            setEnvAmbienceMasterVol(envVol.Value);
            setAnnouncerMasterVol(AnnouncerVol.Value);
            setPitLaneMasterVol(pitAmbienceVol.Value);
            setCrowdMasterVol(crowdVol.Value);
            setGunsMasterVol(gunsVol.Value);
            setSirenMasterVol(sirenVol.Value);
            setMusicMasterVol(musicVol.Value);
            setRepairMasterVol(repairVol.Value);
            setGarageMasterVol(garageVol.Value);
            setFuelingMasterVol(fuelingVol.Value);
            setMasterVol(masterVol.Value);
        }

        private void cbDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            resetSound(false);
        }

        private void resetSound(bool on_connection)
        {
            if (!on_connection)
            {
                int cur_dev = Settings.Default.DeviceID;
                int new_dev = cbDevices.SelectedIndex;

                if ((new_dev != cur_dev) && (new_dev != -1))
                {
                    // update config file for next time
                    Settings.Default.DeviceID = new_dev;
                    Settings.Default.Save();

                    // get the proper dev id from the sound device list
                    // populate device list, pulling default from config file
                    ISoundDeviceList sdl = new IrrKlang.ISoundDeviceList(IrrKlang.SoundDeviceListType.PlaybackDevice);
                    string dev_id = sdl.getDeviceID(new_dev);

                    // kill curent sound process and start a new one with the new device
                    SoundObject.appExit();
                    SoundObject.Initialize(dev_id);
                    SoundObject.SetMasterVolume(MasterVol);
                }
            }
            else
            {
                ISoundDeviceList sdl = new IrrKlang.ISoundDeviceList(IrrKlang.SoundDeviceListType.PlaybackDevice);
                string dev_id = sdl.getDeviceID(Settings.Default.DeviceID);
                SoundObject.Initialize(dev_id);
                SoundObject.SetMasterVolume(MasterVol);
            }
        }

        private void lblMaster_Click(object sender, EventArgs e)
        {
            toggleMasterVolume();
        }

        private void toggleMasterVolume()
        {
            bool flag = false;
            if (lblMaster.Text == "ON")
                lblMaster.Text = "OFF";
            else
            {
                lblMaster.Text = "ON";
                flag = true;
            }

            SoundObject.SetMasterVolume(MasterVol);
            SoundObject.setMasterOn(flag);
            Settings.Default.Master = flag;
            Settings.Default.Save();
        }

        private void lblmusic_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblmusic.Text == "ON")
                lblmusic.Text = "OFF";
            else
            {
                lblmusic.Text = "ON";
                flag = true;
            }

            SoundObject.setMusicOn(flag); 
            Settings.Default.Music = flag;
            Settings.Default.Save();
         }

        private void lblPitAmbience_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblPitAmbience.Text == "ON")
                lblPitAmbience.Text = "OFF";
            else
            {
                lblPitAmbience.Text = "ON";
                flag = true;
            }

            SoundObject.setPitLaneOn(flag);
            Settings.Default.PitLaneAmbience = flag;
            Settings.Default.Save();
        }

        private void lblPitSirens_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblPitSirens.Text == "ON")
                lblPitSirens.Text = "OFF";
            else
            {
                lblPitSirens.Text = "ON";
                flag = true;
            }

            SoundObject.setSirenOn(flag); 
            Settings.Default.PitLaneSirens = flag;
            Settings.Default.Save();
        }

        private void lblWheelsGuns_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblWheelsGuns.Text == "ON")
                lblWheelsGuns.Text = "OFF";
            else
            {
                lblWheelsGuns.Text = "ON";
                flag = true;
            }

            SoundObject.setGunsOn(flag);
            Settings.Default.WheelGuns = flag;
            Settings.Default.Save();
        }

        private void lblAnnouncer_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblAnnouncer.Text == "ON")
                lblAnnouncer.Text = "OFF";
            else
            {
                lblAnnouncer.Text = "ON";
                flag = true;
            }

            SoundObject.setAnnouncerOn(flag);
            Settings.Default.Announcer = flag;
            Settings.Default.Save();
        }

        private void lblAmbience_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblAmbience.Text == "ON")
                lblAmbience.Text = "OFF";
            else
            {
                lblAmbience.Text = "ON";
                flag = true;
            }

            SoundObject.setEnvAmbienceOn(flag);
            Settings.Default.EnvironmentalAmbience = flag;
            Settings.Default.Save();
        }

        private void lblCrowd_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblCrowd.Text == "ON")
                lblCrowd.Text = "OFF";
            else
            {
                lblCrowd.Text = "ON";
                flag = true;
            }

            SoundObject.setCrowdOn(flag);
            Settings.Default.CheeringCrowd = flag;
            Settings.Default.Save();
        }

        private void lblRepair_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblRepair.Text == "ON")
                lblRepair.Text = "OFF";
            else
            {
                lblRepair.Text = "ON";
                flag = true;
            }

            SoundObject.setRepairOn(flag);
            Settings.Default.Repair = flag;
            Settings.Default.Save();
        }

        private void lblGarage_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblGarage.Text == "ON")
                lblGarage.Text = "OFF";
            else
            {
                lblGarage.Text = "ON";
                flag = true;
            }

            SoundObject.setGarageOn(flag);
            Settings.Default.Garage = flag;
            Settings.Default.Save();
        }

        private void lblFueling_Click(object sender, EventArgs e)
        {
            bool flag = false;
            if (lblFueling.Text == "ON")
                lblFueling.Text = "OFF";
            else
            {
                lblFueling.Text = "ON";
                flag = true;
            }

            SoundObject.setFuelingOn(flag);
            Settings.Default.Fueling = flag;
            Settings.Default.Save();
        }

        private void masterVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.MasterVolume = masterVol.Value;
            Settings.Default.Save();
            setMasterVol(masterVol.Value);
        }

        private void musicVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.MusicVolume = musicVol.Value;
            Settings.Default.Save();
            setMusicMasterVol(musicVol.Value);
        }

        private void pitAmbienceVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.PitAmbienceVolume = pitAmbienceVol.Value;
            Settings.Default.Save();
            setPitLaneMasterVol(pitAmbienceVol.Value);
        }

        private void sirenVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.SirensVolume = sirenVol.Value;
            Settings.Default.Save();
            setSirenMasterVol(sirenVol.Value);
        }

        private void gunsVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.GunsVolume = gunsVol.Value;
            Settings.Default.Save();
            setGunsMasterVol(gunsVol.Value);
        }

        private void fuelingVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.FuelingVolume = fuelingVol.Value;
            Settings.Default.Save();
            setFuelingMasterVol(fuelingVol.Value);
        }

        private void AnnouncerVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.AnnouncerVolume = AnnouncerVol.Value;
            Settings.Default.Save();
            setAnnouncerMasterVol(AnnouncerVol.Value);
        }

        private void envVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.EnvironmentVolume = envVol.Value;
            Settings.Default.Save();
            setEnvAmbienceMasterVol(envVol.Value);
        }

        private void crowdVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.CrowdVolume = crowdVol.Value;
            Settings.Default.Save();
            setCrowdMasterVol(crowdVol.Value);
        }

        private void repairVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.RepairVolume = repairVol.Value;
            Settings.Default.Save();
            setRepairMasterVol(repairVol.Value);
        }

        private void garageVol_Scroll(object sender, EventArgs e)
        {
            Settings.Default.GarageVolume = garageVol.Value;
            Settings.Default.Save();
            setGarageMasterVol(garageVol.Value);
        }

        public void setEnvAmbienceMasterVol(int vol)
        {
            float fvol = vol;
            EnvAmbienceMasterVol = fvol / 10;
        }
        public void setAnnouncerMasterVol(int vol)
        {
            float fvol = vol;
            AnnouncerMasterVol = fvol / 10;
        }
        public void setPitLaneMasterVol(int vol)
        {
            float fvol = vol;
            PitLaneMasterVol = fvol / 10;
        }
        public void setCrowdMasterVol(int vol)
        {
            float fvol = vol;
            CrowdMasterVol = fvol / 10;
        }
        public void setGunsMasterVol(int vol)
        {
            float fvol = vol;
            GunsMasterVol = fvol / 10;
        }
        public void setSirenMasterVol(int vol)
        {
            float fvol = vol;
            SirenMasterVol = fvol / 10;
        }
        public void setRepairMasterVol(int vol)
        {
            float fvol = vol;
            RepairMasterVol = fvol / 10;
        }
        public void setGarageMasterVol(int vol)
        {
            float fvol = vol;
            GarageMasterVol = fvol / 10;
        }
        public void setMusicMasterVol(int vol)
        {
            float fvol = vol;
            MusicMasterVol = fvol / 10;
        }
        public void setFuelingMasterVol(int vol)
        {
            float fvol = vol;
            FuelingMasterVol = fvol / 10;
        }
        public void setMasterVol(int vol)
        {
            float fvol = vol;
            MasterVol = fvol / 10;

            if (lblMaster.Text == "OFF")
                MasterVol = 0;  // mute

            SoundObject.SetMasterVolume(MasterVol);
        }

        public void setAllMasterVolumes(int vol)
        {
            setMasterVol(vol);
            setEnvAmbienceMasterVol(vol);
            setAnnouncerMasterVol(vol);
            setPitLaneMasterVol(vol);
            setCrowdMasterVol(vol);
            setGunsMasterVol(vol);
            setSirenMasterVol(vol);
            setRepairMasterVol(vol);
            setGarageMasterVol(vol);
            setMusicMasterVol(vol);
            setFuelingMasterVol(vol);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            notifyIcon1.BalloonTipText = "irAmbience minimized to tray";

            if (FormWindowState.Minimized == this.WindowState)
            {
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(200);
                this.Hide();
            }
            else if (FormWindowState.Normal == this.WindowState)
            {
                notifyIcon1.Visible = false;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void toggleStartOnWindowsLoad(bool add)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(
                       @"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (null != key)
            {
                if (add)
                {
                    key.SetValue("irAmbience", "\"" + Application.ExecutablePath + "\"");
                }
                else
                    key.DeleteValue("irAmbience");

                key.Close();
            }
        }

        private void pbSettings_Click(object sender, EventArgs e)
        {

        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            string startup = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "irAmbience", null);
            if (startup != null)
            {
                autoStartWithWindowsToolStripMenuItem.Checked = true;
            }
            else
            {
                autoStartWithWindowsToolStripMenuItem.Checked = false;
            }

            startMinimizedToolStripMenuItem.Checked = Settings.Default.StartMinimized;
        }

        private void autoStartWithWindowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem check = (ToolStripMenuItem)sender;
            toggleStartOnWindowsLoad(!check.Checked);   // ! is on purpose, we want to flip the value
        }

        private void loadOnWindowsStartup()
        {
        }

        private void startMinimizedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem check = (ToolStripMenuItem)sender;
            toggleMinimized(!check.Checked);
        }

        private void toggleMinimized(bool status)
        {
            Settings.Default.StartMinimized = status;
            Settings.Default.Save();
        }

    }

}
