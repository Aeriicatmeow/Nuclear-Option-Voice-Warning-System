using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Lock_Shoot_Tone_Ping;
using System;
using System.IO;
using System.Text.RegularExpressions;

using NuclearOption.Networking;
using NuclearOption.NetworkTransforms;
using System.ComponentModel.Design.Serialization;
using UnityEngine.Yoga;
using System.Collections.Generic;
using Mirage;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements.Collections;
using UnityEngine.UI;

namespace NuclearOptionVWS;

[BepInPlugin("com.Aeriicatmeow.NuclearOptionVWS", "Aeriicat-VWS", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    #region AudioStorage
    internal static class MiscData
    {
        public static int[] GetPriorityDropDownArray()
        {
            int[] PriorityDropDown = new int[10];
            for (int i = 0; i < PriorityDropDown.Length; i++)
            {
                PriorityDropDown[i] = i;
            }
            return PriorityDropDown;
        }
    }
    internal class BearingAudConfig
    {
        private ConfigEntry<string>[] Bearings;
        private ConfigEntry<string> High;
        private ConfigEntry<string> Low;
        private ConfigEntry<float> SignificantAngle;
        //[0] - 12 o'clock
        //[1] - 3 o'clock
        //[2] - 6 o'clock
        //[3] - 9 o'clock
        public BearingAudConfig(Plugin plugin, string[] ArrayOfAllAudio)
        {
            plugin.Log(LogLevel.Info, "Initialising bearing configs");
            Bearings = new ConfigEntry<string>[4];
            for (int i = 0; i < Bearings.Length; i++)
            {
                string TextDesc = "What sound do you want to be played to tell you that a Hazard is at " + (12 - i * 3) + " O'Clock Audio [" + GetLowerBearing(i) + "-" + GetUpperBearing(i) + "]";
                string Bearing = (12 - i * 3) + " OClock Audio";
                Bearings[i] = plugin.Config.Bind("Position Audio", Bearing, AudioHandler.NoAudio,
                    new ConfigDescription(TextDesc,
                    new AcceptableValueList<string>(ArrayOfAllAudio)));//Its this way more for sake of programming ease than anything else. Counterclockwise generally sucks but oh well
            }

            High = plugin.Config.Bind("Position Audio", "High Audio", AudioHandler.NoAudio,
                new ConfigDescription("What sound do you want to be played to tell you that a Hazard is more than [Significant Angle] degrees above your current aircraft angle", new AcceptableValueList<string>(ArrayOfAllAudio)));

            Low = plugin.Config.Bind("Position Audio", "Low Audio", AudioHandler.NoAudio,
            new ConfigDescription("What sound do you want to be played to tell you that a Hazard is more than [Significant Angle] degrees below your current aircraft angle ", new AcceptableValueList<string>(ArrayOfAllAudio)));

            SignificantAngle = plugin.Config.Bind("Position Audio", "Significant Angle", 10f, new ConfigDescription("the Angle of deviation from the chord line of your aircraft before the VWS considers an object to be high or low", new AcceptableValueRange<float>(5, 45)));
        }

        private int GetUpperBearing(int i) => (i * 90 + 45 + 360) % 360;
        private int GetLowerBearing(int i) => (i * 90 - 45 + 360) % 360;
        private int GetIndex(int Bearing) => ((Bearing + 45) / 90) % 4;

        public string[] GetPositionAudioString(Aircraft Player, GlobalPosition EnemyPosition)
        {
            string[] ReturnString;
            //GlobalPosition PlayerPosition = Player.GlobalPosition();
            //float dx = PlayerPosition.x - EnemyPosition.x;
            //float dy = PlayerPosition.y - EnemyPosition.y;
            //float dz = PlayerPosition.z - EnemyPosition.z;

            //float Distance = FastMath.Distance(EnemyPosition, PlayerPosition);

            ////Z is northways apparently...which isnt very intuative but oh well
            //double RelativeBearing = ((Math.Atan2(dx, dz) + 3 * Math.PI) % (2 * Math.PI) * 180 / Math.PI) - Player.rb.rotation.eulerAngles.y;
            //if(RelativeBearing < 0)
            //{
            //    RelativeBearing += 360;
            //}
            //double ChordAngleDeviation = ((Math.Atan2(dx, Distance) + 3 * Math.PI) % (2 * Math.PI) * 180 / Math.PI) - Player.rb.rotation.eulerAngles.x;//(Math.Atan2(dy, Distance) * 180 / Math.PI) - Player.rb.rotation.eulerAngles.x;
            double RelativeBearing = GetRelativeBearing(Player, EnemyPosition);
            double ChordAngleDeviation = GetRelativeAltitudeDifferenceAngle(Player, EnemyPosition);

            Logger.LogInfo("ANGLE: "+ChordAngleDeviation);
            if (Math.Abs(ChordAngleDeviation) >= SignificantAngle.Value)
            {
                ReturnString = new string[2];
                if(ChordAngleDeviation > 0)
                {
                    ReturnString[1] = High.Value;
                }
                else
                {
                    ReturnString[1] = Low.Value;
                }
            }
            else
            {
                ReturnString = new string[1];
            }

            ReturnString[0] = Bearings[GetIndex((int)Math.Round(RelativeBearing))].Value;

            return ReturnString;
            
        }

        public static float GetRelativeBearing(Aircraft Player, GlobalPosition EnemyPosition)
        {
            //This is the way base game NO does it so ill follow suit as the small discrepencies are annoying me
            Vector3 PlayerPos = new Vector3(Player.GlobalPosition().x, 0f, Player.GlobalPosition().z);
            Vector3 EnemyPos = new Vector3(EnemyPosition.x, 0f, EnemyPosition.z);
            Vector3 Difference = (EnemyPos - PlayerPos).normalized;

            Vector3 Forward = Player.transform.forward;

            float Angle = -Vector3.SignedAngle(Forward, Difference, Player.transform.up);
            if (Angle < 0)
            {
                Angle += 360;
            }
            return Angle;

        }

        public static float GetRelativeAltitudeDifferenceAngle(Aircraft Player, GlobalPosition EnemyPosition)
        {
            Vector3 PlayerPos = Player.GlobalPosition().AsVector3();
            Vector3 EnemyPos = EnemyPosition.AsVector3();
            Vector3 Difference = (EnemyPos - PlayerPos).normalized;

            Vector3 up = Player.transform.up;

            float Angle = Vector3.Angle(up, Difference);
            return (Angle-90)*-1;//this converts it to the format accepted by the mod
        }
        public void AddToCFGDictionary(ref ExternalPackHandler EPH)
        {
            foreach (ConfigEntry<string> c in Bearings)
            {
                EPH.AddToDictionary(c);
            }
            EPH.AddToDictionary(Low);
            EPH.AddToDictionary(High);
        }
    }
    internal class HostileHazardConfig
    {

        private ConfigEntry<string>[] HostileHazards;
        private ConfigEntry<int>[] Priority;
        public HostileHazardConfig(Plugin plugin, string[] ArrayOfAllAudio)
        {
            plugin.Log(LogLevel.Info, "Initialising hazard configs");

            const int GroundAirSplit = 5;
            string[] HazardNames =
            {
            "High Priotiy Misc Ground",//this includes naval units
            "Low Priority Misc Ground",
            "Manpads",
            "SPAAG",
            "SAM",
            "Air",
            "Missile"
            };
            int[] DefaultHazardPriority =
            {
                5,
                2,
                1,
                3,
                4,
                5,
                5,
            };
            int[] PriorityDropDown = MiscData.GetPriorityDropDownArray();
            HostileHazards = new ConfigEntry<string>[HazardNames.Length];
            Priority = new ConfigEntry<int>[HazardNames.Length + 1];
            string Category;
            for (int i = 0; i < HostileHazards.Length; i++)
            {
                //plugin.Log(LogLevel.Info, i+"/"+HazardNames.Length);
                if (i < GroundAirSplit)
                {
                    Category = "Ground Hazards";
                }
                else
                {
                    Category = "Air Hazards";
                }

                HostileHazards[i] = plugin.Config.Bind(Category, HazardNames[i], AudioHandler.NoAudio,
                    new ConfigDescription("What sound do you want to be played to alert you of a " + HazardNames[i] + " threat.", new AcceptableValueList<string>(ArrayOfAllAudio)));

                Priority[i] = plugin.Config.Bind(Category, HazardNames[i] + " Priority", DefaultHazardPriority[i],
                    new ConfigDescription("What priority do you want to assign "+HazardNames[i]+" hazards (Higher number = Higher Priority)", new AcceptableValueList<int>(PriorityDropDown)));

                if (HazardNames[i] == "Missile")
                {
                    int LockedPriority = DefaultHazardPriority[i];
                    if(LockedPriority < Priority.Length - 1)
                    {
                        LockedPriority++;
                    }

                    Priority[HazardNames.Length] = plugin.Config.Bind(Category, "Missile Locked Priority", LockedPriority,
                        new ConfigDescription("What priority do you want to assign Locked Missile hazards (Higher number = Higher Priority)", new AcceptableValueList<int>(PriorityDropDown)));
                }
            }
        }
        private ConfigEntry<string> GetHazardCFG(string HazardName, out int HazardPriority)
        {
            for(int i = 0; i < HostileHazards.Length; i++)
            {
                if (HostileHazards[i].Definition.Key == HazardName)
                {
                    HazardPriority = Priority[i].Value;
                    return HostileHazards[i];
                }
            }
            HazardPriority = 0;
            return null;
        }
        private string GetAudioNameProtected(ConfigEntry<string> HazardCFG)
        {
            if(HazardCFG != null)
            {
                return HazardCFG.Value;
            }
            else
            {
                return AudioHandler.NoAudio;
            }
        }
        public int GetLockedMissilePriority() => Priority[Priority.Length - 1].Value;
        public int GetMissilePriority() => Priority[Priority.Length - 2].Value;
        public string GetUnitAudio(Unit unit, ConfigEntry<float> MinAirThreat, out int HazardPriority,bool Locked = false,float AAThreatOverride = -1)
        {
            if (unit.definition.typeIdentity.missile >= 0.5)
            {
                if (Locked)
                {
                    HazardPriority = GetLockedMissilePriority();
                    int tmp;
                    return GetAudioNameProtected(GetHazardCFG("Missile", out tmp));
                }
                else
                {
                    return GetAudioNameProtected(GetHazardCFG("Missile", out HazardPriority));
                }
            }
            else if (unit.definition.typeIdentity.air >= 0.5)
            {
                return GetAudioNameProtected(GetHazardCFG("Air", out HazardPriority));
            }
            else
            {
                for (int i = 0; i < HostileHazards.Length; i++)
                {
                    if (!HostileHazards[i].Definition.Key.Contains(' '))
                    {
                        Regex tmpRegex = new Regex(HostileHazards[i].Definition.Key.ToUpper());
                        if (tmpRegex.Match(unit.definition.code).Success)
                        {
                            HazardPriority = Priority[i].Value;
                            return HostileHazards[i].Value;
                        }
                    }
                }

                float AAThreat;

                if (AAThreatOverride >= 0)
                {
                    AAThreat = AAThreatOverride;
                }
                else
                {
                    AAThreat = unit.definition.roleIdentity.antiAir;
                }

                if(AAThreat >= MinAirThreat.Value + (1 - MinAirThreat.Value) / 2)
                {
                    return GetAudioNameProtected(GetHazardCFG("High Priotiy Misc Ground", out HazardPriority));
                }
                else
                {
                    return GetAudioNameProtected(GetHazardCFG("Low Priority Misc Ground", out HazardPriority));
                }
            }
        }
        public void AddToCFGDictionary(ref ExternalPackHandler EPH)
        {
            foreach (ConfigEntry<string> c in HostileHazards)
            {
                EPH.AddToDictionary(c);
            }
        }
    }
    internal class InstructionHazard
    {
        public ConfigEntry<string> CFG_AudioOut;
        public ConfigEntry<float> CFG_SecondsToCollision;
        public ConfigEntry<int> CFG_DangerousAltitude;
        public ConfigEntry<int> CFG_InstructionHazardPriority;
        public ConfigEntry<float> CFG_GForceTolerance;
        public ConfigEntry<bool> CFG_InstructMissileCounterMeasures;

        private Dictionary<string, ConfigEntry<string>> CFG_InstructionHazards;

        public InstructionHazard(Plugin plugin, string[] ArrayOfAllAudio)
        {
            ResetAll();

            ConfigEntry<string>[] CFG_EnvironmentHazards;
            plugin.Log(LogLevel.Info, "Initialising Instruction Hazard Configs");
            const int InstructionCMSplit = 11;
            string[] HazardNames =
            {
            "OverG",
            "Roll Right",
            "Roll Left",
            "AoA",
            "Altitude",
            "Critical Altitude",
            "Pull Up",
            "Dangerous Altitude",
            "Check Fuel",
            "Low Fuel",
            "Bingo Fuel",
            "Eject",
            "Flare",
            "Decrease Throttle",
            "Jammer",
            "Notch",
            };
            string[] HazardDescriptions =
            {
                "To be played when G force is sufficienly high enough to make you unconscious",
                "To be played when bank angle exceeds a safe bank angle to the left when close to the ground",
                "To be played when bank angle exceeds a safe bank angle to the right when close to the ground",
                "To be played when AoA approaches Stall angle (but before stalling)",
                "To be played when there are 2*[seconds to collision] time left before collision with ground. will not tirgger if gear is down",
                "To be played when there are [seconds to collision] time left before collision with ground. will not trigger if gear is down",
                "To be played when AoA is too steep for landing or when at an AoA where a collsion with ground is imminent",
                "To be played when aircraft is below [dangerous altitude]",
                "To be played when to alert you to how much fuel you have left (plays during BINGO or when you have <30% fuel)",
                "To be played when fuel is very low (<10%)",
                "To be played when there is approximately just enough fuel for you to return to your closest airfield or carrier (does not take into consideration airframe)",
                "To be played when aircraft is sufficiently damaged that ejecting is recomendable",
                "To be played when flaring is advised",
                "To be played when decreasing your throttle is advised (to counter IR missiles)",
                "To be played when Jamming is advised",
                "To be played when notiching is advised"

            };

            CFG_EnvironmentHazards = new ConfigEntry<string>[HazardNames.Length];
            const string Headder = "Instruction Hazards";
            string Category = Headder;
            for (int i = 0; i < CFG_EnvironmentHazards.Length; i++)
            {

                if (i > InstructionCMSplit)
                {
                    Category = "Counter Measure " + Headder;
                }
                else
                {
                    Category = Headder;
                }
                plugin.Log(LogLevel.Info, i + "/" + CFG_EnvironmentHazards.Length);
                CFG_EnvironmentHazards[i] = plugin.Config.Bind(Category, HazardNames[i], AudioHandler.NoAudio,
                    new ConfigDescription(HazardDescriptions[i], new AcceptableValueList<string>(ArrayOfAllAudio)));
            }

            CFG_InstructMissileCounterMeasures = plugin.Config.Bind(Category, "Instruct on countermeasures", false, "If Enabled, All missile warnings will be appended with some instructions on how to counter them");

            const string HazardSettings = "Instruction Hazards Settings";
            plugin.Log(LogLevel.Info, "OUT");
            CFG_AudioOut = plugin.Config.Bind(HazardSettings, "Suffix Depleted", AudioHandler.NoAudio, 
                new ConfigDescription("To be Appended onto the end of either the Jammer Audio (when Capacitor is low) or onto the end of the Flare Audio (When Flares are low)", new AcceptableValueList<string>(ArrayOfAllAudio)));
            plugin.Log(LogLevel.Info, "STC");
            CFG_SecondsToCollision = plugin.Config.Bind(HazardSettings, "Seconds to collsion", 2f, "The number of seconds [s] you can continue to descend at this speed before crahsing into the ground. This value determines when the altitude warnings are played");
            plugin.Log(LogLevel.Info, "ALT");
            CFG_DangerousAltitude = plugin.Config.Bind(HazardSettings, "dangerous Altitude", 10, "If your relative altitude [m] is below this number, the dangerous altitude audio will be played");
            plugin.Log(LogLevel.Info, "PRIOR");
            CFG_InstructionHazardPriority = plugin.Config.Bind(HazardSettings, "Instruction hazard Priority", 7, new ConfigDescription("What priority do you want to assign instruction hazards (Higher number = Higher Priority)",new AcceptableValueList<int>(MiscData.GetPriorityDropDownArray())));
            plugin.Log(LogLevel.Info, "GeForce");
            CFG_GForceTolerance = plugin.Config.Bind(HazardSettings, "GeForce Tolerance", 8f, new ConfigDescription("How many Gs do you want to be pulled before an overG warning is issued", new AcceptableValueRange<float>(2, 10)));
            plugin.Log(LogLevel.Info,"Dictionary");
            CFG_InstructionHazards = new Dictionary<string, ConfigEntry<string>>();
            foreach(ConfigEntry<string> CFG in CFG_EnvironmentHazards)
            {
                CFG_InstructionHazards.Add(CFG.Definition.Key, CFG);
            }

        }

        private float ConvertAngleToNegpi2piRange(float Angle)
        {
            if(Angle > 180)
            {
                Angle -= 360;
            }
            return Angle;
        }

        private bool DangerCloseFired = true;
        private bool FlaresLowFired = true;
        private bool CapacitorLowFired = true;
        public void AddInstructionWarnings(ref List<VWSWarning> Warnings, Aircraft PlayerAircraft, AudioHandler Audio)
        {
            int Priority = CFG_InstructionHazardPriority.Value;

            float Alt = PlayerAircraft.radarAlt;
            float VerticalVel = Vector3.Dot(PlayerAircraft.CockpitRB().velocity, Vector3.up);

            float BankAngle = ConvertAngleToNegpi2piRange(PlayerAircraft.transform.eulerAngles.z);//Vector3.SignedAngle(Vector3.up, PlayerAircraft.transform.up, PlayerAircraft.transform.forward);
            Logger.LogInfo("[BANKANGLE]"+BankAngle);

            if (VerticalVel*-1*CFG_SecondsToCollision.Value*2 > Alt & !PlayerAircraft.gearDeployed)
            {
                string[] AltitudeWarningLine = new string[2];
                if(VerticalVel * -1 * CFG_SecondsToCollision.Value > Alt)
                {
                    FatalTrajectory = true;
                    AltitudeWarningLine[0] = CFG_InstructionHazards.Get("Critical Altitude").Value;
                }
                else
                {
                    FatalTrajectory = true;
                    AltitudeWarningLine[0] = CFG_InstructionHazards.Get("Altitude").Value;
                }


                if(Math.Abs(BankAngle)>60)
                {
                    if(BankAngle < 0)
                    {
                        AltitudeWarningLine[1] = CFG_InstructionHazards.Get("Roll Left").Value;
                    }
                    else
                    {
                        AltitudeWarningLine[1] = CFG_InstructionHazards.Get("Roll Right").Value;
                    }
                }
                else
                {
                    AltitudeWarningLine[1] = CFG_InstructionHazards.Get("Pull Up").Value;
                }

                VWSWarning.AddWarningSafe(ref Warnings, (new VWSWarning(Priority, AltitudeWarningLine, PlayerAircraft, 0, true)), PlayerAircraft);
            }
            else
            {
                FatalTrajectory = false;
            }

            //one off things will be injected directly into the audio handler so they cannot be ignored

            if (Alt >= CFG_DangerousAltitude.Value)
            {
                DangerCloseFired = false;
            }
            else if (!DangerCloseFired)
            {
                DangerCloseFired = true;
                Audio.AddToQueue(CFG_InstructionHazards.Get("Dangerous Altitude").Value);
            }

            //OverG
            if(Math.Abs(PlayerAircraft.gForce) > CFG_GForceTolerance.Value)
            {
                VWSWarning.AddWarningSafe(ref Warnings, (new VWSWarning(Priority, CFG_InstructionHazards.Get("OverG").Value, PlayerAircraft, 0, true)), PlayerAircraft);
            }

            //Flares
            if (PlayerAircraft.countermeasureManager.GetActiveCountermeasure().ammo >= 14)
            {
                FlaresLowFired = false;
            }
            else if(!FlaresLowFired & PlayerAircraft.countermeasureManager.GetActiveCountermeasure().chargeable == false)
            {
                FlaresLowFired = true;
                Logger.LogInfo("LowFlares");
                Audio.AddToQueue(CFG_InstructionHazards.Get("Flare").Value);
                Audio.AddToQueue(CFG_AudioOut.Value);
            }

            //Jammer
            Logger.LogInfo("JAMMER: " + PlayerAircraft.GetPowerSupply().GetCharge());
            if(PlayerAircraft.GetPowerSupply().GetCharge() > 0.6f)
            {
                CapacitorLowFired = false;
            }
            else if (!CapacitorLowFired)
            {
                CapacitorLowFired = true;
                Audio.AddToQueue(CFG_InstructionHazards.Get("Jammer").Value);
                Audio.AddToQueue(CFG_AudioOut.Value);
            }
            Logger.LogInfo("IR Sig: " + PlayerAircraft.GetIRSource().intensity);

            Logger.LogInfo("DMG: " + PlayerAircraft.partDamageTracker.GetDetachedRatio());
            if (CheckIfEjectAdvisable(PlayerAircraft))
            {
                string EjectAudio = CFG_InstructionHazards.Get("Eject").Value;
                if (!Audio.CheckIfQueueContains(EjectAudio))
                {
                    Audio.AddToQueue(EjectAudio);
                }
            }
        }

        private bool FatalAoA = false;
        private bool FatalTrajectory = false;
        Queue<PilotHealthSnapshot> PilotHealthTrend;

        private struct PilotHealthSnapshot
        {
            public float PilotHealth;
            public float TimeOfReading;
            public PilotHealthSnapshot(float PilotHealth, float Time)
            {
                this.PilotHealth = PilotHealth;
                this.TimeOfReading = Time;
            }
        }
        private bool CheckIfEjectAdvisable(Aircraft PlayerAircraft)
        {
            if(PlayerAircraft.partDamageTracker.GetDetachedRatio() > 0.4)
            {
                return true;
            }

            if(FatalAoA & FatalTrajectory & PlayerAircraft.partDamageTracker.GetDetachedRatio() > 0.2)
            {
                return true;
            }

            if(PlayerAircraft.cockpit.hitPoints < 10)
            {
                return true;
            }

            
            //foreach (Pilot p in PlayerAircraft.pilots)
            //{
            //    if (p.player == PlayerAircraft.Player)
            //    {
            //        p.
            //        PilotHealthTrend.AddItem(new PilotHealthSnapshot(p.))
            //    }
            //}

            return false;
        }



        public void ResetAll()
        {
            ResetBINGOData();
            DangerCloseFired = true;
            FlaresLowFired = true;
            CapacitorLowFired = true;

            FatalAoA = false;
            FatalTrajectory = false;
        }

        private double TotalFuelUsed = 0;//Must be double due to how large the numbers will become over run time.
        private int TotalEvaluations = 0;
        private int TotalSecondsOfFuelConsumption = 0;
        private double TotalSummedSpeed = 0;

        private bool BINGOTriggered = false;
        private bool CheckFuelTriggered = false;
        private bool FuelLowTriggered = false;
        public void ResetFuelWarningStates()
        {
            BINGOTriggered = false;
            CheckFuelTriggered = false;
            FuelLowTriggered = false;
        }
        public void ResetBINGOData()
        {
            TotalFuelUsed = 0;
            TotalEvaluations = 0;
            TotalSecondsOfFuelConsumption = 0;
            TotalSummedSpeed = 0;
            BINGOTriggered = false;
        }
        public void CheckBINGOWarning(Aircraft Aircraft, float FuelUsedOnTick, AudioHandler Audio)
        {
            if (Aircraft.speed >= Aircraft.GetAircraftParameters().takeoffSpeed & BINGOTriggered == false)
            {
                TotalSummedSpeed += Aircraft.speed;
                Logger.LogInfo(Aircraft.speed);
                TotalFuelUsed += FuelUsedOnTick;
                TotalEvaluations++;
                Logger.LogInfo("TotalEval: " + TotalEvaluations);

                int NumberOfAircraftEngines = Aircraft.engineStates.Count;
                Logger.LogInfo("EngineNum: " + NumberOfAircraftEngines);
                if (NumberOfAircraftEngines < 1)
                {
                    return;
                }
                if ((TotalEvaluations % NumberOfAircraftEngines) == 0)
                {
                    //On full tick

                    TotalSecondsOfFuelConsumption++;

                    if (Aircraft.radarAlt > 1)//If Airborne
                    {
                        double AVGFuelConsumption = TotalFuelUsed / TotalSecondsOfFuelConsumption;
                        double AVGSpeed = TotalSummedSpeed / TotalEvaluations;
                        Airbase NearestAirbase = Aircraft.NetworkHQ.GetNearestAirbase(Aircraft.transform.position, new RunwayQuery
                        {
                            RunwayType = RunwayQueryType.Any,
                            MinSize = Aircraft.GetAircraftParameters().takeoffDistance,
                            TailHook = Aircraft.weaponManager.HasTailHook(),
                            LandingSpeed = Mathf.Sqrt(Aircraft.GetMass() / Aircraft.definition.aircraftInfo.maxWeight) * Aircraft.GetAircraftParameters().takeoffSpeed
                        });

                        int SecondsToRTB = (int)Math.Ceiling(FastMath.Distance(NearestAirbase.center.position, Aircraft.transform.position) / AVGSpeed);//distance(m), speed(ms-1)

                        if (SecondsToRTB * AVGFuelConsumption *1.1f > Aircraft.GetFuelQuantity())
                        {
                            Audio.AddToQueue(CFG_InstructionHazards.Get("Check Fuel").Value);
                            Audio.AddToQueue(CFG_InstructionHazards.Get("Bingo Fuel").Value);

                            BINGOTriggered = true;
                        }

                        Logger.LogInfo("AVG FUEL OUT: " + AVGFuelConsumption);
                        Logger.LogInfo("AVG SPEED: " + AVGSpeed);
                        Logger.LogInfo("SECONDS TO RTB: " + SecondsToRTB);
                        Logger.LogInfo("FUEL LEFT: " + Aircraft.GetFuelQuantity());
                    }
                }
            }

            if(!CheckFuelTriggered & Aircraft.GetFuelLevel() < 0.3f)
            {
                CheckFuelTriggered = true;
                Audio.AddToQueue(CFG_InstructionHazards.Get("Check Fuel").Value);
            }
            if(!FuelLowTriggered & Aircraft.GetFuelLevel() < 0.1f)
            {
                FuelLowTriggered = true;
                Audio.AddToQueue(CFG_InstructionHazards.Get("Low Fuel").Value);
            }

            
        }
        public void CheckAoAWarning(Aircraft Aircraft, float StallHornThreshold, float VelocityThreshold, ref List<VWSWarning> Warnings)
        {
            //AoA warning
            Vector3 vector = Aircraft.cockpit.transform.InverseTransformDirection(Aircraft.cockpit.rb.velocity);
            float num = Mathf.Atan2(vector.y, vector.z) * -57.29578f;
            Logger.LogInfo("AoA VAL: " + num);
            if (Aircraft.speed > VelocityThreshold & num > StallHornThreshold*0.85f)
            {

                VWSWarning.AddWarningSafe(ref Warnings, (new VWSWarning(CFG_InstructionHazardPriority.Value, CFG_InstructionHazards.Get("AoA").Value, Aircraft, 0, true)), Aircraft);
                if (num > StallHornThreshold)
                {
                    FatalAoA = true;
                }
            }
            else
            {
                FatalAoA = false;
            }
        }
        public void AppendMissileWarning(ref VWSWarning Warning, Aircraft PlayerAircraft)
        {
            
            if (CheckIfMissileLocked(Warning.UnitCalled,PlayerAircraft) & CFG_InstructMissileCounterMeasures.Value)
            {

                float Distance = FastMath.Distance(PlayerAircraft.GlobalPosition(), Warning.UnitCalled.GlobalPosition()) / 1000;
                string EndInstruction;
                if (Warning.UnitCalled.definition.typeIdentity.radar >= 0.5)
                {
                    float RelBearing = BearingAudConfig.GetRelativeBearing(PlayerAircraft, Warning.UnitCalled.GlobalPosition());
                    if (CheckIfAtValue(90, 10, RelBearing) || CheckIfAtValue(270, 10, RelBearing) || Distance < 2)
                    {
                        EndInstruction = "Jammer";
                    }
                    else
                    {
                        EndInstruction = "Notch";
                    }
                }
                else
                {
                    if (PlayerAircraft.GetIRSource().intensity < 4 || Distance < 2)
                    {
                        EndInstruction = "Flare";
                    }
                    else
                    {
                        EndInstruction = "Decrease Throttle";
                    }
                }
                EndInstruction = CFG_InstructionHazards.Get(EndInstruction).Value;
                Warning.audioNames.Add(EndInstruction);
            }
            
     
        }
        private bool CheckIfAtValue(float Targetvalue, float Uncertainty, float QueryValue) => (QueryValue > Targetvalue - Uncertainty & QueryValue < Targetvalue + Uncertainty);
            
        public static bool CheckIfMissileLocked(Unit unit, Aircraft PlayerAircraft)
        {
            if (unit.definition.typeIdentity.missile >= 0.5) {

                if (((Missile)unit).targetID == PlayerAircraft.persistentID)
                {
                    return true;
                }
            }
            return false;
        }       
            
        public void AddToCFGDictionary(ref ExternalPackHandler EPH)
        {
            foreach(ConfigEntry<string> c in CFG_InstructionHazards.Values)
            {
                EPH.AddToDictionary(c);
            }
            EPH.AddToDictionary(CFG_AudioOut);
        }
    }

    internal class VWSWarning//technically should be a struct but i dont want values to be copied

    {
        public static BearingAudConfig BearingConfig;
        public static InstructionHazard InstructionHazardConfig;
        public static HostileHazardConfig HostileHazardsConfig;

        public List<string> audioNames;
        public double Priority;
        public bool Played;
        public int UpdatesSinceBumped;

        public Unit UnitCalled;

        public bool IsInstruction;
        public bool IsLocked;

        public static void SetAdditionalFields(BearingAudConfig bearingAudConfig, InstructionHazard instructionHazard, HostileHazardConfig hostileHazardConfig)
        {
            BearingConfig = bearingAudConfig;
            InstructionHazardConfig = instructionHazard;
            HostileHazardsConfig = hostileHazardConfig;
        }
        public VWSWarning(int Priority, List<string> AudioNames, Unit Unit, double Distance, bool IsInstruction = false, bool IsLocked = false)
        {
            this.audioNames = AudioNames;
            this.Priority = Priority;
            this.Priority = DistanceInclusivePriority(Distance);
            Played = false;
            this.IsInstruction = IsInstruction;
            UpdatesSinceBumped = 0;
            UnitCalled = Unit;
            this.IsLocked = IsLocked;
        }
        public VWSWarning(int Priority, string[] AudioNames, Unit Unit, double Distance, bool IsInstruction = false, bool IsLocked = false)
        {
            this.audioNames = AudioNames.ToList();
            this.Priority = Priority;
            this.Priority = DistanceInclusivePriority(Distance);
            Played = false;
            this.IsInstruction = IsInstruction;
            UpdatesSinceBumped = 0;
            UnitCalled = Unit;
            this.IsLocked = IsLocked;
        }
        public VWSWarning(int Priority, string AudioName, Unit Unit, double Distance, bool IsInstruction = false, bool IsLocked = false)
        {

            this.audioNames = new List<string>();
            this.audioNames.Add(AudioName);
            this.Priority = Priority;
            this.Priority = DistanceInclusivePriority(Distance);
            Played = false;
            this.IsInstruction = IsInstruction;
            UpdatesSinceBumped = 0;
            UnitCalled = Unit;
            this.IsLocked = IsLocked;
        }
        private double DistanceInclusivePriority(double Distance)
        {
            return Math.Floor(Priority) + 0.9 * Math.Exp(-Distance / (1000 * 50));
        }
        public void IncrementBump()
        {
            UpdatesSinceBumped += 1;
        }
        public void Bump(Aircraft Player, bool RenewBearing = true)
        {
            UpdatesSinceBumped = 0;

            if(UnitCalled.definition.typeIdentity.missile >= 0.5)
            {
                if (InstructionHazard.CheckIfMissileLocked(UnitCalled, Player))
                {
                    Priority = HostileHazardsConfig.GetLockedMissilePriority();
                }
                else
                {
                    Priority = HostileHazardsConfig.GetMissilePriority();
                }
            }

            Priority = DistanceInclusivePriority(FastMath.Distance(Player.GlobalPosition(), UnitCalled.GlobalPosition()));

            if (RenewBearing)
            {
                string tmp = audioNames[0];
                audioNames = BearingConfig.GetPositionAudioString(Player, UnitCalled.GlobalPosition()).ToList();
                audioNames.Insert(0, tmp);

                VWSWarning Warning = this;
                InstructionHazardConfig.AppendMissileWarning(ref Warning, Player);
            }

        }
        public void BumpAdvanced(Aircraft Player, ref List<VWSWarning> WarningList, int IndexOverride = -1)
        {
            Bump(Player, !IsInstruction);
            int CurrentIndex;
            if (IndexOverride < 0)
            {
                CurrentIndex = WarningList.IndexOf(this);
            }
            else
            {
                CurrentIndex = IndexOverride;
            }
            if(CurrentIndex +1 >= WarningList.Count)
            {
                return;
            }
            Logger.LogInfo(CurrentIndex + "/" + WarningList.Count);
            while (WarningList[CurrentIndex].Priority > WarningList[CurrentIndex +1].Priority)
            {
                //Logger.LogInfo("BA loop");
                SwapIndexes(CurrentIndex, CurrentIndex + 1, ref WarningList);
                CurrentIndex++;
                if(CurrentIndex + 1 >= WarningList.Count)
                {
                    Logger.LogInfo("Terminating");
                    break;
                }
                Logger.LogInfo(CurrentIndex + "/" + WarningList.Count);
            }
        }
        public void CreateLogDump()
        {
            string LogLineOne = "VWS: [" + UnitCalled.Identity + "]| ";
            foreach(string s in audioNames)
            {
                LogLineOne += s + "; ";
            }
            Plugin.I.Log(LogLevel.Info, LogLineOne);
            Plugin.I.Log(LogLevel.Info, "PR: " + Priority + " TimeSinceBumped: " + UpdatesSinceBumped + " Played:" + Played);
        }
        public static void SwapIndexes(int Index1, int Index2, ref List<VWSWarning> WarningList)
        {
            VWSWarning tmp = WarningList[Index1];
            WarningList[Index1] = WarningList[Index2];
            WarningList[Index2] = tmp;
        }
        public static bool CheckIfSameContents(VWSWarning Msg1, VWSWarning Msg2)
        {
            if (Msg1.audioNames.Count != Msg2.audioNames.Count)
            {
                return false;
            }
            if(Math.Floor(Msg1.Priority) != Math.Floor(Msg2.Priority))
            {
                return false;
            }
            for (int i = 0; i < Msg1.audioNames.Count; i++)
            {
                if (Msg1.audioNames[i] != Msg2.audioNames[i])
                {
                    return false;
                }
            }
            return true;
            
        }
        public static void AddWarningSafe(ref List<VWSWarning> WarningList, VWSWarning Warning, Aircraft PlayerAircraft)
        {
            if (!CheckIfPresentAndBump(ref WarningList, Warning, PlayerAircraft))
            {
                AddWarning(ref WarningList, Warning);
            }
        }
        public static void AddWarning(ref List<VWSWarning> WarningList, VWSWarning Warning)
        {
            int upper = WarningList.Count-1;
            int lower = 0;
            int index;

            if (WarningList.Count == 0)
            {
                WarningList.Add(Warning);
            }

            if (WarningList[WarningList.Count-1].Priority < Warning.Priority)
            {
                WarningList.Add(Warning);
            }
            if (WarningList[0].Priority > Warning.Priority)
            {
                WarningList.Insert(0, Warning);
            }

            while (upper - lower > 1)
            {
                //Logger.LogInfo("VWS ADD loop");
                index = (upper + lower) / 2;
                Logger.LogInfo("U:" + upper + " L:" + lower + " I:" + index);
                if (WarningList[index].Priority > Warning.Priority)
                {
                    upper = index;
                }
                else if (WarningList[index].Priority < Warning.Priority)
                {
                    lower = index;
                }
                else
                {
                    upper = index;
                    lower = index;
                }

            }

            index = lower;

            while (WarningList[index].Priority < Warning.Priority)
            {
                index++;
                if (index == WarningList.Count)
                {
                    WarningList.Add(Warning);
                }
            }
            WarningList.Insert(index, Warning);
            
        }
        private static int GetIndexOfLowestPriorityInSection(List<VWSWarning> WarningList, int PrioritySection)
        {
            if(WarningList.Count == 0)
            {
                return -1;
            }
            int upper = WarningList.Count - 1;
            int lower = 0;
            int index;
            while (upper - lower > 1)
            {
                //Logger.LogInfo("Priority LOW loop");
                index = (upper + lower) / 2;
                Logger.LogInfo("U:" + upper + " L:" + lower + " I:" + index);
                if (WarningList[index].Priority > PrioritySection)
                {
                    upper = index;
                }
                else if (WarningList[index].Priority < PrioritySection)
                {
                    lower = index;
                }
                else
                {
                    upper = index;
                    lower = index;
                }

            }
            index = lower;
            if (Math.Floor(WarningList[index].Priority) < PrioritySection)
            {
                index++;
            }

            if (Math.Floor(WarningList[index].Priority) == PrioritySection)
            {
                return index;
            }
            else
            {
                return -1;
            }
        }
        public static bool CheckIfPresentAndBump(ref List<VWSWarning> WarningList, VWSWarning Warning, Aircraft Player)
        {
            int index = GetIndexOfLowestPriorityInSection(WarningList, (int)Math.Floor(Warning.Priority));//this is done because priority scales on distance and distance may very well have changed since the last bump
            if (index < 0)
            {
                return false;
            }
            else
            {
                while (Math.Floor(WarningList[index].Priority) == Math.Floor(Warning.Priority))
                {
                    //Logger.LogInfo("Check B loop");
                    if (WarningList[index].UnitCalled == Warning.UnitCalled)
                    {
                        if (!Warning.IsInstruction||CheckIfSameContents(WarningList[index],Warning))
                        {
                            WarningList[index].BumpAdvanced(Player, ref WarningList, index);
                            return true;
                        }
                    }
                    index++;
                    if (index >= WarningList.Count)
                    {
                        break;
                    }
                }
                return false;
            }
        }
        public static bool CheckIfPresentAndBump(ref List<VWSWarning> WarningList, double Priority,Unit unit, Aircraft Player)
        {
            int index = GetIndexOfLowestPriorityInSection(WarningList, (int)Math.Floor(Priority));//this is done because priority scales on distance and distance may very well have changed since the last bump
            if (index < 0)
            {
                return false;
            }
            else
            {
                while (Math.Floor(WarningList[index].Priority) == Math.Floor(Priority))
                {
                    //Logger.LogInfo("Check B loop");
                    if (WarningList[index].UnitCalled == unit)
                    {
                        WarningList[index].BumpAdvanced(Player, ref WarningList, index);
                        return true;
                    }
                    index++;
                    if(index >= WarningList.Count)
                    {
                        break;
                    }
                }
                return false;
            }
        }

        public bool CheckIfNoAudioInWarning()
        {
            foreach(string s in audioNames)
            {
                if(s != AudioHandler.NoAudio)
                {
                    return false;
                }
            }
            return true;
        }
    }
    #endregion
    private const string FileModName = "Aeriicat-VWS";
    bool DeletePluginDllOnClose = false;
    public static Plugin I { get; private set; }
    internal static new ManualLogSource Logger;

    private Harmony Inj_Harmony;

    AudioHandler Audio;
    ExternalPackHandler PackHandler;


    Aircraft PlayerAircraft;
    FactionHQ PlayerHQ;

    bool NullPosition;

    ConfigEntry<int> CFG_Volume_Percent;
    ConfigEntry<string> CFG_EncodingType;

    ConfigEntry<float> CFG_MaxConsiderationDistanceAIR;
    ConfigEntry<float> CFG_MaxConsiderationDistanceGROUND;
    ConfigEntry<float> CFG_MinAirThreat;
    ConfigEntry<bool> CFG_AlwaysCallOutAircraft;
    ConfigEntry<bool> CFG_OnlyCallOutLockedAirMissiles;
    ConfigEntry<bool> CFG_OnlyCallOutIfInLineOfSight;

    //AudioStorage
    BearingAudConfig CFG_PositionCalloutAudio;
    HostileHazardConfig CFG_HostilehazardsAudio;
    InstructionHazard CFG_InstructionHazardAudio;

    List<VWSWarning> VWSList;


    public void Log(LogLevel LogLevel, object Data)
    {
        Logger.Log(LogLevel, Data);
    }
    public string GetFileModName() => FileModName;

    private void Awake()
    {
        // Plugin startup logic
        I = this;
        Logger = base.Logger;


        CFG_Volume_Percent = Config.Bind("General", "Volume", 50, new ConfigDescription("How loud do you want VWS audio to be", new AcceptableValueRange<int>(0, 100)));

        CFG_MaxConsiderationDistanceAIR = Config.Bind("VWS General", "Max Distance to analyse Air units (Km)", 10f,new ConfigDescription("Units outside of this distance will not be considered to be called out by the VWS", new AcceptableValueRange<float>(1, 50)));
        CFG_MaxConsiderationDistanceGROUND = Config.Bind("VWS General", "Max Distance to analyse Ground units (Km)", 10f, new ConfigDescription("Units outside of this distance will not be considered to be called out by the VWS", new AcceptableValueRange<float>(1, 50)));
        CFG_MinAirThreat = Config.Bind("VWS General", "Minimun threat posed by unit", 0.5f, new ConfigDescription("Minimun threat a unit must pose to the aircraft for it to be considered by the VWS", new AcceptableValueRange<float>(0, 1)));
        CFG_AlwaysCallOutAircraft = Config.Bind("VWS General", "Ignore Aircraft Capability", false, "If checked, all nearby enemy aircraft will be called out regardless of if their A2A capability");
        CFG_OnlyCallOutLockedAirMissiles = Config.Bind("VWS General", "Only Call Out Locked", false, "If checked, Only locked missiles will be called out");
        CFG_OnlyCallOutIfInLineOfSight = Config.Bind("VWS General", "Only Call Out If In Line Of Sight", true, "If Checked, Only hazards that are in line of sight of the aircraft will be called out");
        string Root = Path.GetDirectoryName(Info.Location);
        DeletePluginDllOnClose = VerifyFileStructure(ref Root);


        //new project. Lets import a bunch of stuff from LSTP and modularise it cos it was an unmodularised hell scape by the end
        //from the get go, lots allow for pack handling because people care about that a lot. 

        Logger.LogInfo("Creating Pack Handler");
        PackHandler = new ExternalPackHandler(Root, gameObject, CFG_Volume_Percent, this, out CFG_EncodingType, out Audio);

        try
        {
            Logger.LogInfo("Initialising Audio Configs");
            string[] AllAudioNames = Audio.CreateArrayOfAudioNames();
            CFG_PositionCalloutAudio = new BearingAudConfig(this, AllAudioNames);
            CFG_InstructionHazardAudio = new InstructionHazard(this, AllAudioNames);
            CFG_HostilehazardsAudio = new HostileHazardConfig(this, AllAudioNames);

            Logger.LogInfo("Audio Configs Initialised");
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
        Logger.LogInfo("Initialising List Of Warnings");
        VWSWarning.SetAdditionalFields(CFG_PositionCalloutAudio, CFG_InstructionHazardAudio, CFG_HostilehazardsAudio);
        VWSList = new List<VWSWarning>();

        Logger.LogInfo("Generating CFG Dictionary");
        try
        {
            CFG_PositionCalloutAudio.AddToCFGDictionary(ref PackHandler);
            CFG_InstructionHazardAudio.AddToCFGDictionary(ref PackHandler);
            CFG_HostilehazardsAudio.AddToCFGDictionary(ref PackHandler);
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }

        Logger.LogInfo("Initialising HarmonyX");
        Inj_Harmony = new Harmony($"com.Aeriicatmeow.{FileModName}");
        Inj_Harmony.PatchAll();
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
    private bool VerifyFileStructure(ref string Root)
    {
        bool ReturnVal = false;
        Regex LastInPath = new Regex(@"^(.*[\\])([^\\]*$)");
        if (LastInPath.Match(Root).Groups[2].Value != FileModName)
        {
            CreateDirectoryIfNone(Root, FileModName);
            Root += "\\" + FileModName;
            ReturnVal = true;
            Logger.LogError("Plugin has been found to be in the wrong head folder. Dll will be copied to correct head folder. This DLL will be moved when runtime terminated");
            try
            {
                File.Copy(Info.Location, Root + "\\" + LastInPath.Match(Info.Location).Groups[2].Value);
            }
            catch(Exception EXP)
            {
                Logger.LogFatal("FAILED TO COPY");
                Logger.LogFatal(EXP);
            }
        }

        CreateDirectoryIfNone(Root, "Audio");
        CreateDirectoryIfNone(Root, "Packs");

        return ReturnVal;
    }
    private void CreateDirectoryIfNone(string Root, string FolderName)
    {
        string tmp = $"{Root}\\{FolderName}";
        if (!Directory.Exists(tmp))
        {
            Logger.LogError(FolderName+" folder not found. Generating replacement");
            Directory.CreateDirectory(tmp);
        }
    }
    private void OnDestroy()
    {
        if (DeletePluginDllOnClose)
        {
            File.Delete(Info.Location);
        }
        else
        {
            PackHandler.SaveCurrentSelectedConfig();
        }
    }
    private void Update()
    {
        try
        {
            PackHandler.UpdateActivePack();
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }

        if (SceneSingleton<CombatHUD>.i != null & SceneSingleton<CombatHUD>.i.aircraft != null)
        {
            NullPosition = false;
            PlayerAircraft = SceneSingleton<CombatHUD>.i.aircraft;
            PlayerHQ = SceneSingleton<DynamicMap>.i.HQ;

            try
            {
                Audio.Update();
            }
            catch(Exception EXP)
            {
                Logger.LogFatal(EXP);
            }
        }
        else
        {
            NullPosition = true;
            Audio.ClearQueue();

            CFG_InstructionHazardAudio.ResetAll();
        }

        if (!NullPosition)
        {
            try
            {
                CFG_InstructionHazardAudio.AddInstructionWarnings(ref VWSList, PlayerAircraft, Audio);
                VWSListUpdate();
            }
            catch (Exception EXP)
            {
                Logger.LogFatal(EXP);
            }
        }

    }

    private void VWSListUpdate()
    {
        Logger.LogInfo(VWSList.Count);


        int limit = VWSList.Count;
        int index = 0;
        Logger.LogInfo("NEW UPDATE");
        while(index < limit)
        {
            Logger.LogInfo("UPD loop: "+index +"/"+limit);
            VWSList[index].IncrementBump();
            VWSList[index].CreateLogDump();
            Logger.LogInfo("SECOND");
            if (VWSList[index].UpdatesSinceBumped > 1 || VWSList[index].UnitCalled.Identity.NetId == 0 || VWSList[index].CheckIfNoAudioInWarning())
            {
                Logger.LogInfo("REMOVING");
                VWSList.RemoveAt(index);
                limit--;
            }
            else
            {
                index++;
            }

        }


        Logger.LogInfo("FINAL PART");
        if (Audio.GetQueueLength() == 0 & VWSList.Count > 0)
        {
            ReadOffHighestPriorities();
        }

        
        
    }
    private void ReadOffHighestPriorities()
    {
        int index = VWSList.Count - 1;
        int HighestPriorityTier = (int)Math.Floor(VWSList[index].Priority);
        Logger.LogInfo("HIGHEST PRIORITY: " + HighestPriorityTier);
        while (Math.Floor(VWSList[index].Priority) == HighestPriorityTier & VWSList[index].Played)
        {
            index--;
            if (index < 0)
            {
                for (int i = 0; i < VWSList.Count; i++)
                {
                    VWSList[i].Played = false;
                }
                return;
            }
        }

        VWSWarning WarningToBePlayed = VWSList[index];
        if (Math.Floor(WarningToBePlayed.Priority) < HighestPriorityTier)
        {
            return;
        }
        Logger.LogInfo("CURR PRIOR: "+WarningToBePlayed.Priority);
        WarningToBePlayed.Played = true;
        foreach (string s in WarningToBePlayed.audioNames)
        {
            Audio.AddToQueue(s);
        }


    }
    public void ObserveUnitBearingFromMapIcon(Unit unit, bool IsLocked)
    {
        //Logger.LogInfo("MapIcon Request recieved");
        //Logger.LogInfo(NullPosition);
        try
        {
            if (((PlayerHQ != null & PlayerHQ != unit.NetworkHQ & unit.NetworkHQ != null)
                ||IsLocked) 
                & !NullPosition)//If Enemy (or is locked onto you)
            {
                GlobalPosition EnemyPosition = unit.GlobalPosition();
                GlobalPosition PlayerPosition = PlayerAircraft.GlobalPosition();
                double Distance = FastMath.Distance(EnemyPosition, PlayerPosition);
               // Logger.LogInfo("2nd cull");


                if (CheckIfValidForCallout(unit, Distance) || IsLocked) //ya gonna want to know of a lock regardless of if its 1km away or 100km away.
                {
                    //Logger.LogInfo("3rd cull");
                    BearingDebug(unit, PlayerAircraft, EnemyPosition);

                    int Priority;
                    Logger.LogInfo("HAZARD AUDIO");
                    string HazardAudio = CFG_HostilehazardsAudio.GetUnitAudio(unit, CFG_MinAirThreat, out Priority, IsLocked);


                    if (IsLocked)
                    {
                        Logger.LogInfo("ITS LOCKED");
                    }

                    bool IsPresent = VWSWarning.CheckIfPresentAndBump(ref VWSList, Priority, unit, PlayerAircraft);

                    if (!IsPresent) 
                    { 
                        Logger.LogInfo("BEARING AUDIO");
                        List<string> AudioNames = CFG_PositionCalloutAudio.GetPositionAudioString(PlayerAircraft, EnemyPosition).ToList();
                        foreach (string s in AudioNames)
                        {
                            Logger.LogInfo(s);
                        }
                        Logger.LogInfo("HAZARD AUDIO: " + HazardAudio);
                        AudioNames.Insert(0, HazardAudio);
                        Logger.LogInfo("SCRIPT");
                        foreach(string s in AudioNames)
                        {
                            Logger.LogInfo(s);
                        }

                        Logger.LogInfo("FINAL WARNING");
                        VWSWarning Warning = new VWSWarning(Priority, AudioNames, unit, Distance);
                        CFG_InstructionHazardAudio.AppendMissileWarning(ref Warning, PlayerAircraft);
                        VWSWarning.AddWarning(ref VWSList, Warning);
                        Logger.LogInfo("Done :)");
                    }
                    else
                    {
                        Logger.LogInfo("A duplicate of the VWS request has been found. Request has been bumped. entry will not be added");
                    }
                }
            }
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
    }
    private void BearingDebug(Unit unit, Aircraft Player, GlobalPosition EnemyPosition)
    {
        GlobalPosition PlayerPosition = Player.GlobalPosition();
        float dx = PlayerPosition.x - EnemyPosition.x;
        float dz = PlayerPosition.z - EnemyPosition.z;
        float dy = PlayerPosition.y - EnemyPosition.y;
        float Distance = FastMath.Distance(PlayerPosition, EnemyPosition);

        //Z is northways apparently...which isnt very intuative but oh well
        //float RelativeBearing = (float)((Math.Atan2(dx, dz) + 3 * Math.PI) % (2 * Math.PI) * 180 / Math.PI) ;
        //float ChordDeviation = (float) ((Math.Atan2(dy, Distance) + 2 * Math.PI) % (2 * Math.PI) * 180 / Math.PI) ;
        float RelativeBearing = BearingAudConfig.GetRelativeBearing(Player, EnemyPosition);
        float ChordDeviation = BearingAudConfig.GetRelativeAltitudeDifferenceAngle(Player, EnemyPosition);
        Logger.LogInfo("[DEBUG]");
        Logger.LogInfo("Bearing to Hazard: " + unit.unitName + $"({unit.definition.code})" + " : " + (RelativeBearing) + " TO " + unit.GlobalPosition());
        Logger.LogInfo("ROLES:  AA: " + unit.definition.roleIdentity.antiAir + " AG: " + unit.definition.roleIdentity.antiSurface + " AM: " + unit.definition.roleIdentity.antiMissile + " AR: " + unit.definition.roleIdentity.antiRadar);
        Logger.LogInfo("ANGLE DIFF: " + (ChordDeviation));
        Logger.LogInfo("TYPE: S: " + unit.definition.typeIdentity.surface + " A: " + unit.definition.typeIdentity.air + " M: " + unit.definition.typeIdentity.missile + " R: " + unit.definition.typeIdentity.radar);
        Logger.LogInfo("Local Bearing: " + PlayerAircraft.rb.rotation.eulerAngles.y);
        Logger.LogInfo("Position " + PlayerAircraft.GlobalPosition());
        Logger.LogInfo("Pitch " + PlayerAircraft.rb.rotation.eulerAngles.x);
        Logger.LogInfo("Yaw: " + PlayerAircraft.rb.rotation.eulerAngles.y);
        Logger.LogInfo("Roll: " + PlayerAircraft.rb.rotation.eulerAngles.z);
        //Logger.LogInfo("PRE CONSIDERATIONS RAW: DALT: " + ChordDeviation + " DBEAR: " + RelativeBearing);
        Logger.LogInfo("UNIT ID " + unit.Identity);
        //(Pitch,Yaw, Roll)
        //note that pitch is inverted
        //Bearings are slightly off but thats a nuclear option problem. not a me problem
    }

    private Regex CRAMcheck = new Regex("CRAM");

    private bool CheckIfValidForCallout(Unit unit, double Distance)
    {
        float MaxConsiderationDistance;
        bool IsGround;
        bool IsMissile = unit.definition.typeIdentity.missile >= 0.5;

        if (CFG_OnlyCallOutIfInLineOfSight.Value & !unit.LineOfSight(PlayerAircraft.GlobalPosition().AsVector3(), 10000f))
        {
            return false;
        }

        if (unit.definition.typeIdentity.surface >= 0.5)//I dont think anything that can be both air and ground exist but just incase:
        {
            MaxConsiderationDistance = CFG_MaxConsiderationDistanceGROUND.Value;
            IsGround = true;
        }
        else
        {
            MaxConsiderationDistance = CFG_MaxConsiderationDistanceAIR.Value;
            IsGround = false;
        }

        if(Distance < MaxConsiderationDistance * 1000f)
        {
            if (!IsGround & !IsMissile & CFG_AlwaysCallOutAircraft.Value)
            {
                return true;
            }
            else
            {
                float AAthreat = unit.definition.roleIdentity.antiAir;
                if (CRAMcheck.Match(unit.unitName).Success)
                {
                    AAthreat = 0.6f;//I justify this by saying that the CRAM is very similar to the SPAAG. NO said that CRAM poses no risk to aircraft. this is wrong imo.
                    //Ill say that CRAM is slightly weaker than SPAAG (0.7) as NO seems to think it poses no airthreat
                }

                if(CFG_OnlyCallOutLockedAirMissiles.Value & unit.definition.typeIdentity.missile >= 0.5 & !InstructionHazard.CheckIfMissileLocked(unit,PlayerAircraft))
                {
                    return false;
                }

                return (AAthreat > CFG_MinAirThreat.Value);
            }
        }
        else
        {
            return false;
        }
    }
    public void TriggerBINGOCheck(Aircraft AircraftConcerned, float FuelUsedOnTick)
    {
        try
        {
            if (AircraftConcerned == PlayerAircraft)
            {
                CFG_InstructionHazardAudio.CheckBINGOWarning(PlayerAircraft, FuelUsedOnTick, Audio);
            }
            else
            {
                Logger.LogInfo("Aircraft decided that this was not player aircraft");
            }
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
    }
    public void TriggerAoACheck(float StallHornThreshold, float VelocityThreshold)
    {
        if (!NullPosition)
        {
            try
            {
                CFG_InstructionHazardAudio.CheckAoAWarning(PlayerAircraft, StallHornThreshold, VelocityThreshold, ref VWSList);
            }
            catch (Exception EXP)
            {
                Logger.LogFatal(EXP);
            }
        }
    }
    
    
}
