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
        //try
        //{
        //    PackHandler.UpdateActivePack();
        //}
        //catch(Exception EXP)
        //{
        //    Logger.LogFatal(EXP);
        //}

        //if (SceneSingleton<CombatHUD>.i != null & SceneSingleton<CombatHUD>.i.aircraft != null)
        //{
        //    NullPosition = false;
        //    PlayerAircraft = SceneSingleton<CombatHUD>.i.aircraft;
        //    PlayerHQ = SceneSingleton<DynamicMap>.i.HQ;

        //    try
        //    {
        //        Audio.Update();
        //    }
        //    catch(Exception EXP)
        //    {
        //        Logger.LogFatal(EXP);
        //    }
        //}
        //else
        //{
        //    NullPosition = true;
        //    Audio.ClearQueue();

        //    CFG_InstructionHazardAudio.ResetAll();
        //}

        //if (!NullPosition)
        //{
        //    try
        //    {
        //        CFG_InstructionHazardAudio.AddInstructionWarnings(ref VWSList, PlayerAircraft, Audio);
        //        VWSListUpdate();
        //    }
        //    catch (Exception EXP)
        //    {
        //        Logger.LogFatal(EXP);
        //    }
        //}

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

                GlobalPosition EnemyPosition = unit.GlobalPosition();
                GlobalPosition PlayerPosition = PlayerAircraft.GlobalPosition();
                double Distance = FastMath.Distance(EnemyPosition, PlayerPosition);
                //Logger.LogInfo("2nd cull");


                if (CheckIfValidForCallout(unit, Distance) || IsLocked) //ya gonna want to know of a lock regardless of if its 1km away or 100km away.
                {

                    Logger.LogInfo(unit.Identity + " BUMPED");
                    if (!VWSWarning.CheckIfUnitPresentAndBump(ref VWSList, unit, PlayerAircraft))
                    {


                        Logger.LogInfo("HAZARD AUDIO");
                        int Priority;
                        ConfigEntry<string> HazardAudio = CFG_HostilehazardsAudio.GetUnitAudio(unit, CFG_MinAirThreat, out Priority, IsLocked);


                        if (IsLocked)
                        {
                            Logger.LogInfo("ITS LOCKED");
                        }


                        Logger.LogInfo("BEARING AUDIO");
                        List<ConfigEntry<string>> AudioNames = CFG_PositionCalloutAudio.GetPositionAudioString(PlayerAircraft, EnemyPosition).ToList();
                        foreach (ConfigEntry<string> s in AudioNames)
                        {
                            Logger.LogInfo(s);
                        }
                        Logger.LogInfo("HAZARD AUDIO: " + HazardAudio);
                        AudioNames.Insert(0, HazardAudio);
                        Logger.LogInfo("SCRIPT");
                        foreach (ConfigEntry<string> s in AudioNames)
                        {
                            Logger.LogInfo(s.Value);
                        }

                        Logger.LogInfo("FINAL WARNING");
                        VWSWarning Warning = new VWSWarning(Priority, AudioNames, unit, Distance);
                        CFG_InstructionHazardAudio.AppendMissileWarning(ref Warning, PlayerAircraft);
                        VWSWarning.AddWarning(ref VWSList, Warning);
                        Logger.LogInfo("Done :)");

                        VWSWarning.AddWarningSafe(ref VWSList, Warning, PlayerAircraft);
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
                float AAthreat = unit.definition.roleIdentity.antiAir;
                if (CRAMcheck.Match(unit.unitName).Success)
                {
                    AAthreat = 0.6f;//I justify this by saying that the CRAM is very similar to the SPAAG. NO said that CRAM poses no risk to aircraft. this is wrong imo.
                    //Ill say that CRAM is slightly weaker than SPAAG (0.7) as NO seems to think it poses no airthreat
                }

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
                CFG_InstructionHazardAudio.CheckAoAWarning(PlayerAircraft, StallHornThreshold, VelocityThreshold, ref VWSList);
            }
            catch (Exception EXP)
            {
                Logger.LogFatal(EXP);
            }
        }
    }
    #endregion

}
