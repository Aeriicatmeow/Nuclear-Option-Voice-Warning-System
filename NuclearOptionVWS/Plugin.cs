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

namespace NuclearOptionVWS;

[BepInPlugin("com.Aeriicatmeow.NuclearOptionVWS", "Aeriicat-VWS", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    #region AudioStorage
    internal class BearingAudConfig
    {
        ConfigEntry<string>[] Bearings;
        ConfigEntry<string> High;
        ConfigEntry<string> Low;
        //[0] - 12 o'clock
        //[1] - 3 o'clock
        //[2] - 6 o'clock
        //[3] - 9 o'clock
        public BearingAudConfig(Plugin plugin, string[] ArrayOfAllAudio)
        {
            Bearings = new ConfigEntry<string>[4];
            for (int i = 0; i < Bearings.Length; i++)
            {
                string Bearing = (12 - i * 3) + " O'Clock Audio";
                Bearings[i] = plugin.Config.Bind("Position Audio", Bearing, AudioHandler.NoAudio,
                    new ConfigDescription("What sound do you want to be played to tell you that a Hazard is at " + (12 - i * 3) + " O'Clock Audio [" + GetLowerBearing(i) + "-" + GetUpperBearing(i) + "]",
                    new AcceptableValueList<string>(ArrayOfAllAudio)));//Its this way more for sake of programming ease than anything else. Counterclockwise generally sucks but oh well
            }

            High = plugin.Config.Bind("Position Audio", "High Audio", AudioHandler.NoAudio,
                new ConfigDescription("What sound do you want to be played to tell you that a Hazard is more than 30 degrees above your current aircraft angle", new AcceptableValueList<string>(ArrayOfAllAudio)));

            Low = plugin.Config.Bind("Position Audio", "Low Audio", AudioHandler.NoAudio,
            new ConfigDescription("What sound do you want to be played to tell you that a Hazard is more than 30 degrees below your current aircraft angle ", new AcceptableValueList<string>(ArrayOfAllAudio)));
        }

        private int GetUpperBearing(int i) => (i * 90 + 45 + 360) % 360;
        private int GetLowerBearing(int i) => (i * 90 - 45 + 360) % 360;
        private int GetIndex(int Bearing) => ((Bearing + 45) / 90) % 4;
    }
    internal class HostileHazardConfig
    {
        ConfigEntry<string>[] HostileHazards;
        ConfigEntry<float> GroundHazardRadius;
        ConfigEntry<float> AirHazardRadius;
        ConfigEntry<int>[] Priority;
        ConfigEntry<bool> OnlyDeclareLockedMissiles;
        public HostileHazardConfig(Plugin plugin, string ArrayOfAllAudio)
        {
            int GroundAirSplit = 3;
            string[] HazardNames =
            {
            "Manpads",
            "SPAA",
            "SAM",
            "Air",
            "Missile"
        };
            int[] DefaultHazardPriority =
            {
            1,
            2,
            2,
            5,
            8,
        };
            int[] PriorityDropDown = new int[10];
            for (int i = 0; i < PriorityDropDown.Length; i++)
            {
                PriorityDropDown[i] = i;
            }
            HostileHazards = new ConfigEntry<string>[HazardNames.Length];
            string Category;
            for (int i = 0; i < HostileHazards.Length; i++)
            {
                if (i < GroundAirSplit)
                {
                    Category = "Ground Hazards";
                }
                else
                {
                    Category = "Air Hazards";
                }

                HostileHazards[i] = plugin.Config.Bind(Category, HazardNames[i], AudioHandler.NoAudio,
                    new ConfigDescription("What sound do you want to be played to alert you of a " + HazardNames[i] + " Audio", new AcceptableValueList<string>(ArrayOfAllAudio)));

                Priority[i] = plugin.Config.Bind(Category, HazardNames[i] + " Priority", DefaultHazardPriority[i],
                    new ConfigDescription("What priority do you want to assign a hazard (Higher number = Higher Priority)", new AcceptableValueList<int>(PriorityDropDown)));
            }
        }
    }
    internal class InstructionHazard
    {
        ConfigEntry<string>[] EnvironmentHazards;
        ConfigEntry<string> AudioOut;
        ConfigEntry<float> SecondsToCollision;
        ConfigEntry<int> LandingAltitude;
        public InstructionHazard(Plugin plugin, string[] ArrayOfAllAudio)
        {
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
            "To be played when there are [seconds to collision] time left before collision with ground. will not tirgger if gear is down",
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

            for (int i = 0; i < EnvironmentHazards.Length; i++)
            {
                EnvironmentHazards[i] = plugin.Config.Bind("Instruction Hazards", HazardNames[i], AudioHandler.NoAudio,
                    new ConfigDescription(HazardDescriptions[i], new AcceptableValueList<string>(ArrayOfAllAudio)));
            }
        }

    }
    #endregion


    public static Plugin I { get; private set; }
    internal static new ManualLogSource Logger;
    private const string FileModName = "Aeriicat-VWS";
    private Harmony Inj_Harmony;

    AudioHandler Audio;
    ExternalPackHandler PackHandler;


    Aircraft PlayerAircraft;
    FactionHQ PlayerHQ;

    bool NullPosition;

    ConfigEntry<int> CFG_Volume_Percent;
    ConfigEntry<float> CFG_MaxConsiderationDistance;

    //Bearings
    BearingAudConfig CFG_PositionCalloutAudio;


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
        CFG_MaxConsiderationDistance = Config.Bind("General", "Max Distance to analyse units", 10f,new ConfigDescription("Units outside of this distance will now be considered to be called out by the VWS", new AcceptableValueRange<float>(1, 50)));


        //new project. Lets import a bunch of stuff from LSTP and modularise it cos it was an unmodularised hell scape by the end
        //from the get go, lots allow for pack handling because people care about that a lot. 

        //PackHandler = new ExternalPackHandler(Path.GetDirectoryName(Info.Location), gameObject, CFG_Volume_Percent, out Audio);

        //CFG_PositionCalloutAudio = new BearingAudConfig(this, Audio.CreateArrayOfAudioNames());

        Inj_Harmony = new Harmony($"com.Aeriicatmeow.{FileModName}");
        Inj_Harmony.PatchAll();
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
    private void Update()
    {
        if (SceneSingleton<CombatHUD>.i != null & SceneSingleton<CombatHUD>.i.aircraft != null)
        {
            PlayerAircraft = SceneSingleton<CombatHUD>.i.aircraft;
            PlayerHQ = SceneSingleton<DynamicMap>.i.HQ;
        }
        else
        {
            NullPosition = true;
        }
    }
    public void ObserveUnitBearingFromMapIcon(Unit unit)
    {
        if (PlayerHQ != null & PlayerHQ != unit.NetworkHQ & unit.NetworkHQ != null)//If Enemy
        {
            GlobalPosition EnemyPosition = unit.GlobalPosition();
            GlobalPosition PlayerPosition = PlayerAircraft.GlobalPosition();
            float Distance = FastMath.Distance(EnemyPosition, PlayerPosition);
            if (Distance < CFG_MaxConsiderationDistance.Value * 1000f)
            {
                float dx = PlayerPosition.x - EnemyPosition.x;
                float dz = PlayerPosition.z - EnemyPosition.z;

                //Z is northways apparently...which isnt very intuative but oh well
                float RelativeBearing = (float)((Math.Atan2(dz, dx)+2*Math.PI)%(2*Math.PI)*180/Math.PI);
                Logger.LogInfo("Bearing to Hazard: " + unit.definition.roleIdentity.antiAi)r + " : " + RelativeBearing +" TO "+unit.GlobalPosition());
                Logger.LogInfo("Local Bearing: " + PlayerAircraft.rb.rotation.eulerAngles.y);
                Logger.LogInfo("Position " + PlayerAircraft.GlobalPosition());
                Logger.LogInfo("Pitch " + PlayerAircraft.rb.rotation.eulerAngles.x);
                //(Pitch,Yaw, Roll)
                //note that pitch is inverted
                //Bearings are slightly off but thats a nuclear option problem. not a me problem
            }
        }
    }
}
