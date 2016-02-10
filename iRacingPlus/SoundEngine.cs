using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.IO;
using IrrKlang;

namespace irAmbience
{
    class SoundEngine
    {
        private const string SoundPath = "sounds/";
        private const uint SOUND_MAX = 4294967295;
        private volatile bool ShutdownThread = false;
        private static ISoundEngine Engine;

        // list of possible sounds that can be playing
        private ISound EnvAmbience;
        private ISound Announcer;
        private ISound PitLane;
        private ISound Crowd;
        private ISound Guns;
		private ISound Siren;
		private ISound Repair;
        private ISound Garage;
        private ISound Music;
        private ISound Fueling;

        private bool EnvAmbienceOn = false;
        private bool AnnouncerOn = false;
        private bool PitLaneOn = false;
        private bool CrowdOn = false;
        private bool GunsOn = false;
        private bool SirenOn = false;
        private bool RepairOn = false;
        private bool GarageOn = false;
        private bool MusicOn = false;
        private bool FuelingOn = false;
        private bool MasterOn = false;

        public void Run()
        {
            while (!ShutdownThread)
            {
                Thread.Sleep(1);   // don't bogart the CPU
            }
            StopAllSounds();
            Debug.WriteLine("sound engine thread: terminating gracefully.");
        }

        public void RequestStop()
        {
            ShutdownThread = true;
        }

        public void Initialize(string device_id)
        {
            Engine = new ISoundEngine(SoundOutputDriver.AutoDetect,
                                      SoundEngineOptionFlag.DefaultOptions, 
                                      device_id);
        }

        public void SetMasterVolume(float volume)
        {
            if (null != Engine)
                Engine.SoundVolume = volume;
        }

        public bool startAnnouncer(float volume)
        {
            if (AnnouncerOn)
            {
				string filename = getRandomFile(SoundPath, "announcer");
                Announcer = Engine.Play2D(filename, true, true);
                if (null == Announcer)
                    return false;
                Announcer.Volume = volume;
                Announcer.Paused = false;
                return true;
            }
            else return false;
        }

        public bool startPitLane(float volume)
        {
            if (PitLaneOn)
            {
				string filename = getRandomFile(SoundPath, "pit-ambience");
                PitLane = Engine.Play2D(filename, true, true);
                if (null == PitLane)
                    return false;
                PitLane.Volume = volume;
                PitLane.Paused = false;
                return true;
            }
            else return false;
        }

        public void setPitVolume(float volume)
        {
            if (null != PitLane)
                PitLane.Volume = volume;
        }

        public void pausePitLane()
        {
            if (null != PitLane)
            {
                if (PitLane.Paused == false)
                {
                    fadeOut(PitLane);
                    PitLane.Paused = true;
                }
            }
        }

        public void resumePitLane()
        {
            if ((null != PitLane)&&PitLaneOn)
            {
                if (PitLane.Paused == true)
                {
                    PitLane.Paused = false;
                    fadeIn(PitLane);
                }
            }
        }


        public bool startEnvironment(float volume)
        {
            if (EnvAmbienceOn)
            {
   				string filename = getRandomFile(SoundPath, "env-ambience");
                EnvAmbience = Engine.Play2D(filename, true, true);
                if (null == EnvAmbience)
                    return false;
                EnvAmbience.Volume = volume;
                EnvAmbience.Paused = false;
                return true;
            }
                else return false;
        }

        public bool stopAnnouncer()
        {
            if (null != Announcer)
            {
                fadeOut(Announcer);
                Announcer.Stop();
                return true;
            }
            else
                return false;
        }
        public bool stopPitLane()
        {
            if (null != PitLane)
            {
                fadeOut(PitLane);
                PitLane.Stop();
                return true;
            }
            else
                return false;
        }
        public bool stopEnvironment()
        {
            if (null != EnvAmbience)
            {
                fadeOut(EnvAmbience);
                EnvAmbience.Stop();
                return true;
            }
            else
                return false;
        }


        public bool startCrowd(float volume)
        {
            if (CrowdOn)
            {
                string filename = getRandomFile(SoundPath, "crowd");
                Crowd = Engine.Play2D(filename, true, true);
                if (null == Crowd)
                    return false;
                Crowd.Volume = volume;
                Crowd.Paused = false;
                return true;
            }
            else return false;
        }

        public bool stopCrowd()
        {
            if (null != Crowd)
            {
                fadeOut(Crowd);
                Crowd.Stop();
                return true;
            }
            else
                return false;
        }

        public void setCrowdVolume(float volume)
        {
            if (null != Crowd)
            {
                if (Crowd.Volume != volume)
                    Crowd.Volume = volume;
            }
        }

        public void setAnnouncerVolume(float volume)
        {
            if (null != Announcer)
            {
                if (Announcer.Volume != volume)
                    Announcer.Volume = volume;
            }
        }

        public void setMusicVolume(float volume)
        {
            if (null != Music)
            {
                if (Music.Volume != volume)
                    Music.Volume = volume;
            }
        }

        public void setEnvAmbienceVolume(float volume)
        {
            if (null != EnvAmbience)
            {
                if (EnvAmbience.Volume != volume)
                    EnvAmbience.Volume = volume;
            }
        }

        public void pauseCrowd()
        {
            if (null != Crowd)
            {
                if (Crowd.Paused == false)
                {
                    fadeOut(Crowd);
                    Crowd.Paused = true;
                }
            }
        }

        public void resumeCrowd()
        {
            if ((null != Crowd)&&CrowdOn)
            {
                if (Crowd.Paused == true)
                {
                    Crowd.Paused = false;
                    fadeIn(Crowd);
                }
            }
        }

        public bool startGuns(float volume, bool oval = true)
        {
            if (GunsOn)
            {
                string f = "wheelguns_";
                if (oval)
                    f += "oval";
                else
                    f += "road";
                string filename = getRandomFile(SoundPath, f);
                Guns = Engine.Play2D(filename, true, true);
                if (null == Guns)
                    return false;
                Guns.Volume = volume;
                Guns.Paused = false;
                return true;
            }
            else return false;
        }

        public bool stopGuns()
        {
            if ((null != Guns)&&(areGunsPlaying()))
            {
                fadeOut(Guns);
                Guns.Stop();
                return true;
            }
            else
                return false;
        }

        public bool areGunsPlaying()
        {
			if (null != Guns)
			{
                if ((Guns.PlayPosition != 0) && (Guns.PlayPosition != SOUND_MAX))
                    return true;
                else if (Guns.Finished == false)
                    return true;
                else
                    return false;
			}
			else
				return false;
        }

        public bool startFueling(float volume)
        {
            if (FuelingOn)
            {
                string filename = getRandomFile(SoundPath, "fuel");
                Fueling = Engine.Play2D(filename, true, true);
                if (null == Fueling)
                    return false;
                Fueling.Volume = volume;
                Fueling.Paused = false;
                return true;
            }
            else return false;
        }

        public bool stopFueling()
        {
            if ((null != Fueling)&&(isFuelingPlaying()))
            {
                fadeOut(Fueling);
                Fueling.Stop();
                return true;
            }
            else
                return false;
        }

        public bool isFuelingPlaying()
        {
            if (null != Fueling)
            {
                if ((Fueling.PlayPosition != 0) && (Fueling.PlayPosition != SOUND_MAX))
                    return true;
                else if (Fueling.Finished == false)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        public bool startSiren(float volume)
        {
            if (SirenOn)
            {
				string filename = getRandomFile(SoundPath, "pitsiren");
                Siren = Engine.Play2D(filename, false, true);
                if (null == Siren)
                    return false;
                Siren.Volume = volume;
                Siren.Paused = false;
                return true;
            }
            else return false;
        }

        public bool stopSiren()
        {
            if ((null != Siren)&&(isSirenPlaying()))
            {
                fadeOut(Siren);
                Siren.Stop();
                return true;
            }
            else
                return false;
        }

        public bool isSirenPlaying()
        {
            if (null != Siren)
            {
                if ((Siren.PlayPosition != 0) && (Siren.PlayPosition != SOUND_MAX))
                    return true;
                else if (Siren.Finished == false)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        public bool startGarage(float volume)
        {
            if (GarageOn)
            {
				string filename = getRandomFile(SoundPath, "garage");
                Garage = Engine.Play2D(filename, false, true);
                if (null == Garage)
                    return false;
                Garage.Volume = volume;
                Garage.Paused = false;
                return true;
            }
            else return false;
        }

        public bool stopGarage()
        {
            if ((null != Garage)&&(isGaragePlaying()))
            {
                fadeOut(Garage);
                Garage.Stop();
                return true;
            }
            else
                return false;
        }

        public bool isGaragePlaying()
        {
            if (null != Garage)
            {
                if ((Garage.PlayPosition != 0) && (Garage.PlayPosition != SOUND_MAX))
                    return true;
                else if (Garage.Finished == false)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        public bool startMusic(float volume)
        {
            if (MusicOn)
            {
                string filename = getRandomFile(SoundPath, "music");
				Music = Engine.Play2D(filename, false, true);
                if (null == Music)
                    return false;
                Music.Volume = volume;
                Music.Paused = false;
                return true;
            }
            else return false;
        }

        public bool stopMusic()
        {
            if (null != Music)
            {
                fadeOut(Music);
                Music.Stop();
                return true;
            }
            else
                return false;
        }

        public bool isMusicPlaying()
        {
            if (null != Music)
            {
                if ((Music.PlayPosition != 0) && (Music.PlayPosition != SOUND_MAX))
                    return true;
                else if (Music.Finished == false)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        public bool startRepair(float volume)
        {
            if (RepairOn)
            {
				string filename = getRandomFile(SoundPath, "repair");
                Repair = Engine.Play2D(filename, true, true);
                if (null == Repair)
                    return false;
                Repair.Volume = volume;
                Repair.Paused = false;
                return true;
            }
            else return false;
        }

        public bool stopRepair()
        {
            if (null != Repair)
            {
                fadeOut(Repair);
                Repair.Stop();
                return true;
            }
            else
                return false;
        }			

        public bool isRepairPlaying()
        {
            if (null != Repair)
            {
                if ((Repair.PlayPosition != 0) && (Repair.PlayPosition != SOUND_MAX))
                    return true;
                else if (Repair.Finished == false)
                    return true;
                else
                    return false;
			}
			else
				return false;
        }		
		
        private void StopAllSounds()
        {
            fadeOut(PitLane);
            fadeOut(Crowd);
            fadeOut(EnvAmbience);
            fadeOut(Announcer);
            fadeOut(Guns);
			fadeOut(Siren);
			fadeOut(Repair);
            fadeOut(Music);
            fadeOut(Fueling);
            Engine.StopAllSounds();
            Engine.Dispose();
        }

        public void appExit()
        {
            StopAllSounds();
        }

        // fade in and out over half a second regardless of initial volume
        private void fadeOut(ISound sample)
        {
            if (sample != null)
            {
                float cur_vol = sample.Volume * 1000;
                float step = cur_vol / 100;

                if ((cur_vol > 0)&&(step > 1))
                {
                    for (float k = 100; k > 0; k -= step)
                    {
                        sample.Volume -= (step / 1000);
                        Thread.Sleep(1);
                    }
                }
            }
        }
        
        private void fadeIn(ISound sample)
        {
            if (sample != null)
            {
                float cur_vol = sample.Volume * 1000;
                float step = cur_vol / 100;

                if ((cur_vol > 0) && (step > 1))
                {
                    for (float k = 0; k < 100; k += step)
                    {
                        sample.Volume += (step / 1000);
                        Thread.Sleep(1);
                    }
                }
            }
        }

        private string getRandomFile(string path, string prefix)
        {
            ArrayList al = new ArrayList();
            DirectoryInfo di = new DirectoryInfo(path);
            string full = prefix + "*.*";
            FileInfo[] rgFiles = di.GetFiles(full);
            foreach (FileInfo fi in rgFiles)
            {
                al.Add(fi.FullName);
            }

            Random r = new Random();
            int x = r.Next(0, al.Count);

            return al[x].ToString();
        }

        public void setEnvAmbienceOn(bool on)
        {
            EnvAmbienceOn = on;
        }
        public void setAnnouncerOn(bool on)
        {
            AnnouncerOn = on;
        }
        public void setPitLaneOn(bool on)
        {
            PitLaneOn = on;
        }
        public void setCrowdOn(bool on)
        {
            CrowdOn = on;
        }
        public void setGunsOn(bool on)
        {
            GunsOn = on;
        }
        public void setSirenOn(bool on)
        {
            SirenOn = on;
        }
        public void setRepairOn(bool on)
        {
            RepairOn = on;
        }
        public void setGarageOn(bool on)
        {
            GarageOn = on;
        }
        public void setMusicOn(bool on)
        {
            MusicOn = on;
        }
        public void setFuelingOn(bool on)
        {
            FuelingOn = on;
        }
        public void setMasterOn(bool on)
        {
            MasterOn = on;

            if (!MasterOn)
                SetMasterVolume(0F);
        }
    }
}
