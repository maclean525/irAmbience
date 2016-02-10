using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using iRacingSdkWrapper;

namespace iRacingAmbience
{
    public class State
    {
        #region cars
        public enum CarState
        {
            NotInWorld = 0,
            EnteringWorld,
            ExitingStall,
            ExitingPits,
            EnteringTrack,
            OnTrack,
            ExitingTrack,
            EnteringPits,
            PassingEntryCones,
            PassingExitCones,
            EnteringStall,
            InStall,
            ChangingTires,
            ExitingWorld
        }

        private CarState CurrentState = CarState.NotInWorld;
        private CarState PreviousState = CarState.NotInWorld;

        bool BetweenCones = false; 
        bool PreviousBetweenCones = false; 
        bool InTransition = false;
        private DateTime LiftTimer = DateTime.Now;
        private DateTime DropTimer = DateTime.Now;
        private DateTime DropTime = DateTime.Now;
        private DateTime FuelTimer = DateTime.Now;
        int LiftCounter = 0;

        public void Initialize()
        {
            CurrentState = CarState.NotInWorld;
            PreviousState = CarState.NotInWorld;

            CurrentJackState = JackState.NotJacking;
            PreviousJackState = JackState.NotJacking;

            InTransition = false;
            BetweenCones = false;
            PreviousBetweenCones = false;
            Fueling = false;
            Repairing = false;
            FuelLevel = 0;
            PreviousFuelLevel = 0;
            LiftTimer = DateTime.Now;
            DropTimer = DateTime.Now;
            DropTime = DateTime.Now;
            FuelTimer = DateTime.Now;
            LiftCounter = 0;
        }
        
        // this method will compare the previous and current states and figure out what's going on in transition
        public CarState updateState (TrackSurfaces ts_state, bool between_cones)
        {
            // kick the states along a tick
            PreviousState = CurrentState;
            PreviousBetweenCones = BetweenCones;
            BetweenCones = between_cones;

            InTransition = false;
            switch (ts_state)
            {
                case TrackSurfaces.NotInWorld:
                {
                    // the state we care about is if previous was not, NotInWorld that means we just got kicked out of the world
                    if (PreviousState == CarState.NotInWorld)
                    {
                        // already out, yawn
                    }
                    else if (PreviousState == CarState.ExitingWorld)
                    {
                        // just left the world, now flip to out
                        setCurrentState(CarState.NotInWorld);
                    }
                    else
                    {
                        // in transition, leaving the world
                        InTransition = true;
                        setCurrentState(CarState.ExitingWorld);
                    }
                }
                break;

                case TrackSurfaces.OffTrack:
                {   
                    if ((PreviousState != CarState.OnTrack)&&(PreviousState != CarState.NotInWorld))
                        setCurrentState(CarState.OnTrack);
                }
                break;
                
                case TrackSurfaces.InPitStall:
                {
                    BetweenCones = true;

                    // transitions are dropping in the world and coming into the pits after approaching
                    if (PreviousState == CarState.NotInWorld)
                    {
                        // just dropped in
                        setCurrentState(CarState.EnteringWorld);
                        InTransition = true;
                    }
                    else if (PreviousState == CarState.EnteringWorld)
                    {
                        // just dropped in and now sitting in pits
                        setCurrentState(CarState.InStall);
                    }
                    else if (PreviousState == CarState.EnteringStall)
                    {
                        // now stopping in pit stall
                        setCurrentState(CarState.InStall);
                        InTransition = true;
                    }
                    else if (PreviousState == CarState.EnteringPits)
                    {
                        // driving up to the stall
                        setCurrentState(CarState.EnteringStall);
                        InTransition = true;
                    }
                    else
                    {
                        // best guess default
                        if (PreviousState != CarState.InStall)
                            setCurrentState(CarState.InStall);
                    }

                    // drop through to special handling for changing tires

                } 
                break;

                // complication here is we need to watch out for crossing the cone boundaries and capture that
                case TrackSurfaces.AproachingPits:
                {
                    // this state is both entering and exiting the pit lane
                    if (PreviousState == CarState.InStall)
                    {
                        // just exited the stall
                        setCurrentState(CarState.ExitingStall);
                        InTransition = true;
                    }
                    else if (PreviousState == CarState.ExitingStall)
                    {
                        // driving out the stall
                        setCurrentState(CarState.ExitingPits);
                        InTransition = true;
                    }
                    else if (PreviousState == CarState.OnTrack)
                    {
                        // entering the pits
                        setCurrentState(CarState.EnteringPits);
                        InTransition = true;
                    }
                    else if (PreviousState == CarState.EnteringPits)
                    {
                    }
                    else if (PreviousState == CarState.ExitingPits)
                    {
                    }
                    else if (PreviousState == CarState.PassingEntryCones)
                    {
                    }
                    else if (PreviousState == CarState.PassingExitCones)
                    {
                    }
                    else if (PreviousState == CarState.NotInWorld)
                    {
                        // entering the pits
                        setCurrentState(CarState.EnteringWorld);
                        InTransition = true;
                    }
                    else
                    {
                        // best guess default
                        if (PreviousState != CarState.ExitingPits)
                            setCurrentState(CarState.ExitingPits);
                    }

                    // the cones override the standard states this is a bit ugly but meh
                    // hitting the cones on entry
                    if ((PreviousBetweenCones == false) && (between_cones == true) && (PreviousState != CarState.NotInWorld))   // might be qualy dropping right in
                    {
                        setCurrentState(CarState.PassingEntryCones);
                        InTransition = true;
                        BetweenCones = true;
                    }

                    // passing the cones on exit
                    else if ((PreviousBetweenCones == true) && (between_cones == false))
                    {
                        setCurrentState(CarState.PassingExitCones);
                        InTransition = true;
                        BetweenCones = false;
                    }


                }
                break;

                case TrackSurfaces.OnTrack:
                {
                    if (PreviousState == CarState.ExitingPits)
                    {
                        // just exited the pits
                        setCurrentState(CarState.EnteringTrack);
                        InTransition = true;
                    }
                    else if (PreviousState == CarState.EnteringTrack)
                    {
                        // just exited the pits
                        setCurrentState(CarState.OnTrack);
                        InTransition = true;
                    }
                    else if (PreviousState == CarState.PassingExitCones)
                    {
                        // just exited the pits
                        setCurrentState(CarState.OnTrack);
                        InTransition = true;
                    }
                    else if (PreviousState == CarState.NotInWorld)
                    {
                        // entering the pits
                        setCurrentState(CarState.EnteringWorld);
                        InTransition = true;
                    }
                    else
                    {
                        // best guess default
                        if (PreviousState != CarState.OnTrack)
                        setCurrentState(CarState.OnTrack);
                    }
 
                } 
                break;

            }

            return CurrentState;
        }

        public void setCurrentState(CarState state)
        {
            CurrentState = state;
            Debug.WriteLine("state = " + state);
        }
        public CarState getCurrentState()
        {
            return CurrentState;
        }
        public CarState getPreviousState()
        {
            return PreviousState;
        }

        public bool isTransition()
        {
            return InTransition;
        }
        #endregion

        #region pits

        private bool Fueling;
        private bool Repairing;
        private float FuelLevel;
        private float PreviousFuelLevel;

        public enum JackState
        {
            NotJacking = 0,
            Lifting,
            InAir,
            Dropping
        }

        private JackState CurrentJackState = JackState.NotJacking;
        private JackState PreviousJackState = JackState.NotJacking;
        
        public void setCurrentJackState(JackState state)
        {
            CurrentJackState = state;
            Debug.WriteLine("set jackstate = " + state);
        }
        public JackState getCurrentJackState()
        {
            return CurrentJackState;
        }
        public JackState getPreviousJackState()
        {
            return PreviousJackState;
        }

        public bool getFueling() { return Fueling; }
        public void setFueling(bool j) { Fueling = j; }

        public bool getRepairing() { return Repairing; }
        public void setRepairing(bool j) { Repairing = j; }

        public void pitTick(float fuel_level)
        {
            // only check once every 1/2 second
            if (hasFuelTimerExpired())
            {
                startFuelTimer();
            
                // if fuel level going up then fueling
                double drounded = Math.Round((double)fuel_level, 1);
                float rounded = (float)drounded;

                if (FuelLevel == 0.0)
                    FuelLevel = rounded;

                if ((PreviousFuelLevel != FuelLevel) || (PreviousFuelLevel < 0.1))
                    PreviousFuelLevel = FuelLevel;
            
                FuelLevel = rounded;
                if (FuelLevel > PreviousFuelLevel)
                    Fueling = true;
                else
                    Fueling = false;
            }
        }

        public void resetFuel(float fuel)
        {
            double drounded = Math.Round((double)fuel, 1);
            float rounded = (float)drounded;

            PreviousFuelLevel = FuelLevel = rounded;
        }

        public void startLiftTimer()
        {
            LiftTimer = DateTime.Now;
        }

        public bool hasLiftTimerExpired()
        {
            DateTime now = DateTime.Now;
            TimeSpan diff = now - LiftTimer;
            if (diff.TotalMilliseconds > 500)
                return true;
            else
                return false;
        }

        public void startFuelTimer()
        {
            FuelTimer = DateTime.Now;
        }
        public bool hasFuelTimerExpired()
        {
            DateTime now = DateTime.Now;
            TimeSpan diff = now - FuelTimer;
            if (diff.TotalMilliseconds > 500)
                return true;
            else
                return false;
        }

        public void startDropTimer()
        {
            DropTimer = DateTime.Now;
        }

        public bool hasDropTimerExpired()
        {
            DateTime now = DateTime.Now;
            TimeSpan diff = now - DropTimer;
            if (diff.TotalMilliseconds > 1000)
                return true;
            else
                return false;
        }

        public void setDropTime()
        {
            DropTime = DateTime.Now;
        }

        public bool isCarDoneDropping()
        {
            DateTime now = DateTime.Now;
            TimeSpan diff = now - DropTime;
            if (diff.TotalMilliseconds > 1000)
                return true;
            else
                return false;
        }

        public void incrementLiftCounter()
        {
            LiftCounter++;
        }

        public bool isGenuineLift()
        {
            if (LiftCounter > 3)
                return true;
            else
                return false;
        }

        public void resetLiftCounter()
        {
            LiftCounter = 0;
        }

        #endregion
    }
}
