﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using iRSDKSharp;

namespace iRacingSdkWrapper
{
    /// <summary>
    /// Provides a useful wrapper of the iRacing SDK.
    /// </summary>
    public sealed class SdkWrapper
    {
        #region Fields

        internal readonly iRacingSDK sdk;
        private readonly SynchronizationContext context;
        private int waitTime;

        #endregion

        /// <summary>
        /// Creates a new instance of the SdkWrapper.
        /// </summary>
        public SdkWrapper(int frequency)
        {
            this.context = SynchronizationContext.Current;
            this.sdk = new iRacingSDK();
            this.EventRaiseType = EventRaiseTypes.CurrentThread;

            this.TelemetryUpdateFrequency = frequency;
            _DriverId = -1;
        }

        #region Properties

        /// <summary>
        /// Gets or sets how events are raised. Choose 'CurrentThread' to raise the events on the thread you created this object on (typically the UI thread), 
        /// or choose 'BackgroundThread' to raise the events on a background thread, in which case you have to delegate any UI code to your UI thread to avoid cross-thread exceptions.
        /// </summary>
        public EventRaiseTypes EventRaiseType { get; set; }

        private bool _IsRunning;
        /// <summary>
        /// Is the main loop running?
        /// </summary>
        public bool IsRunning { get { return _IsRunning; } }

        private bool _IsConnected;
        /// <summary>
        /// Is the SDK connected to iRacing?
        /// </summary>
        public bool IsConnected { get { return _IsConnected; } }

        private int _TelemetryUpdateFrequency;
        /// <summary>
        /// Gets or sets the number of times the telemetry info is updated per second. The default and maximum is 60 times per second.
        /// </summary>
        public int TelemetryUpdateFrequency
        {
            get { return _TelemetryUpdateFrequency; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("TelemetryUpdateFrequency must be at least 1.");
                if (value > 60)
                    throw new ArgumentOutOfRangeException("TelemetryUpdateFrequency cannot be more than 60.");

                _TelemetryUpdateFrequency = value;

                waitTime = (int) Math.Floor(1000f/value) - 1;
            }
        }

        private int _DriverId;
        /// <summary>
        /// Gets the Id (CarIdx) of yourself (the driver running this application).
        /// </summary>
        public int DriverId { get { return _DriverId; } }

        #endregion

        #region Methods

        public object GetData(string headerName)
        {
            if (!this.IsConnected) return null;

            return sdk.GetData(headerName);
        }

        public TelemetryValue<T> GetTelemetryValue<T>(string name)
        {
            return new TelemetryValue<T>(sdk, name);
        }

        /// <summary>
        /// Connects to iRacing and starts the main loop in a background thread.
        /// </summary>
        public void Start()
        {
            _IsRunning = true;

            Thread t = new Thread(Loop);
            t.Start();
        }

        /// <summary>
        /// Stops the main loop
        /// </summary>
        public void Stop()
        {
            _IsRunning = false;
        }

        private void Loop()
        {
            int lastUpdate = -1;

            while (_IsRunning)
            {
                // Check if we can find the sim
                if (sdk.IsConnected())
                {
                    if (!_IsConnected)
                    {
                        // If this is the first time, raise the Connected event
                        this.RaiseEvent(OnConnected, EventArgs.Empty);
                    }

                    _IsConnected = true;

                    // Get the session info string
                    string sessionInfo = sdk.GetSessionInfo();

                    // Parse out your own driver Id
                    if (this.DriverId == -1) _DriverId = int.Parse(YamlParser.Parse(sessionInfo, "DriverInfo:DriverCarIdx:"));

                    // Get the session time (in seconds) of this update
                    var time = (double) sdk.GetData("SessionTime");

                    // Is the session info updated?
                    int newUpdate = sdk.Header.SessionInfoUpdate;
                    if (newUpdate != lastUpdate)
                    {
                        lastUpdate = newUpdate;

                        // Raise the SessionInfoUpdated event and pass along the session info and session time.
                        var sessionArgs = new SessionInfoUpdatedEventArgs(sessionInfo, time);
                        this.RaiseEvent(OnSessionInfoUpdated, sessionArgs);
                    }

                    // Raise the TelemetryUpdated event and pass along the lap info and session time
                    var telArgs = new TelemetryUpdatedEventArgs(new TelemetryInfo(sdk), time);
                    this.RaiseEvent(OnTelemetryUpdated, telArgs);

                }
                else if (sdk.IsInitialized)
                {
                    // We have already been initialized before, so the sim is closing
                    this.RaiseEvent(OnDisconnected, EventArgs.Empty);

                    sdk.Shutdown();
                    _DriverId = -1;
                    lastUpdate = -1;
                    _IsConnected = false;
                }
                else
                {
                    _IsConnected = false;
                    _DriverId = -1;

                    //Try to find the sim
                    sdk.Startup();
                }

                // Sleep for a short amount of time until the next update is available
                if (_IsConnected)
                {
                    if (waitTime <= 0 || waitTime > 1000) waitTime = 15;
                    Thread.Sleep(waitTime);
                }
                else
                {
                    // Not connected yet, no need to check every 16 ms, let's try again in a second
                    Thread.Sleep(1000);
                }
            }

            sdk.Shutdown();
            _DriverId = -1;
            _IsConnected = false;
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when the sim outputs telemetry information (60 times per second).
        /// </summary>
        public event EventHandler<TelemetryUpdatedEventArgs> TelemetryUpdated;

        /// <summary>
        /// Event raised when the sim refreshes the session info (few times per minute).
        /// </summary>
        public event EventHandler<SessionInfoUpdatedEventArgs> SessionInfoUpdated;

        /// <summary>
        /// Event raised when the SDK detects the sim for the first time.
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// Event raised when the SDK no longer detects the sim (sim closed).
        /// </summary>
        public event EventHandler Disconnected;

        private void RaiseEvent<T>(Action<T> del, T e)
            where T : EventArgs
        {
            var callback = new SendOrPostCallback(obj => del(obj as T));

            if (context != null && this.EventRaiseType == EventRaiseTypes.CurrentThread)
            {
                // Post the event method on the thread context, this raises the event on the thread on which the SdkWrapper object was created
                context.Post(callback, e);
            }
            else
            {
                // Simply invoke the method, this raises the event on the background thread that the SdkWrapper created
                // Care must be taken by the user to avoid cross-thread operations
                callback.Invoke(e);
            }
        }

        private void OnSessionInfoUpdated(SessionInfoUpdatedEventArgs e)
        {
            var handler = this.SessionInfoUpdated;
            if (handler != null) handler(this, e);
        }

        private void OnTelemetryUpdated(TelemetryUpdatedEventArgs e)
        {
            var handler = this.TelemetryUpdated;
            if (handler != null) handler(this, e);
        }

        private void OnConnected(EventArgs e)
        {
            var handler = this.Connected;
            if (handler != null) handler(this, e);
        }

        private void OnDisconnected(EventArgs e)
        {
            var handler = this.Disconnected;
            if (handler != null) handler(this, e);
        }

        #endregion

        #region Enums

        public enum EventRaiseTypes
        {
            CurrentThread,
            BackgroundThread
        }

        #endregion

        #region Nested classes

        public class SdkUpdateEventArgs : EventArgs
        {
            public SdkUpdateEventArgs(double time)
            {
                _UpdateTime = time;
            }

            private readonly double _UpdateTime;
            /// <summary>
            /// Gets the time (in seconds) when this update occured.
            /// </summary>
            public double UpdateTime { get { return _UpdateTime; } }
        }

        public class SessionInfoUpdatedEventArgs : SdkUpdateEventArgs
        {
            public SessionInfoUpdatedEventArgs(string sessionInfo, double time) : base(time)
            {
                _SessionInfo = sessionInfo;
            }

            private readonly string _SessionInfo;
            /// <summary>
            /// Gets the session info string.
            /// </summary>
            public string SessionInfo { get { return _SessionInfo; } }
        }

        public class TelemetryUpdatedEventArgs : SdkUpdateEventArgs
        {
            public TelemetryUpdatedEventArgs(TelemetryInfo info, double time) : base(time)
            {
                _TelemetryInfo = info;
            }

            private readonly TelemetryInfo _TelemetryInfo;
            /// <summary>
            /// Gets the telemetry info object.
            /// </summary>
            public TelemetryInfo TelemetryInfo { get { return _TelemetryInfo; } }
        }

        #endregion
    }
}
