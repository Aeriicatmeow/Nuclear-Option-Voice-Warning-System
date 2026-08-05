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

[BepInPlugin("com.Aeriicatmeow.NuclearOptionVWS", "NuclearOption-VWS", "1.0.0")]
public class Plugin : BaseUnityPlugin
{


    #region MainPlugin
    private const string FileModName = "NuclearOption-VWS";
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
    public ConfigEntry<bool> CFG_OnlyCallOutLockedAirMissiles;
    ConfigEntry<bool> CFG_OnlyCallOutIfInLineOfSight;
    //ConfigEntry<float> CFG_MinimunDelayBetweenWarnings;
    public ConfigEntry<bool> CFG_HighCautionMode;

    //AudioStorage
    BearingAudConfig CFG_PositionCalloutAudio;
    HostileHazardConfig CFG_HostilehazardsAudio;
    InstructionHazard CFG_InstructionHazardAudio;

    //Evidence Of Poor Programming:
    List<VWSWarning> VWSList;

    List<Unit> NotableUnits;
    List<double> NotableUnitInternalPriority;


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
        //CFG_MinimunDelayBetweenWarnings = Config.Bind("General", "MinimunDelayBeforeWarningReIssued", 0f, "What is the minimun amount of time do you want to pass before you hear a warning about the same unit again?");

        CFG_MaxConsiderationDistanceAIR = Config.Bind("VWS General", "Max Distance to analyse Air units (Km)", 15f,new ConfigDescription("Units outside of this distance will not be considered to be called out by the VWS", new AcceptableValueRange<float>(1, 50)));
        CFG_MaxConsiderationDistanceGROUND = Config.Bind("VWS General", "Max Distance to analyse Ground units (Km)", 10f, new ConfigDescription("Units outside of this distance will not be considered to be called out by the VWS", new AcceptableValueRange<float>(1, 50)));
        CFG_MinAirThreat = Config.Bind("VWS General", "Minimun threat posed by unit", 0.5f, new ConfigDescription("Minimun threat a unit must pose to the aircraft for it to be considered by the VWS", new AcceptableValueRange<float>(0, 1)));
        CFG_AlwaysCallOutAircraft = Config.Bind("VWS General", "Ignore Aircraft Capability", false, "If checked, all nearby enemy aircraft will be called out regardless of if their A2A capability");
        CFG_OnlyCallOutLockedAirMissiles = Config.Bind("VWS General", "Only Call Out Locked", true, "If checked, Only locked missiles will be called out");
        CFG_OnlyCallOutIfInLineOfSight = Config.Bind("VWS General", "Only Call Out If In Line Of Sight", true, "If Checked, Only hazards that are in line of sight of the aircraft will be called out");
        CFG_HighCautionMode = Config.Bind("VWS General", "High Caution Mode", false, "If Enabled, mod will attempt to estimate if a missile is locked onto you ");

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
        VWSWarning.SetAdditionalFields(CFG_PositionCalloutAudio, CFG_InstructionHazardAudio, CFG_HostilehazardsAudio, this);
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
        Logger.LogInfo("UPDATE");
        try
        {
            PackHandler.UpdateActivePack();
        }
        catch (Exception EXP)
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
            catch (Exception EXP)
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
            UpdateNotableUnits();
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
            //Logger.LogInfo("UPD loop: "+index +"/"+limit);
            VWSList[index].IncrementBump();
            //VWSList[index].CreateLogDump();
            Logger.LogInfo("SECOND");
            if (VWSList[index].UnitCalled.Identity.NetId == 0 || VWSList[index].CheckIfNoAudioInWarning() || Math.Floor(VWSList[index].Priority) == 0 || (VWSList[index].UpdatesSinceBumped>5 & VWSList[index].IsInstruction)|| VWSList[index].UpdatesSinceBumped > 60)
            {

                Logger.LogInfo("REMOVING");
                VWSList[index].CreateLogDump();
                VWSList.RemoveAt(index);
                limit--;

            }
            else
            {
                if (!CheckIfInRange(VWSList[index].UnitCalled, PlayerAircraft))
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

        }


        Logger.LogInfo("FINAL PART");
        if (Audio.GetQueueLength() == 0 & VWSList.Count > 0)
        {
            Logger.LogInfo("[CURRENT LIST:]");
            foreach(VWSWarning s in VWSList)
            {
                s.CreateLogDump();
            }
            ReadOffHighestPriorities();
        }

        
        
    }
    private void ReadOffHighestPriorities(int HighestPriorityTierOverride = int.MinValue)
    {
        int index = VWSList.Count - 1;
        int HighestPriorityTier = (int)Math.Floor(VWSList[index].Priority);

        if (HighestPriorityTierOverride >= 0)
        {
            HighestPriorityTier = HighestPriorityTierOverride;
        }
        


        Logger.LogInfo("HIGHEST PRIORITY: " + HighestPriorityTier);
        while (Math.Floor(VWSList[index].Priority) == HighestPriorityTier & VWSList[index].Played /*& (Time.timeSinceLevelLoad - VWSList[index].TimeOfLastPlayed) < CFG_MinimunDelayBetweenWarnings.Value*/)
        {
            index--;
            if(index < 0)
            {
                ReloadAllAudioPriorities();
                return;
            }
        }

        VWSWarning WarningToBePlayed = VWSList[index];
        if (Math.Floor(WarningToBePlayed.Priority) < HighestPriorityTier)
        {
            //if (index != VWSList.Count - 1) 
            //{
            //    if (Time.timeSinceLevelLoad - VWSList[index+1].TimeOfLastPlayed < CFG_MinimunDelayBetweenWarnings.Value)
            //    {
            //        ReadOffHighestPriorities(HighestPriorityTier - 1);
            //        return;
            //    }
            //}

            ReloadAllAudioPriorities();
            return;
        }
        Logger.LogInfo("CURR PRIOR: "+WarningToBePlayed.Priority);
        WarningToBePlayed.UpdateAccuracy(PlayerAircraft, true);
        WarningToBePlayed.Played = true;
        WarningToBePlayed.TimeOfLastPlayed = Time.timeSinceLevelLoad;
        foreach (ConfigEntry<string> s in WarningToBePlayed.audioNames)
        {
            Audio.AddToQueue(MiscData.GetAudioNameProtected(s));
        }


    }
    private void ReloadAllAudioPriorities()
    {
        Audio.AddToQueue("vws_fire");
        Logger.LogInfo("RELOADING");
        for (int i = 0; i < VWSList.Count; i++)
        {
            VWSList[i].Played = false;
        }
        ReadOffHighestPriorities();
    }
    public void ObserveUnitBearingFromMapIcon(Unit unit, bool IsLocked)
    {
        //Logger.LogInfo("MapIcon Request recieved");
        //Logger.LogInfo(NullPosition);
        Logger.LogInfo(unit.Identity);
        try
        {
            

            if (((PlayerHQ != null & PlayerHQ != unit.NetworkHQ & unit.NetworkHQ != null)
                ||IsLocked) 
                & !NullPosition)//If Enemy (or is locked onto you)
            {
                if (!IsLocked & CFG_HighCautionMode.Value)
                {
                    IsLocked = InstructionHazard.CheckIfMissileLocked(unit, PlayerAircraft);
                }
                //Logger.LogInfo("2nd cull");


                if (CheckIfValidForCallout(unit,IsLocked)) //ya gonna want to know of a lock regardless of if its 1km away or 100km away.
                {

                    Logger.LogInfo(unit.Identity + "Is Valid");
                    if (!NotableUnits.Contains(unit))
                    {
                        NotableUnits.Add(unit);
                        NotableUnitInternalPriority.Add(GetBasePriority(unit));
                    }
                }
            }
        }
        catch(Exception EXP)
        {
            Logger.LogFatal(EXP);
        }
    }
    private void UpdateNotableUnits()
    {
        
        int Index = 0;
        int HighIndex = 0;

        double HighestPriority = int.MinValue;
        while (Index < NotableUnits.Count)
        {

            if (NotableUnits[Index].NetId == 0)
            {
                NotableUnits.RemoveAt(Index);
                NotableUnitInternalPriority.RemoveAt(Index);//this is arguably a lot more primative but it does work
            }

            if (CheckIfValidForCallout(NotableUnits[Index], InstructionHazard.CheckIfMissileLocked(NotableUnits[Index], PlayerAircraft)))
            {
                NotableUnits.RemoveAt(Index);
                NotableUnitInternalPriority.RemoveAt(Index);//this is arguably a lot more primative but it does work
            }
            else
            {

                if (NotableUnitInternalPriority[Index] > 0 & NotableUnitInternalPriority[Index] != MiscData.FirstStagePriority & NotableUnitInternalPriority[Index] != MiscData.SecondStagePriority)//If its negetive, its already been called out.
                                                                                                                //If its Int16.MaxValue it is currently being called out (Hazard has only been called out itself).
                                                                                                                //If its Int32.MaxValue it is currently being called out. (Hazard is in its second stage of being called out. Bearing has been called out but not advise for 
                                                                                                                //In manyways, this means that the priority of an item increases as it is being called out cos cutting out part of it feels off.
                                                                                                                //This is specifically done because the List.contains function is probably written in a lower level language and so is faster. This is useful if the unit list is very cluttered
                                                                                                                //This is a shit system if someone else is working on this. Its a good thing im the only one working on this
                {
                    NotableUnitInternalPriority[Index] = MiscData.ApplyDistanceInclusivePriority(NotableUnitInternalPriority[Index], FastMath.Distance(NotableUnits[Index].GlobalPosition(), PlayerAircraft.GlobalPosition()));
                }

                if(NotableUnitInternalPriority[Index] > HighestPriority)
                {
                    HighestPriority = NotableUnitInternalPriority[Index];
                    HighIndex = Index;
                }
                

                Index++;
            }

        }

        if(Audio.GetQueueLength() == 0)
        {
            Index = NotableUnitInternalPriority.IndexOf(MiscData.SecondStagePriority);
            if(Index != -1)
            {
                //
                AddHazardAdviceOnlyToAudioList(NotableUnits[Index]);
                NotableUnitInternalPriority[Index] = -GetBasePriority(NotableUnits[Index]);
            }
            else
            {
                Index = NotableUnitInternalPriority.IndexOf(MiscData.FirstStagePriority);

                if (Index != -1)
                {
                    //
                    AddHazardBearingOnlyToAudioList(NotableUnits[Index]);
                    if (CFG_InstructionHazardAudio.CheckIfResponseIsNeeded(NotableUnits[Index], PlayerAircraft))
                    {
                        NotableUnitInternalPriority[Index] = MiscData.SecondStagePriority;
                    }
                    else
                    {
                        NotableUnitInternalPriority[Index] = -GetBasePriority(NotableUnits[Index]);
                    }
                }
                else
                {
                    Index = HighIndex;
                    //
                    AddHazardNameOnlyToAudioList(NotableUnits[Index]);
                    NotableUnitInternalPriority[Index] = MiscData.FirstStagePriority;
                }
            }
        }
        
    }
    public int GetHighestBasePriority()
    {
        int High = -1;
        foreach(Unit u in NotableUnits)
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
        Audio.AddToQueue(CFG_HostilehazardsAudio.GetUnitAudio(unit, CFG_MinAirThreat).Value);
    }
    private void AddHazardBearingOnlyToAudioList(Unit unit)
    {
        ConfigEntry<string>[] PositionAudio = CFG_PositionCalloutAudio.GetPositionAudioString(PlayerAircraft, unit.GlobalPosition());
        foreach (ConfigEntry<string> s in PositionAudio)
        {
            Audio.AddToQueue(s.Value);
        }
    }
    private void AddHazardAdviceOnlyToAudioList(Unit unit)
    {
        ConfigEntry<string> Advice = CFG_InstructionHazardAudio.GetMissileResponseAudio(unit, PlayerAircraft);
        if(Advice != null)
        {
            Audio.AddToQueue(Advice.Value);
        }
    }

    
    private int GetBasePriority(Unit unit)
    {
        int Priority;
        CFG_HostilehazardsAudio.GetUnitAudio(unit, CFG_MinAirThreat, out Priority, InstructionHazard.CheckIfMissileLocked(unit, PlayerAircraft));
        return Priority;
    }
    public bool CheckIfValidForCallout(Unit unit, bool LockedOverride = false)
    {

        if (LockedOverride)
        {
            return true;
        }
        CFG_HostilehazardsAudio.IsUnitExcludedViaConfig(unit, PlayerAircraft, CFG_MinAirThreat, false);

        

        GlobalPosition EnemyPosition = unit.GlobalPosition();
        GlobalPosition PlayerPosition = PlayerAircraft.GlobalPosition();
        double Distance = FastMath.Distance(EnemyPosition, PlayerPosition);

        float MaxConsiderationDistance;
        bool IsGround;
        bool IsMissile = unit.definition.typeIdentity.missile >= 0.5;

        if (CFG_OnlyCallOutIfInLineOfSight.Value & !unit.LineOfSight(PlayerAircraft.GlobalPosition().AsVector3(), 100000f))
        {
            Logger.LogInfo("Fail due to no line of sight");
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
                float AAthreat = HostileHazardConfig.GetAAThreat(unit);

                if(CFG_OnlyCallOutLockedAirMissiles.Value & unit.definition.typeIdentity.missile >= 0.5 & !InstructionHazard.CheckIfMissileLocked(unit,PlayerAircraft))
                {
                    Logger.LogInfo("Fail as Missile must be locked to not be ignored");
                    return false;
                }

                return (AAthreat > CFG_MinAirThreat.Value);
            }
        }
        else
        {
            Logger.LogInfo("Fail as unit is out of range");
            Logger.LogInfo(MaxConsiderationDistance);
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
        return FastMath.Distance(PlayerAircraft.GlobalPosition(),Enemy.GlobalPosition()) < MaxConsiderationDistance * 1000f;
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
                CFG_InstructionHazardAudio.CheckAoAWarning(PlayerAircraft, StallHornThreshold, VelocityThreshold, Audio);
            }
            catch (Exception EXP)
            {
                Logger.LogFatal(EXP);
            }
        }
    }
    #endregion

}
