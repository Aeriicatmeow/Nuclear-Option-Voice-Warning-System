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
using NuclearOption.Debugging;

namespace NuclearOptionVWS;

[BepInPlugin("com.Aeriicatmeow.NuclearOptionVWS", "NuclearOption-VWS", "1.2.1")]
public class Plugin : BaseUnityPlugin
{


    #region MainPlugin
    private const string FileModName = "NuclearOptionVWS";
    bool DeletePluginDllOnClose = false;
    public static Plugin I { get; private set; }
    internal static new ManualLogSource Logger;

    private Harmony Inj_Harmony;

    AudioHandler Audio;
    ExternalPackHandler PackHandler;


    Aircraft PlayerAircraft;
    FactionHQ PlayerHQ;

    bool NullPosition;

    ConfigEntry<bool> CFG_Enabled;
    ConfigEntry<int> CFG_Volume_Percent;
    ConfigEntry<string> CFG_EncodingType;

    ConfigEntry<float> CFG_MaxConsiderationDistanceAIR;
    ConfigEntry<float> CFG_MaxConsiderationDistanceGROUND;
    ConfigEntry<float> CFG_MinAirThreat;
    ConfigEntry<bool> CFG_AlwaysCallOutAircraft;
    public ConfigEntry<bool> CFG_OnlyCallOutLockedAirMissiles;
    ConfigEntry<bool> CFG_OnlyCallOutIfInLineOfSight;

    ConfigEntry<float> CFG_MinimunDelayBetweenWarnings;
    ConfigEntry<bool> CFG_ReIssueUnitWarningOnSignificantPositionChange;
    ConfigEntry<bool> CFG_BulkGroupAnnouncementForUnits;

    //AudioStorage
    BearingAudConfig CFG_PositionCalloutAudio;
    HostileHazardConfig CFG_HostilehazardsAudio;
    InstructionHazard CFG_InstructionHazardAudio;

    //Evidence Of Poor Programming:

    List<Unit> NotableUnits;
    List<double> NotableUnitInternalPriority;
    List<float> NotableUnitTimeOfLastPing;
    List<float> NotableUnitTimeOfLastWarned;
    List<int[]> NotableUnitRoughPositionOfLastWarned;
    int BasePriorityOfLastPlayed;


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

        Logger.LogInfo("Please note, this version is intended for Nuclear Option 0.34");
        //Unit_AAThreat_Ripper.Initialise();
        Logger.LogInfo("Establishing core configs");

        CFG_Enabled = Config.Bind("General", "Enabled", true, "Do you want the mod to run?");
        CFG_Volume_Percent = Config.Bind("General", "Volume", 50, new ConfigDescription("How loud do you want VWS audio to be", new AcceptableValueRange<int>(0, 200)));

        CFG_MinimunDelayBetweenWarnings = Config.Bind("VWS General", "MinimunDelayBeforeUnitWarningReIssued", 5f, "What is the minimun amount of time do you want to pass before you hear a warning about the same unit again?");
        CFG_ReIssueUnitWarningOnSignificantPositionChange = Config.Bind("VWS General", "ReIssueWarningOnSignificantChange", false, "If enabled, if the bearing and relative altitude of a hazard has changed significantly, the warning will be re-issued");
        CFG_BulkGroupAnnouncementForUnits = Config.Bind("VWS General", "Issue Bulk Warnings", false, "If enabled, when a warning is issued. All other warnings that would have the same voice line will be marked as called");

        CFG_MaxConsiderationDistanceAIR = Config.Bind("VWS General", "Max Distance to analyse Air units (Km)", 15f, new ConfigDescription("Units outside of this distance will not be considered to be called out by the VWS", new AcceptableValueRange<float>(1, 50)));
        CFG_MaxConsiderationDistanceGROUND = Config.Bind("VWS General", "Max Distance to analyse Ground units (Km)", 10f, new ConfigDescription("Units outside of this distance will not be considered to be called out by the VWS", new AcceptableValueRange<float>(1, 50)));
        CFG_MinAirThreat = Config.Bind("VWS General", "Minimun threat posed by unit", 0.5f, new ConfigDescription("Minimun threat a unit must pose to the aircraft for it to be considered by the VWS", new AcceptableValueRange<float>(0, 1)));
        CFG_AlwaysCallOutAircraft = Config.Bind("VWS General", "Ignore Aircraft Capability", false, "If checked, all nearby enemy aircraft will be called out regardless of if their A2A capability");
        CFG_OnlyCallOutLockedAirMissiles = Config.Bind("VWS General", "Only Call Out Locked", true, "If checked, Only locked missiles will be called out");
        CFG_OnlyCallOutIfInLineOfSight = Config.Bind("VWS General", "Only Call Out If In Line Of Sight", true, "If Checked, Only hazards that are in line of sight of the aircraft will be called out");

        Logger.LogInfo("Verifying File Structure");
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
        catch (Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
        Logger.LogInfo("Initialising List Of Warnings");
        NotableUnits = new List<Unit>();
        NotableUnitInternalPriority = new List<double>();
        NotableUnitTimeOfLastPing = new List<float>();
        NotableUnitTimeOfLastWarned = new List<float>();
        NotableUnitRoughPositionOfLastWarned = new List<int[]>();
        BasePriorityOfLastPlayed = 0;

        Logger.LogInfo("Generating CFG Dictionary");
        try
        {
            CFG_PositionCalloutAudio.AddToCFGDictionary(ref PackHandler);
            CFG_InstructionHazardAudio.AddToCFGDictionary(ref PackHandler);
            CFG_HostilehazardsAudio.AddToCFGDictionary(ref PackHandler);
        }
        catch (Exception EXP)
        {
            Logger.LogFatal(EXP);
        }

        Logger.LogInfo("Initialising HarmonyX");
        Inj_Harmony = new Harmony($"com.Aeriicatmeow.{FileModName}");
        Inj_Harmony.PatchAll();
        Logger.LogInfo("Updating Active Pack");
        PackHandler.UpdateActivePack();
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Logger.LogInfo("If you run into any issues, please raise an issue request on the github: https://github.com/Aeriicatmeow/Nuclear-Option-Voice-Warning-System. \nAlternately, please contact me on Dicord. \n Nuclear Option Official Discord Channel: https://discord.com/channels/909034158205059082/1537140171110096976 \n Primerva 2082 Channel: https://discord.com/channels/1303878245942431765/1537143460090355863 \n Thankyou in advance.");
    }
    private bool VerifyFileStructure(ref string Root)
    {
        string OldFilemodname = "NuclearOption-VWS";
        bool ReturnVal = false;
        Regex LastInPath = new Regex(@"^(.*[\\])([^\\]*$)");
        Logger.LogInfo("Root:" + Root);
        Match LastInPathmatch = LastInPath.Match(Root);
        if (LastInPathmatch.Groups[2].Value == OldFilemodname)
        {
            Logger.LogError("Oldfilemodname detected");
            string Oldroot = Root;
            Root += "\\" + FileModName;

            Directory.Move(Oldroot, LastInPathmatch.Groups[1].Value+FileModName);
        }
        else if (LastInPathmatch.Groups[2].Value != FileModName)
        {

            CreateDirectoryIfNone(Root, FileModName);
            Root += "\\" + FileModName;
            ReturnVal = true;
            Logger.LogError("Plugin has been found to be in the wrong head folder. Dll will be copied to correct head folder. This DLL will be moved when runtime terminated");
            try
            {
                string NewPath = Root + "\\" + LastInPath.Match(Info.Location).Groups[2].Value;
                //I will always assume that the external dll is the newest
                if (File.Exists(NewPath))
                {
                    File.Delete(NewPath);
                }
                File.Copy(Info.Location, NewPath);
            }
            catch (Exception EXP)
            {
                Logger.LogFatal("FAILED TO COPY");
                Logger.LogFatal(EXP);
            }
        }
        
        CreateDirectoryIfNone(Root, "Audio");
        bool DownloadExamplePacks = false;
        string PRoot = $"{Root}\\Packs";
        if (!Directory.Exists(PRoot))
        {
            DownloadExamplePacks = true;
        }
        CreateDirectoryIfNone(Root, "Packs");
        if (DownloadExamplePacks)
        {
            ExternalPackHandler.PopulateFolderWithExamplePacks(PRoot);
        }

        return ReturnVal;
    }
    private void CreateDirectoryIfNone(string Root, string FolderName)
    {
        string tmp = $"{Root}\\{FolderName}";
        if (!Directory.Exists(tmp))
        {
            Logger.LogError(FolderName + " folder not found. Generating replacement");
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
        Unit_AAThreat_Ripper.DumpData(Path.GetDirectoryName(Info.Location) + @"\AllUnitsAAThreats.txt");
    }
    private void Update()
    {
        //Logger.LogInfo("UPDATE");
        try
        {
            PackHandler.UpdateActivePack();
        }
        catch (Exception EXP)
        {
            Logger.LogFatal(EXP);
        }


        if (SceneSingleton<CombatHUD>.i != null & SceneSingleton<CombatHUD>.i.aircraft != null & CFG_Enabled.Value)
        {

            NullPosition = false;
            PlayerAircraft = SceneSingleton<CombatHUD>.i.aircraft;
            PlayerHQ = SceneSingleton<DynamicMap>.i.HQ;

            try
            {
                Audio.Update();
                CFG_InstructionHazardAudio.InstructionOnUpdate(PlayerAircraft);
            }
            catch (Exception EXP)
            {
                Logger.LogFatal(EXP);
            }
        }
        else
        {
            KillProgramLoop();
        }

        if (!NullPosition)
        {
            if (!PlayerAircraft.HasEjected())
            {
                //Logger.LogInfo("Main Update Loop");
                try
                {
                    //Logger.LogInfo("Instructions");
                    //if (CFG_InstructionHazardAudio.CheckIfAnInstructionComplaintShouldBeIssued())
                    //{
                    //    NotableUnits.Add(PlayerAircraft);
                    //    NotableUnitInternalPriority.Add(CFG_InstructionHazardAudio.GetInstructionHazardPriority());
                    //}
                    bool CTP;
                    CFG_InstructionHazardAudio.InstructionWarnings(PlayerAircraft, Audio, this, out CTP);
                    //Logger.LogInfo("Hazards");
                    if (CTP)
                    {
                        UpdateNotableUnits();
                    }
                    else
                    {
                        //Logger.LogInfo("InstructionWarnings have taken priority");
                    }
                }
                catch (Exception EXP)
                {
                    Logger.LogFatal(EXP);
                }
            }
            else
            {
                KillProgramLoop();
            }

        }

    }
    private void KillProgramLoop()
    {
        NullPosition = true;
        Audio.ClearAllQueues();

        CFG_InstructionHazardAudio.ResetAll();
    }

    public void ObserveUnitBearingFromMapIcon(Unit unit, bool IsLocked)
    {
        //Logger.LogInfo("MapIcon Request recieved");
        //Logger.LogInfo(NullPosition);
        //Logger.LogInfo(unit.Identity);
        Unit_AAThreat_Ripper.ConsiderUnitForList(unit);
        try
        {
            //Logger.LogInfo(PlayerAircraft.NetworkHQ);
            //Logger.LogInfo(unit.NetworkHQ);

            if (((PlayerHQ != null & PlayerHQ != unit.NetworkHQ & unit.NetworkHQ != null)
                || IsLocked)
                & !NullPosition)//If Enemy (or is locked onto you)
            {
                if (!IsLocked)
                {
                    IsLocked = InstructionHazard.CheckIfMissileLocked(unit, PlayerAircraft);
                }
                //Logger.LogInfo("1st calling cull");


                if (CheckIfValidForCallout(unit, IsLocked)) //ya gonna want to know of a lock regardless of if its 1km away or 100km away.
                {

                    //Logger.LogInfo(unit.Identity + "Is Valid");
                    int index = NotableUnits.IndexOf(unit);
                    if (index == -1)
                    {
                        //Logger.LogInfo("Adding Unit");
                        NotableUnits.Add(unit);
                        NotableUnitInternalPriority.Add(GetBasePriority(unit));
                        NotableUnitTimeOfLastPing.Add(Time.timeSinceLevelLoad);
                        NotableUnitTimeOfLastWarned.Add(0);
                        NotableUnitRoughPositionOfLastWarned.Add(MiscData.DefaultUncalledRoughPosition);
                    }
                    else
                    {
                        NotableUnitTimeOfLastPing[index] = Time.timeSinceLevelLoad;
                    }
                }
            }
        }
        catch (Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
    }
    private void UpdateNotableUnits()
    {
        if (Audio.GetTotalQueueLength() > 0)
        {
            //Logger.LogInfo("Returning notable unit update as the audio queue is occupied");
            return;
        }
        if (NotableUnits.Count == 0)
        {
            //Logger.LogInfo("Returning notable unit update as there are no notable units in list");
            return;
        }

        int Index = 0;
        int HighIndex = 0;
        int LowIndex = 0;

        double HighestPriority = int.MinValue;
        double LowestPriority = int.MaxValue;
        //Logger.LogInfo("Cull Section");
        while (Index < NotableUnits.Count)
        {
            bool ClearToProceed = true;
            if (NotableUnits[Index].NetId == 0)
            {
                //Logger.LogInfo("Removing " + NotableUnits[Index].Identity + "Due to NetID of 0 (unit nolonger exists)");
                RemoveNotableUnit(Index);
                ClearToProceed = false;
            }
            else if (Time.timeSinceLevelLoad - NotableUnitTimeOfLastPing[Index] > 1)
            {
                //Logger.LogInfo("Removing " + NotableUnits[Index].Identity + "Due to time out exception");
                RemoveNotableUnit(Index);
                ClearToProceed = false;
            }
            else if (!CheckIfValidForCallout(NotableUnits[Index], InstructionHazard.CheckIfMissileLocked(NotableUnits[Index], PlayerAircraft)))
            {
                //Logger.LogInfo("Removing " + NotableUnits[Index].Identity + "Due to unit threat nolonger being valid");
                RemoveNotableUnit(Index);
                ClearToProceed = false;
            }

            if (ClearToProceed)
            {

                if (NotableUnitInternalPriority[Index] != MiscData.FirstStagePriority & NotableUnitInternalPriority[Index] != MiscData.SecondStagePriority & CooldownChecks(Index))//If its negetive, its already been called out.
                                                                                                                                                                                                  //If its Int16.MaxValue it is currently being called out (Hazard has only been called out itself).
                                                                                                                                                                                                  //If its Int32.MaxValue it is currently being called out. (Hazard is in its second stage of being called out. Bearing has been called out but not advise for 
                                                                                                                                                                                                  //In manyways, this means that the priority of an item increases as it is being called out cos cutting out part of it feels off.
                                                                                                                                                                                                  //This is specifically done because the List.contains function is probably written in a lower level language and so is faster. This is useful if the unit list is very cluttered
                                                                                                                                                                                                  //This is a shit system if someone else is working on this. Its a good thing im the only one working on this
                {
                    if (NotableUnitInternalPriority[Index] > 0)
                    {
                        NotableUnitInternalPriority[Index] = MiscData.ApplyDistanceInclusivePriority(NotableUnitInternalPriority[Index], FastMath.Distance(NotableUnits[Index].GlobalPosition(), PlayerAircraft.GlobalPosition()));
                    }

                    if (NotableUnitInternalPriority[Index] > HighestPriority)
                    {
                        HighestPriority = NotableUnitInternalPriority[Index];
                        HighIndex = Index;
                    }
                    if (NotableUnitInternalPriority[Index] < LowestPriority)
                    {
                        LowestPriority = NotableUnitInternalPriority[Index];
                        LowIndex = Index;
                    }

                }
                Index++;

            }





        }
        if (NotableUnits.Count == 0)
        {
            return;
        }

        //Logger.LogInfo("Execute Section");
        //Logger.LogInfo("HI: " + HighestPriority);
        //Logger.LogInfo("LO: " + LowestPriority);

        Index = NotableUnitInternalPriority.IndexOf(MiscData.SecondStagePriority);
        if (Index != -1)
        {
            //
            //Logger.LogInfo("Continuing a Missile Hazard. Specify Counterplay");
            AddHazardAdviceOnlyToAudioList(NotableUnits[Index]);
            MarkNotableUnitAsCalled(Index, true);
        }
        else
        {
            Index = NotableUnitInternalPriority.IndexOf(MiscData.FirstStagePriority);

            if (Index != -1)
            {
                //
                //Logger.LogInfo("Second stage of audio hazard playing - specify bearing");
                AddHazardBearingOnlyToAudioList(NotableUnits[Index]);
                if (CFG_InstructionHazardAudio.CheckIfResponseIsNeeded(NotableUnits[Index], PlayerAircraft))
                {
                    NotableUnitInternalPriority[Index] = MiscData.SecondStagePriority;
                }
                else
                {
                    MarkNotableUnitAsCalled(Index, true);
                }
            }
            else
            {
                Index = HighIndex;
                if (HighestPriority != int.MinValue & LowestPriority != int.MaxValue)
                {
                    if (HighestPriority >= -LowestPriority || LowestPriority > 0)
                    {
                        //
                        //Logger.LogInfo("First stage of audio playing. Specifying hazard");
                        AddHazardNameOnlyToAudioList(NotableUnits[Index]);
                        NotableUnitInternalPriority[Index] = MiscData.FirstStagePriority;
                    }
                    else
                    {
                        //Logger.LogInfo("Reloading notable units");
                        for (int i = 0; i < NotableUnits.Count; i++)
                        {
                            if (NotableUnitInternalPriority[i] == LowestPriority & CooldownChecks(Index))
                            {
                                NotableUnitInternalPriority[i] = -LowestPriority;//this resets it and puts it back into the calling list
                            }
                        }
                    }
                }
            }
        }

        //Logger.LogInfo("[ALL ITEMS:]");
        //for(int i = 0; i < NotableUnits.Count; i++)
        //{
        //    Logger.LogInfo(NotableUnits[i] + " |:| " + NotableUnitInternalPriority[i]);
        //}
        //Logger.LogInfo("[END OF NOTABLE UNITS LIST]");

    }
    private void MarkNotableUnitAsCalled(int Index, bool MarkBulkWarning = false)
    {
        NotableUnitInternalPriority[Index] = -GetBasePriority(NotableUnits[Index]);
        NotableUnitTimeOfLastWarned[Index] = Time.timeSinceLevelLoad;
        NotableUnitRoughPositionOfLastWarned[Index] = GenerateRoughPosition(NotableUnits[Index],false);

        if (MarkBulkWarning)
        {
            MarkBulkWarningAsCalledIfNeeded(Index);
        }
    }
    private void RemoveNotableUnit(int Index)
    {
        NotableUnits.RemoveAt(Index);
        NotableUnitInternalPriority.RemoveAt(Index);//this is arguably a lot more primative but it does work
        NotableUnitTimeOfLastPing.RemoveAt(Index);
        NotableUnitRoughPositionOfLastWarned.RemoveAt(Index);
    }
    private bool IsNotableUnitIndexOnCooldown(int Index)
    {
        return Time.timeSinceLevelLoad - NotableUnitTimeOfLastWarned[Index] < CFG_MinimunDelayBetweenWarnings.Value;
    }
    public int GetHighestBasePriority()
    {
        int High = -1;
        foreach (Unit u in NotableUnits)
        {
            int Priority = GetBasePriority(u);
            if (Priority > High)
            {
                High = Priority;
            }
        }
        return High;
    }

    private void AddHazardNameOnlyToAudioList(Unit unit)
    {
        ConsiderInterrupt(unit);
        Audio.AddToQueueLowPriority(CFG_HostilehazardsAudio.GetUnitAudio(unit, CFG_MinAirThreat).Value);
    }
    private void AddHazardBearingOnlyToAudioList(Unit unit)
    {
        ConsiderInterrupt(unit);
        ConfigEntry<string>[] PositionAudio = CFG_PositionCalloutAudio.GetPositionAudioString(PlayerAircraft, unit.GlobalPosition());
        foreach (ConfigEntry<string> s in PositionAudio)
        {
            Audio.AddToQueueLowPriority(s.Value);
        }
    }
    private void AddHazardAdviceOnlyToAudioList(Unit unit)
    {
        ConsiderInterrupt(unit);
        ConfigEntry<string> Advice = CFG_InstructionHazardAudio.GetMissileResponseAudio(unit, PlayerAircraft);
        if (Advice != null)
        {
            Audio.AddToQueueLowPriority(Advice.Value);
        }
    }
    public bool LastHadPriortyOverride = false;
    public void ConsiderInterrupt(Unit unit, int PriorityOverride = -1, bool ForceClearRegardless = false)
    {
        int CurrentPriority;
        if (PriorityOverride != -1)
        {
            CurrentPriority = PriorityOverride;
            LastHadPriortyOverride = true;
        }
        else
        {
            CurrentPriority = GetBasePriority(unit);
            LastHadPriortyOverride = false;
        }
        if (CurrentPriority != BasePriorityOfLastPlayed || ForceClearRegardless)
        {
            //Logger.LogInfo("Clearing Low Priority Queue");

            CFG_InstructionHazardAudio.ResetAltitudeComplaintStatus();

            Audio.ClearQueueLowPriority();
            for (int i = 0; i < NotableUnits.Count; i++)
            {
                if (NotableUnitInternalPriority[i] == MiscData.FirstStagePriority || NotableUnitInternalPriority[i] == MiscData.SecondStagePriority)
                {
                    NotableUnitInternalPriority[i] = GetBasePriority(NotableUnits[i]);
                }
            }
        }
        BasePriorityOfLastPlayed = CurrentPriority;
    }

    private int GetBasePriority(Unit unit)
    {
        int Priority;
        CFG_HostilehazardsAudio.GetUnitAudio(unit, CFG_MinAirThreat, out Priority, InstructionHazard.CheckIfMissileLocked(unit, PlayerAircraft));
        return Priority;
    }
    public bool CheckIfValidForCallout(Unit unit, bool LockedOverride = false)
    {
        //Logger.LogInfo("ENTERING CHECK FOR VALID CALL OUT "+unit.Identity);


        if (CFG_HostilehazardsAudio.IsUnitExcludedViaConfig(unit, PlayerAircraft, CFG_MinAirThreat, LockedOverride))
        {
            //Logger.LogInfo("Fail due to unit not being valid in config");
            //Logger.LogInfo(unit.Identity + "Removed due to being disabled in configs");
            return false;
        }
        if (LockedOverride & unit.definition.typeIdentity.missile >= 0.5)
        {
            return true;
        }


        GlobalPosition EnemyPosition = unit.GlobalPosition();
        GlobalPosition PlayerPosition = PlayerAircraft.GlobalPosition();
        double Distance = FastMath.Distance(EnemyPosition, PlayerPosition);

        float MaxConsiderationDistance;
        bool IsGround;
        bool IsMissile = unit.definition.typeIdentity.missile >= 0.5;

        if (CFG_OnlyCallOutIfInLineOfSight.Value & !unit.LineOfSight(PlayerAircraft.GlobalPosition().AsVector3(), 100000f))
        {
            //Logger.LogInfo("Fail due to no line of sight");
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

        if (Distance < MaxConsiderationDistance * 1000f)
        {
            if (!IsGround & !IsMissile & CFG_AlwaysCallOutAircraft.Value)
            {
                return true;
            }
            else
            {
                float AAthreat = HostileHazardConfig.GetAAThreat(unit);

                if (CFG_OnlyCallOutLockedAirMissiles.Value & unit.definition.typeIdentity.missile >= 0.5 & !InstructionHazard.CheckIfMissileLocked(unit, PlayerAircraft))
                {
                    //Logger.LogInfo("Fail as Missile must be locked to not be ignored");
                    return false;
                }

                //Logger.LogInfo(AAthreat + ":" + CFG_MinAirThreat.Value);
                return (AAthreat > CFG_MinAirThreat.Value);
            }
        }
        else
        {
            //Logger.LogInfo("Fail as unit is out of range");
            //Logger.LogInfo(MaxConsiderationDistance);
            return false;
        }
    }
    private bool CheckIfInRange(Unit Enemy, Aircraft Player)
    {
        float MaxConsiderationDistance;
        bool IsGround;
        if (Enemy.definition.typeIdentity.surface >= 0.5)//I dont think anything that can be both air and ground exist but just incase:
        {
            MaxConsiderationDistance = CFG_MaxConsiderationDistanceGROUND.Value;
        }
        else
        {
            MaxConsiderationDistance = CFG_MaxConsiderationDistanceAIR.Value;
        }
        return FastMath.Distance(PlayerAircraft.GlobalPosition(), Enemy.GlobalPosition()) < MaxConsiderationDistance * 1000f;
    }
    #region Update 1.2.0
    private bool CheckIfTwoUnitsWouldHaveSameWarning(Unit Unit1, Unit Unit2)
    {
        if (CFG_PositionCalloutAudio.GetAltitudeClass(PlayerAircraft, Unit1) != CFG_PositionCalloutAudio.GetAltitudeClass(PlayerAircraft, Unit2))
        {
            return false;
        }

        if (CFG_HostilehazardsAudio.GetUnitAudio(Unit1, CFG_MinAirThreat) != CFG_HostilehazardsAudio.GetUnitAudio(Unit2, CFG_MinAirThreat))
        {
            return false;
        }

        if (BearingAudConfig.GetIndex(PlayerAircraft, Unit1) != BearingAudConfig.GetIndex(PlayerAircraft, Unit2))
        {
            return false;
        }

        return true;
    }
    private int[] GenerateRoughPosition(Unit unit, bool RelativeToCockpit = true)
    {
        int[] ReturnArray = {BearingAudConfig.GetIndex(PlayerAircraft,unit),CFG_PositionCalloutAudio.GetAltitudeClass(PlayerAircraft,unit, RelativeToCockpit)};
        return ReturnArray;
    }

    private void MarkBulkWarningAsCalledIfNeeded(int Index)
    {
        if (CFG_BulkGroupAnnouncementForUnits.Value) 
        {
            int[] UnitRoughPos = GenerateRoughPosition(NotableUnits[Index]);
            ConfigEntry<string> UnitAudio = CFG_HostilehazardsAudio.GetUnitAudio(NotableUnits[Index], CFG_MinAirThreat);
            for (int i = 0; i < NotableUnits.Count; i++)
            {
                if (CheckIfTwoArraysSame(UnitRoughPos, GenerateRoughPosition(NotableUnits[i]))) //Done in this staggered way to save on resources. its a micro optomisation but it adds up
                {
                    if(UnitAudio == CFG_HostilehazardsAudio.GetUnitAudio(NotableUnits[i], CFG_MinAirThreat))
                    {
                        MarkNotableUnitAsCalled(i);
                    }
                }
            }
        }
    }
    private bool CooldownChecks(int Index)//true if not on cooldown. false if on cooldown
    {
        if (!IsNotableUnitIndexOnCooldown(Index))
        {
            return true;
        }
        if (GenerateRoughPosition(NotableUnits[Index], false)[0] != NotableUnitRoughPositionOfLastWarned[Index][0] & CFG_ReIssueUnitWarningOnSignificantPositionChange.Value)
        {
            //Logger.LogInfo("Dump");
            //Logger.LogInfo(NotableUnitRoughPositionOfLastWarned[Index]);
            //Logger.LogInfo(GenerateRoughPosition(NotableUnits[Index]));
            return true;
        }
        return false;
    }
    private bool CheckIfTwoArraysSame(int[] Array1, int[] Array2)
    {
        if(Array1.Length == Array2.Length)
        {
            for(int i = 0; i < Array1.Length; i++)
            {
                if (Array1[i] != Array2[i])
                {
                    return false;
                }
            }
            return true;
        }
        else 
        {
            return false;
        }
    }
    #endregion
    #region Misc External Triggers
    public void TriggerBINGOCheck(Aircraft AircraftConcerned, float FuelUsedOnTick)
    {
        try
        {
            if (AircraftConcerned == PlayerAircraft & !NullPosition & AircraftConcerned != null & Audio != null & CFG_Enabled.Value)
            {
                CFG_InstructionHazardAudio.CheckBINGOWarning(PlayerAircraft, FuelUsedOnTick, Audio);
            }
            //else
            //{
            //    Logger.LogInfo("Aircraft decided that this was not player aircraft");
            //}
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
    }
    public void TriggerAoACheck(float StallHornThreshold, float VelocityThreshold)
    {
        if (!NullPosition & CFG_Enabled.Value)
        {
            try
            {
                CFG_InstructionHazardAudio.CheckAoAWarning(PlayerAircraft, StallHornThreshold, VelocityThreshold, Audio);
            }
            catch (Exception EXP)
            {
                Logger.LogFatal(EXP);
            }
        }
    }
    public void TriggerRefuelCheck(Aircraft AircraftConcerned)
    {
        try
        {
            if (AircraftConcerned == PlayerAircraft & CFG_Enabled.Value)
            {
                CFG_InstructionHazardAudio.ResetFuelWarningStates();
            }
            //else
            //{
            //    Logger.LogInfo("Aircraft decided that this was not player aircraft");
            //}
        }
        catch (Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
    }
    public void TriggerDeathOutcome()
    {
        Audio.ClearAllQueues();
        NullPosition = true;
    }
    public void TriggerDamageCheck()
    {
        //CFG_InstructionHazardAudio.CheckDamage(PlayerAircraft);
    }
    #endregion
    #endregion
}
