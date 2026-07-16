using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Lock_Shoot_Tone_Ping;
using System;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;

using NuclearOption.Networking;
using NuclearOption.NetworkTransforms;
using System.ComponentModel.Design.Serialization;
using UnityEngine.Yoga;
using System.Collections.Generic;
using Mirage;
using System.Linq;
using System.Reflection;

namespace NuclearOptionVWS;

[BepInPlugin("com.Aeriicatmeow.NuclearOptionVWS", "Aeriicat-VWS", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    #region AudioStorage
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

            SignificantAngle = plugin.Config.Bind("Position Audio", "Significant Angle", 30f, "the Angle of deviation from the chord line of your aircraft before the VWS considers an object to be high or low");
        }

        private int GetUpperBearing(int i) => (i * 90 + 45 + 360) % 360;
        private int GetLowerBearing(int i) => (i * 90 - 45 + 360) % 360;
        private int GetIndex(int Bearing) => ((Bearing + 45) / 90) % 4;

        public string[] GetPositionAudioString(GlobalPosition PlayerPosition, GlobalPosition EnemyPosition)
        {
            string[] ReturnString;

            float dx = PlayerPosition.x - EnemyPosition.x;
            float dy = PlayerPosition.y - EnemyPosition.y;
            float dz = PlayerPosition.z - EnemyPosition.z;

            float Distance = FastMath.Distance(EnemyPosition, PlayerPosition);

            //Z is northways apparently...which isnt very intuative but oh well
            double RelativeBearing = ((Math.Atan2(dz, dx) + 2 * Math.PI) % (2 * Math.PI) * 180 / Math.PI);
            double ChordAngleDeviation = (Math.Atan2(dy, Distance) * 180 / Math.PI);

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
            int[] PriorityDropDown = new int[10];
            for (int i = 0; i < PriorityDropDown.Length; i++)
            {
                PriorityDropDown[i] = i;
            }
            HostileHazards = new ConfigEntry<string>[HazardNames.Length];
            Priority = new ConfigEntry<int>[HazardNames.Length];
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
                    new ConfigDescription("What priority do you want to assign a hazard (Higher number = Higher Priority)", new AcceptableValueList<int>(PriorityDropDown)));
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
        public string GetUnitAudio(Unit unit, ConfigEntry<float> MinAirThreat, out int HazardPriority,float AAThreatOverride = -1)
        {
            if (unit.definition.typeIdentity.missile >= 0.5)
            {
                return GetAudioNameProtected(GetHazardCFG("Missile", out HazardPriority));
            }
            else if (unit.definition.typeIdentity.air >= 0.5)
            {
                return GetAudioNameProtected(GetHazardCFG("Low Priority Misc Ground", out HazardPriority));
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
                    return GetAudioNameProtected(GetHazardCFG("Air", out HazardPriority));
                }
            }
        }
    }
    internal class InstructionHazard
    {
        public ConfigEntry<string>[] CFG_EnvironmentHazards;
        public ConfigEntry<string> CFG_AudioOut;
        public ConfigEntry<float> CFG_SecondsToCollision;
        public ConfigEntry<int> CFG_LandingAltitude;
        public InstructionHazard(Plugin plugin, string[] ArrayOfAllAudio)
        {
            plugin.Log(LogLevel.Info, "Initialising Instruction Hazard Configs");
            string[] HazardNames =
            {
            "OverG",
            "Roll Right",
            "Roll Left",
            "AoA",
            "Altitude",
            "Critical Altitude",
            "Pull Up",
            "Landing Altitude",
            "Check Fuel",
            "Low Fuel",
            "Bingo Fuel",
            "Flare",
            "Low Flare",
            "Flare Out",
            "Notch",
            "Jammer",
            "Jammer Out",
            "Eject",
            };
            string[] HazardDescriptions =
            {
                "To be played when G force is sufficienly high enough to make you unconscious",
                "To be played when bank angle exceeds a safe bank angle to the left",
                "To be played when bank angle exceeds a safe bank angle to the right",
                "To be played when AoA approaches Stall angle (but before stalling)",
                "To be played when there are 2*[seconds to collision] time left before collision with ground. will not tirgger if gear is down",
                "To be played when there are [seconds to collision] time left before collision with ground. will not trigger if gear is down",
                "To be played when AoA is too steep for landing or when at an AoA where a collsion with ground is imminent",
                "To be played when aircraft is below [landing altitude]",
                "To be played when fuel is low (in the yellow)",
                "To be played when fuel is very low (in the red)",
                "To be played when there is approximately just enough fuel for you to return to your closest airfield or carrier (does not take into consideration airframe)",
                "To be played when flaring is advised",
                "To be played when less than 25% flares remain",
                "To be played when no flares remain",
                "To be played when notiching is advised",
                "To be played when Jamming is advised",
                "To be played when Capacitor charge is sufficiently low that jammer is nolonger effective",
                "To be played when aircraft is sufficiently damaged that ejecting is recomendable"
            };

            CFG_EnvironmentHazards = new ConfigEntry<string>[HazardNames.Length];
            for (int i = 0; i < CFG_EnvironmentHazards.Length; i++)
            {
                plugin.Log(LogLevel.Info, i + "/" + CFG_EnvironmentHazards.Length);
                CFG_EnvironmentHazards[i] = plugin.Config.Bind("Instruction Hazards", HazardNames[i], AudioHandler.NoAudio,
                    new ConfigDescription(HazardDescriptions[i], new AcceptableValueList<string>(ArrayOfAllAudio)));
            }

            plugin.Log(LogLevel.Info, "OUT");
            CFG_AudioOut = plugin.Config.Bind("Instruction Hazards", "Suffix Depleted", AudioHandler.NoAudio, 
                new ConfigDescription("Audio that will be tagged onto the end of a counter measure label to tell you that it has been depleted", new AcceptableValueList<string>(ArrayOfAllAudio)));
            plugin.Log(LogLevel.Info, "STC");
            CFG_SecondsToCollision = plugin.Config.Bind("Instruction Hazards", "Seconds to collsion", 2f, "The number of seconds [s] you can continue to descend at this speed before crahsing into the ground. This value determines when the altitude warnings are played");
            plugin.Log(LogLevel.Info, "ALT");
            CFG_LandingAltitude = plugin.Config.Bind("Instruction Hazards", "landing Altitude", 10, "If your relative altitude [m] is below this number, the landing altitude audio will be played");

        }

    }
    internal struct VWSWarning

    {
        public string[] audioNames;
        public double Priority;
        public bool Played;
        public int UpdatesSinceBumped;

        public Unit UnitCalled;

        public VWSWarning(int Priority, string[] AudioNames, Unit Unit, double Distance)
        {
            this.audioNames = AudioNames;
            this.Priority = Priority;
            this.Priority = DistanceInclusivePriority(Distance);
            Played = false;
            UpdatesSinceBumped = 0;
            UnitCalled = Unit;
        }
        private double DistanceInclusivePriority(double Distance)
        {
            return Math.Floor(Priority) + 0.9 * Math.Exp(-Distance / (1000 * 50));
        }
        public void IncrementBump()
        {
            UpdatesSinceBumped += 1;
        }
        public void Bump(GlobalPosition PlayerPosition)
        {
            UpdatesSinceBumped = 0;

            Priority = DistanceInclusivePriority(FastMath.Distance(PlayerPosition, UnitCalled.GlobalPosition()));
        }
        public void BumpAdvanced(GlobalPosition PlayerPosition, ref List<VWSWarning> WarningList)
        {
            Bump(PlayerPosition);
            int CurrentIndex = WarningList.IndexOf(this);
            while(WarningList[CurrentIndex].Priority > WarningList[CurrentIndex +1].Priority & (CurrentIndex < WarningList.Count))
            {
                SwapIndexes(CurrentIndex, CurrentIndex + 1, ref WarningList);
                CurrentIndex++;
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
            if (Msg1.audioNames.Length != Msg2.audioNames.Length)
            {
                return false;
            }
            if(Math.Floor(Msg1.Priority) != Math.Floor(Msg2.Priority))
            {
                return false;
            }
            for (int i = 0; i < Msg1.audioNames.Length; i++)
            {
                if (Msg1.audioNames[i] != Msg2.audioNames[i])
                {
                    return false;
                }
            }
            return true;
            
        }
        public static void AddWarning(ref List<VWSWarning> WarningList, VWSWarning Warning)
        {
            int upper = WarningList.Count-1;
            int lower = 0;
            int index;


            while (upper != lower)
            {
                index = (upper + lower) / 2;
                if (WarningList[index].Priority > Warning.Priority)
                {
                    upper = index;
                }
                else if (WarningList[index].Priority < Warning.Priority)
                {
                    lower = index;
                }

            }

            index = lower;

            if (WarningList[index].Priority > Warning.Priority)
            {
                WarningList.Insert(index, Warning);
            }
            else
            {
                index++;
                if(index == WarningList.Count)
                {
                    WarningList.Add(Warning);
                }
                else
                {
                    WarningList.Insert(index, Warning);
                }
            }
            
        }
        private static int GetIndexOfLowestPriorityInSection(List<VWSWarning> WarningList, int PrioritySection)
        {
            int upper = WarningList.Count - 1;
            int lower = 0;
            int index;
            while (upper != lower)
            {
                index = (upper + lower) / 2;
                if (WarningList[index].Priority > PrioritySection)
                {
                    upper = index;
                }
                else if (WarningList[index].Priority < PrioritySection)
                {
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
        public static bool CheckIfPresentAndBump(ref List<VWSWarning> WarningList, VWSWarning Warning, GlobalPosition PlayerPosition)
        {
            return CheckIfPresentAndBump(ref WarningList, Warning.Priority, Warning.UnitCalled, PlayerPosition);
        }
        public static bool CheckIfPresentAndBump(ref List<VWSWarning> WarningList, double Priority,Unit unit, GlobalPosition PlayerPosition)
        {
            int index = GetIndexOfLowestPriorityInSection(WarningList, (int)Math.Floor(Priority));//this is done because priority scales on distance and distance may very well have changed since the last bump
            if (index == -1)
            {
                return false;
            }
            else
            {
                while (Math.Floor(WarningList[index].Priority) == Math.Floor(Priority))
                {
                    if (WarningList[index].UnitCalled == unit)
                    {
                        WarningList[index].BumpAdvanced(PlayerPosition, ref WarningList);
                        return true;
                    }
                }
                return false;
            }
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
            CFG_HostilehazardsAudio = new HostileHazardConfig(this, AllAudioNames);
            CFG_InstructionHazardAudio = new InstructionHazard(this, AllAudioNames);
            Logger.LogInfo("Audio Configs Initialised");
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
        Logger.LogInfo("Initialising List Of Warnings");
        VWSList = new List<VWSWarning>();

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

        }
    }
    private void Update()
    {
        Logger.LogInfo("Update Recieved");
        if (SceneSingleton<CombatHUD>.i != null & SceneSingleton<CombatHUD>.i.aircraft != null)
        {
            NullPosition = false;
            PlayerAircraft = SceneSingleton<CombatHUD>.i.aircraft;
            PlayerHQ = SceneSingleton<DynamicMap>.i.HQ;
        }
        else
        {
            NullPosition = true;
        }

        try
        {
            VWSListUpdate();
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
    }

    private void VWSListUpdate()
    {
        for (int i = 0; i < VWSList.Count; i++)
        {
            VWSList[i].IncrementBump();
            VWSList[i].CreateLogDump();
            if (VWSList[i].UpdatesSinceBumped > 4)
            {
                VWSList.RemoveAt(i);
                i--;
            }

        }
    }
    public void ObserveUnitBearingFromMapIcon(Unit unit, bool IsLocked)
    {
        Logger.LogInfo("MapIcon Request recieved");
        try
        {
            if (((PlayerHQ != null & PlayerHQ != unit.NetworkHQ & unit.NetworkHQ != null)
                ||IsLocked) 
                & !NullPosition)//If Enemy (or is locked onto you)
            {
                GlobalPosition EnemyPosition = unit.GlobalPosition();
                GlobalPosition PlayerPosition = PlayerAircraft.GlobalPosition();
                double Distance = FastMath.Distance(EnemyPosition, PlayerPosition);



                if (CheckIfValidForCallout(unit, Distance) || IsLocked) //ya gonna want to know of a lock regardless of if its 1km away or 100km away.
                {
                    BearingDebug(unit, PlayerPosition, EnemyPosition);

                    int Priority;
                    string HazardAudio = CFG_HostilehazardsAudio.GetUnitAudio(unit, CFG_MinAirThreat, out Priority);

                    VWSWarning.CheckIfPresentAndBump(ref VWSList, Priority, unit, PlayerPosition);
                    string[] AudioNames = CFG_PositionCalloutAudio.GetPositionAudioString(PlayerPosition, EnemyPosition);
                    AudioNames.Prepend(HazardAudio);

                    VWSWarning Warning = new VWSWarning(Priority, AudioNames, unit, Distance);
                }
            }
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
    }
    private void BearingDebug(Unit unit, GlobalPosition PlayerPosition, GlobalPosition EnemyPosition)
    {
        float dx = PlayerPosition.x - EnemyPosition.x;
        float dz = PlayerPosition.z - EnemyPosition.z;

        //Z is northways apparently...which isnt very intuative but oh well
        float RelativeBearing = (float)((Math.Atan2(dz, dx) + 2 * Math.PI) % (2 * Math.PI) * 180 / Math.PI);
        Logger.LogInfo("Bearing to Hazard: " + unit.unitName + $"({unit.definition.code})" + " : " + RelativeBearing + " TO " + unit.GlobalPosition());
        Logger.LogInfo("ROLES:  AA: " + unit.definition.roleIdentity.antiAir + " AG: " + unit.definition.roleIdentity.antiSurface + " AM: " + unit.definition.roleIdentity.antiMissile + " AR: " + unit.definition.roleIdentity.antiRadar);

        Logger.LogInfo("TYPE: S: " + unit.definition.typeIdentity.surface + " A: " + unit.definition.typeIdentity.air + " M: " + unit.definition.typeIdentity.missile + " R: " + unit.definition.typeIdentity.radar);
        Logger.LogInfo("Local Bearing: " + PlayerAircraft.rb.rotation.eulerAngles.y);
        Logger.LogInfo("Position " + PlayerAircraft.GlobalPosition());
        Logger.LogInfo("Pitch " + PlayerAircraft.rb.rotation.eulerAngles.x);
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

        if (CFG_OnlyCallOutLockedAirMissiles.Value)
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
                return (AAthreat > CFG_MinAirThreat.Value);
            }
        }
        else
        {
            return false;
        }
    }
    
}
