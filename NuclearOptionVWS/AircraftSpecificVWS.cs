using BepInEx.Configuration;
using Lock_Shoot_Tone_Ping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuclearOptionVWS
{
    internal class AircraftSpecificVWS
    {
        private ConfigEntry<string>[] PacksForAircraft;
        private ConfigEntry<bool> Enabled;

        private static bool initialisedSuccessfully = false;

        private static string[] BDF =
            {
                @"SFB-81",//Darkreach
                @"FS-20",//Vortex
                @"FS-12",//Revoker
                @"A-19",//Brawler
                @"VT-7",//Vagrant
                @"EW-25",//Medusa
                @"VL-49",//Tarantula

                //Custom/modded
                @"FQ-106",//Kestrel
                @"FS-41",//Eclipse
                @"F-99"//Shrike
            };
        private static string[] PALA =
        {
                @"AB-4",//Alkyon
                @"KR-67",//Ifrit
                @"T/A-30",//Compass
                @"UH-90"//Ibis

        };

        public AircraftSpecificVWS(Plugin Plugin, ExternalPackHandler PackHandler)
        {
            const string SpecificVWSHeaderBase = "AircraftSpecificVWS";
            Enabled = Plugin.Config.Bind(SpecificVWSHeaderBase, "EnableAircraftSpecificVWS", false, "If enabled, aircraft will be able to have a specific VWS associated with it. The internal pack loaded value will be overridden. (this is useful if you wanna make PALA aircraft use RITA for example)");

            Plugin.I.Log(BepInEx.Logging.LogLevel.Info, 1);
            List<AircraftDefinition> AllAircrafts = Encyclopedia.i.aircraft;
            Plugin.I.Log(BepInEx.Logging.LogLevel.Info, 2);
            PacksForAircraft = new ConfigEntry<string>[AllAircrafts.Count];

            string[] NameOfAllPacks = PackHandler.GeneratePackNamesArray();


            string[] DefaultPackNames = ExternalPackHandler.GetDefaultPackNames();

            string SpecificVWSHeader = SpecificVWSHeaderBase;

            for (int i = 0; i < AllAircrafts.Count; i++)
            {
                //If BDF, use Betty, if PALA use Rita, else use Xiao

                string DefaultPack;
                if (BDF.Contains(AllAircrafts[i].code))
                {
                    DefaultPack = DefaultPackNames[1];
                    SpecificVWSHeader = SpecificVWSHeaderBase + "(BDF)";
                }
                else if (PALA.Contains(AllAircrafts[i].code))
                {
                    DefaultPack = DefaultPackNames[2];
                    SpecificVWSHeader = SpecificVWSHeaderBase + "(PALA)";
                }
                else
                {
                    DefaultPack = DefaultPackNames[3];
                    SpecificVWSHeader = SpecificVWSHeaderBase + "(Unknown/Undefinied/Undocumented Custom)";
                }


                if (!NameOfAllPacks.Contains(DefaultPack))
                {
                    DefaultPack = ExternalPackHandler.DefaultNotated;
                }
                PacksForAircraft[i] = Plugin.Config.Bind(SpecificVWSHeader, AllAircrafts[i].unitName, DefaultPack, new ConfigDescription("What audio pack do you want to be used for this aircraft?", new AcceptableValueList<string>(NameOfAllPacks)));
                //the default is :None: for stability sake. the true default is whatever it is here.

            }
        }
        public int GetNumberOfRegisteredAircraft() => PacksForAircraft.Length;
        public void Update(Aircraft PlayerAircraft, ExternalPackHandler PackHandler)
        {
            if (!Enabled.Value)
            {
                return;
            }

            string TrueCorrectPack = ExternalPackHandler.DefaultNotated;
            for(int i = 0; i < PacksForAircraft.Length; i++)
            {
                if (PacksForAircraft[i].Definition.Key == PlayerAircraft.definition.unitName)
                {
                    TrueCorrectPack = PacksForAircraft[i].Value;
                }
            }
            if(PackHandler.GetNameOfCurrentSelectedPack() != TrueCorrectPack)
            {
                PackHandler.CurrentSelectedPack().Value = TrueCorrectPack;
                PackHandler.UpdateActivePack();
            }
        }
        public static void EncylopediaBasedUpdate(AircraftSpecificVWS ASVWS)
        {
            if(Encyclopedia.i != null & initialisedSuccessfully)
            {
                if(Encyclopedia.i.aircraft.Count != ASVWS.GetNumberOfRegisteredAircraft())
                {
                    Plugin.I.Log(BepInEx.Logging.LogLevel.Info, "Discrepency in number of aircraft found. Correcting ASVWS");
                    initialisedSuccessfully = false;
                }
            }
        }

        public static void TryInitialise(ref AircraftSpecificVWS ASVWS,Plugin plugin, ExternalPackHandler PackHandler)
        {
            if (initialisedSuccessfully)
            {
                return;
            }
            if (Encyclopedia.i != null)
            {
                plugin.Log(BepInEx.Logging.LogLevel.Info, "Trying to initialise Aircraft specific VWS");
                ASVWS = new AircraftSpecificVWS(plugin, PackHandler);
                initialisedSuccessfully = true;
            }
            else
            {
                plugin.Log(BepInEx.Logging.LogLevel.Error, "Aircraft encyclopedia is currenly null");
                initialisedSuccessfully = false;
            }
        }
        public static void ResetInitialiseSuccess()
        {
            initialisedSuccessfully = false;
        }
    }
}
