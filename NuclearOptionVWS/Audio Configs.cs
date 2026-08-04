using BepInEx.Configuration;
using BepInEx.Logging;
using Lock_Shoot_Tone_Ping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.UIElements.Collections;
using UnityEngine;

namespace NuclearOptionVWS
{
    #region AudioStorage
    internal static class MiscData
    {
        public const string PriorityDescriptionText = "(Higher number = Higher Priority). A priority of 0 will result in the audio not being played at all";
        public static int[] GetPriorityDropDownArray()
        {
            int[] PriorityDropDown = new int[10];
            for (int i = 0; i < PriorityDropDown.Length; i++)
            {
                PriorityDropDown[i] = i;
            }
            return PriorityDropDown;
        }
        public static string GetAudioNameProtected(ConfigEntry<string> HazardCFG)
        {
            if (HazardCFG != null)
            {
                return HazardCFG.Value;
            }
            else
            {
                return AudioHandler.NoAudio;
            }
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

        public ConfigEntry<string>[] GetPositionAudioString(Aircraft Player, GlobalPosition EnemyPosition)
        {
            ConfigEntry<string>[] ReturnString;
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

            //Plugin.I.Log(LogLevel.Info, "ANGLE: "+ChordAngleDeviation);
            if (Math.Abs(ChordAngleDeviation) >= SignificantAngle.Value)
            {
                ReturnString = new ConfigEntry<string>[2];
                if (ChordAngleDeviation > 0)
                {
                    ReturnString[1] = High;
                }
                else
                {
                    ReturnString[1] = Low;
                }
            }
            else
            {
                ReturnString = new ConfigEntry<string>[1];
            }

            ReturnString[0] = Bearings[GetIndex((int)Math.Round(RelativeBearing))];

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
            return (Angle - 90) * -1;//this converts it to the format accepted by the mod
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
                    new ConfigDescription("What priority do you want to assign " + HazardNames[i] + " hazards " + MiscData.PriorityDescriptionText, new AcceptableValueList<int>(PriorityDropDown)));

                if (HazardNames[i] == "Missile")
                {
                    int LockedPriority = DefaultHazardPriority[i];
                    if (LockedPriority < Priority.Length - 1)
                    {
                        LockedPriority++;
                    }

                    Priority[HazardNames.Length] = plugin.Config.Bind(Category, "Missile Locked Priority", LockedPriority,
                        new ConfigDescription("What priority do you want to assign Locked Missile hazards " + MiscData.PriorityDescriptionText, new AcceptableValueList<int>(PriorityDropDown)));
                }
            }
        }
        private ConfigEntry<string> GetHazardCFG(string HazardName, out int HazardPriority)
        {
            for (int i = 0; i < HostileHazards.Length; i++)
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
        public int GetLockedMissilePriority() => Priority[Priority.Length - 1].Value;
        public int GetMissilePriority() => Priority[Priority.Length - 2].Value;
        public ConfigEntry<string> GetUnitAudio(Unit unit, ConfigEntry<float> MinAirThreat, out int HazardPriority, bool Locked = false, float AAThreatOverride = -1)
        {
            ConfigEntry<string> ReturnConfig;
            if (unit.definition.typeIdentity.missile >= 0.5)
            {
                if (Locked)
                {
                    HazardPriority = GetLockedMissilePriority();
                    int tmp;
                    ReturnConfig = GetHazardCFG("Missile", out tmp);
                }
                else
                {
                    ReturnConfig = GetHazardCFG("Missile", out HazardPriority);
                }
            }
            else if (unit.definition.typeIdentity.air >= 0.5)
            {
                ReturnConfig = GetHazardCFG("Air", out HazardPriority);
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
                            return HostileHazards[i];
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

                if (AAThreat >= MinAirThreat.Value + (1 - MinAirThreat.Value) / 2)
                {
                    ReturnConfig = GetHazardCFG("High Priotiy Misc Ground", out HazardPriority);
                }
                else
                {
                    ReturnConfig = GetHazardCFG("Low Priority Misc Ground", out HazardPriority);
                }
            }

            if (ReturnConfig.Value == AudioHandler.NoAudio)
            {
                HazardPriority = 0;
            }
            return ReturnConfig;
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
            CFG_InstructionHazardPriority = plugin.Config.Bind(HazardSettings, "Instruction hazard Priority", 7, new ConfigDescription("What priority do you want to assign instruction hazards " + MiscData.PriorityDescriptionText, new AcceptableValueList<int>(MiscData.GetPriorityDropDownArray())));
            plugin.Log(LogLevel.Info, "GeForce");
            CFG_GForceTolerance = plugin.Config.Bind(HazardSettings, "GeForce Tolerance", 8f, new ConfigDescription("How many Gs do you want to be pulled before an overG warning is issued", new AcceptableValueRange<float>(2, 10)));
            plugin.Log(LogLevel.Info, "Dictionary");
            CFG_InstructionHazards = new Dictionary<string, ConfigEntry<string>>();
            foreach (ConfigEntry<string> CFG in CFG_EnvironmentHazards)
            {
                CFG_InstructionHazards.Add(CFG.Definition.Key, CFG);
            }

        }

        private float ConvertAngleToNegpi2piRange(float Angle)
        {
            if (Angle > 180)
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
            //Plugin.I.Log(LogLevel.Info, "[BANKANGLE]"+BankAngle);

            if (VerticalVel * -1 * CFG_SecondsToCollision.Value * 2 > Alt & !PlayerAircraft.gearDeployed)
            {
                ConfigEntry<string>[] AltitudeWarningLine = new ConfigEntry<string>[2];
                if (VerticalVel * -1 * CFG_SecondsToCollision.Value > Alt)
                {
                    FatalTrajectory = true;
                    AltitudeWarningLine[0] = CFG_InstructionHazards.Get("Critical Altitude");
                }
                else
                {
                    FatalTrajectory = true;
                    AltitudeWarningLine[0] = CFG_InstructionHazards.Get("Altitude");
                }


                if (Math.Abs(BankAngle) > 60)
                {
                    if (BankAngle < 0)
                    {
                        AltitudeWarningLine[1] = CFG_InstructionHazards.Get("Roll Left");
                    }
                    else
                    {
                        AltitudeWarningLine[1] = CFG_InstructionHazards.Get("Roll Right");
                    }
                }
                else
                {
                    AltitudeWarningLine[1] = CFG_InstructionHazards.Get("Pull Up");
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
            if (Math.Abs(PlayerAircraft.gForce) > CFG_GForceTolerance.Value)
            {
                VWSWarning.AddWarningSafe(ref Warnings, (new VWSWarning(Priority, CFG_InstructionHazards.Get("OverG"), PlayerAircraft, 0, true)), PlayerAircraft);
            }

            //Flares
            if (PlayerAircraft.countermeasureManager.GetActiveCountermeasure().ammo >= 14)
            {
                FlaresLowFired = false;
            }
            else if (!FlaresLowFired & PlayerAircraft.countermeasureManager.GetActiveCountermeasure().chargeable == false)
            {
                FlaresLowFired = true;
                Plugin.I.Log(LogLevel.Info, "LowFlares");
                Audio.AddToQueue(CFG_InstructionHazards.Get("Flare").Value);
                Audio.AddToQueue(CFG_AudioOut.Value);
            }

            //Jammer
            //Plugin.I.Log(LogLevel.Info, "JAMMER: " + PlayerAircraft.GetPowerSupply().GetCharge());
            if (PlayerAircraft.GetPowerSupply() != null)
            {
                if (PlayerAircraft.GetPowerSupply().GetCharge() > 0.6f)
                {
                    CapacitorLowFired = false;
                }
                else if (!CapacitorLowFired)
                {
                    CapacitorLowFired = true;
                    Audio.AddToQueue(CFG_InstructionHazards.Get("Jammer").Value);
                    Audio.AddToQueue(CFG_AudioOut.Value);
                }
            }
            //Plugin.I.Log(LogLevel.Info, "IR Sig: " + PlayerAircraft.GetIRSource().intensity);

            //Plugin.I.Log(LogLevel.Info, "DMG: " + PlayerAircraft.partDamageTracker.GetDetachedRatio());
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
            if (PlayerAircraft.partDamageTracker.GetDetachedRatio() > 0.4)
            {
                return true;
            }

            if (FatalAoA & FatalTrajectory & PlayerAircraft.partDamageTracker.GetDetachedRatio() > 0.2)
            {
                return true;
            }

            if (PlayerAircraft.cockpit.hitPoints < 10)
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
                Plugin.I.Log(LogLevel.Info, Aircraft.speed);
                TotalFuelUsed += FuelUsedOnTick;
                TotalEvaluations++;
                Plugin.I.Log(LogLevel.Info, "TotalEval: " + TotalEvaluations);

                int NumberOfAircraftEngines = Aircraft.engineStates.Count;
                Plugin.I.Log(LogLevel.Info, "EngineNum: " + NumberOfAircraftEngines);
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

                        if (SecondsToRTB * AVGFuelConsumption * 1.1f > Aircraft.GetFuelQuantity())
                        {
                            Audio.AddToQueue(CFG_InstructionHazards.Get("Check Fuel").Value);
                            Audio.AddToQueue(CFG_InstructionHazards.Get("Bingo Fuel").Value);

                            BINGOTriggered = true;
                        }

                        Plugin.I.Log(LogLevel.Info, "AVG FUEL OUT: " + AVGFuelConsumption);
                        Plugin.I.Log(LogLevel.Info, "AVG SPEED: " + AVGSpeed);
                        Plugin.I.Log(LogLevel.Info, "SECONDS TO RTB: " + SecondsToRTB);
                        Plugin.I.Log(LogLevel.Info, "FUEL LEFT: " + Aircraft.GetFuelQuantity());
                    }
                }
            }

            if (!CheckFuelTriggered & Aircraft.GetFuelLevel() < 0.3f)
            {
                CheckFuelTriggered = true;
                Audio.AddToQueue(CFG_InstructionHazards.Get("Check Fuel").Value);
            }
            if (!FuelLowTriggered & Aircraft.GetFuelLevel() < 0.1f)
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
            Plugin.I.Log(LogLevel.Info, "AoA VAL: " + num);
            if (Aircraft.speed > VelocityThreshold & num > StallHornThreshold * 0.85f)
            {

                VWSWarning.AddWarningSafe(ref Warnings, (new VWSWarning(CFG_InstructionHazardPriority.Value, CFG_InstructionHazards.Get("AoA"), Aircraft, 0, true)), Aircraft);
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

            if (CheckIfMissileLocked(Warning.UnitCalled, PlayerAircraft) & CFG_InstructMissileCounterMeasures.Value)
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
                    Plugin.I.Log(LogLevel.Info, "IR SIGNATURE: PlayerAircraft.GetIRSource().intensity");
                    if (PlayerAircraft.GetIRSource().intensity < 4 || Distance < 2)
                    {
                        EndInstruction = "Flare";
                    }
                    else
                    {
                        EndInstruction = "Decrease Throttle";
                    }
                }
                Warning.audioNames.Add(CFG_InstructionHazards.Get(EndInstruction));
            }


        }
        private bool CheckIfAtValue(float Targetvalue, float Uncertainty, float QueryValue) => (QueryValue > Targetvalue - Uncertainty & QueryValue < Targetvalue + Uncertainty);

        public static bool CheckIfMissileLocked(Unit unit, Aircraft PlayerAircraft)
        {
            if (unit.definition.typeIdentity.missile >= 0.5)
            {

                if (((Missile)unit).targetID == PlayerAircraft.persistentID)
                {
                    return true;
                }
            }
            return false;
        }

        public void AddToCFGDictionary(ref ExternalPackHandler EPH)
        {
            foreach (ConfigEntry<string> c in CFG_InstructionHazards.Values)
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
        public static Plugin MainPlugin;

        public List<ConfigEntry<string>> audioNames;
        public double Priority;
        public bool Played;
        public int UpdatesSinceBumped;

        public float TimeOfLastPlayed;

        public Unit UnitCalled;

        public bool IsInstruction;
        public bool IsLocked;

        public static void SetAdditionalFields(BearingAudConfig bearingAudConfig, InstructionHazard instructionHazard, HostileHazardConfig hostileHazardConfig, Plugin Plugin)
        {
            BearingConfig = bearingAudConfig;
            InstructionHazardConfig = instructionHazard;
            HostileHazardsConfig = hostileHazardConfig;
            MainPlugin = Plugin;
        }
        public VWSWarning(int Priority, List<ConfigEntry<string>> AudioNames, Unit Unit, double Distance, bool IsInstruction = false, bool IsLocked = false)
        {
            this.audioNames = AudioNames;
            this.Priority = Priority;
            this.Priority = DistanceInclusivePriority(Distance);
            Played = false;
            this.IsInstruction = IsInstruction;
            UpdatesSinceBumped = 0;
            UnitCalled = Unit;
            this.IsLocked = IsLocked;
            TimeOfLastPlayed = 0;
        }
        public VWSWarning(int Priority, ConfigEntry<string>[] AudioNames, Unit Unit, double Distance, bool IsInstruction = false, bool IsLocked = false)
        {
            this.audioNames = AudioNames.ToList();
            this.Priority = Priority;
            this.Priority = DistanceInclusivePriority(Distance);
            Played = false;
            this.IsInstruction = IsInstruction;
            UpdatesSinceBumped = 0;
            UnitCalled = Unit;
            this.IsLocked = IsLocked;
            TimeOfLastPlayed = 0;
        }
        public VWSWarning(int Priority, ConfigEntry<string> AudioName, Unit Unit, double Distance, bool IsInstruction = false, bool IsLocked = false)
        {

            this.audioNames = new List<ConfigEntry<string>>();
            this.audioNames.Add(AudioName);
            this.Priority = Priority;
            this.Priority = DistanceInclusivePriority(Distance);
            Played = false;
            this.IsInstruction = IsInstruction;
            UpdatesSinceBumped = 0;
            UnitCalled = Unit;
            this.IsLocked = IsLocked;
            TimeOfLastPlayed = 0;
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
            UpdateAccuracy(Player, RenewBearing);


        }
        public void UpdateAccuracy(Aircraft Player, bool RenewBearing)
        {
            if (UnitCalled.definition.typeIdentity.missile >= 0.5)
            {
                if (!InstructionHazard.CheckIfMissileLocked(UnitCalled, Player))
                {
                    Priority = HostileHazardsConfig.GetMissilePriority();
                    if (MainPlugin.CFG_OnlyCallOutLockedAirMissiles.Value)
                    {
                        Plugin.I.Log(LogLevel.Info, "MISSILE NOLONGER TRACKING. WIPING");
                        Priority = 0;
                    }

                }
                else if (MainPlugin.CFG_HighCautionMode.Value)
                {
                    Priority = HostileHazardsConfig.GetLockedMissilePriority();

                }
            }

            Priority = DistanceInclusivePriority(FastMath.Distance(Player.GlobalPosition(), UnitCalled.GlobalPosition()));

            if (RenewBearing)
            {
                Plugin.I.Log(LogLevel.Info, "Renewing Bearing");
                ConfigEntry<string> tmp = audioNames[0];
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
            if (CurrentIndex + 1 >= WarningList.Count)
            {
                return;
            }
            Plugin.I.Log(LogLevel.Info, CurrentIndex + "/" + WarningList.Count);
            while (WarningList[CurrentIndex].Priority > WarningList[CurrentIndex + 1].Priority)
            {
                //Plugin.I.Log(LogLevel.Info, "BA loop");
                SwapIndexes(CurrentIndex, CurrentIndex + 1, ref WarningList);
                CurrentIndex++;
                if (CurrentIndex + 1 >= WarningList.Count)
                {
                    Plugin.I.Log(LogLevel.Info, "Terminating");
                    break;
                }
                Plugin.I.Log(LogLevel.Info, CurrentIndex + "/" + WarningList.Count);
            }
        }
        public void CreateLogDump()
        {
            string LogLineOne = "VWS: [" + UnitCalled.Identity + "]| ";
            foreach (ConfigEntry<string> s in audioNames)
            {
                LogLineOne += s.Value + "; ";
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
            if (Math.Floor(Msg1.Priority) != Math.Floor(Msg2.Priority))
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
            else
            {
                Plugin.I.Log(LogLevel.Info, "SIMILAR WARNING FOUND");
            }
        }
        public static void AddWarning(ref List<VWSWarning> WarningList, VWSWarning Warning)
        {
            int upper = WarningList.Count - 1;
            int lower = 0;
            int index;

            if (WarningList.Count == 0)
            {
                WarningList.Add(Warning);
            }

            if (WarningList[WarningList.Count - 1].Priority < Warning.Priority)
            {
                WarningList.Add(Warning);
            }
            if (WarningList[0].Priority > Warning.Priority)
            {
                WarningList.Insert(0, Warning);
            }

            while (upper - lower > 1)
            {
                //Plugin.I.Log(LogLevel.Info, "VWS ADD loop");
                index = (upper + lower) / 2;
                Plugin.I.Log(LogLevel.Info, "U:" + upper + " L:" + lower + " I:" + index);
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
            Plugin.I.Log(LogLevel.Info, WarningList.Count);
            if (WarningList.Count == 0)
            {
                return -1;
            }
            int upper = WarningList.Count - 1;
            int lower = 0;
            int index;
            while (upper - lower > 1)
            {
                //Plugin.I.Log(LogLevel.Info, "Priority LOW loop");
                index = (upper + lower) / 2;
                Plugin.I.Log(LogLevel.Info, "U:" + upper + " L:" + lower + " I:" + index);
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
            Plugin.I.Log(LogLevel.Info, "Index:" + index);
            if (Math.Floor(WarningList[index].Priority) < PrioritySection)
            {
                index++;
                if (index >= WarningList.Count)
                {
                    return -1;
                }
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
            for (int index = 0; index < WarningList.Count; index++)
            {

                if (WarningList[index].UnitCalled == Warning.UnitCalled)
                {
                    if ((!Warning.IsInstruction & !WarningList[index].IsInstruction) || CheckIfSameContents(WarningList[index], Warning))
                    {
                        WarningList[index].BumpAdvanced(Player, ref WarningList, index);
                        return true;
                    }
                }

            }
            return false;
        }
        public static bool CheckIfUnitPresentAndBump(ref List<VWSWarning> WarningList, Unit unit, Aircraft Player)
        {
            for (int index = 0; index < WarningList.Count; index++)
            {

                if (WarningList[index].UnitCalled == unit)
                {
                    WarningList[index].BumpAdvanced(Player, ref WarningList, index);
                    return true;
                }

            }
            return false;
        }

        public bool CheckIfNoAudioInWarning()
        {
            foreach (ConfigEntry<string> s in audioNames)
            {
                if (s.Value != AudioHandler.NoAudio)
                {
                    return false;
                }
            }
            return true;
        }
    }
    #endregion
}
