using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NuclearOptionVWS
{
    //temporary class. While it will remain in the source code for archiving sake, i will most likely disable it.
    internal static class Unit_AAThreat_Ripper
    {
        private static List<string> UnitTypeName;
        private static List<string> UnitCodes;
        private static List<float> AAThreat;
        private static bool Initialised = false;
        public static void Initialise()
        {
            UnitTypeName = new List<string>();
            UnitCodes = new List<string>();
            AAThreat = new List<float>();
            Initialised = true;
        }
        public static void ConsiderUnitForList(Unit unit)
        {
            try
            {
                if (Initialised & unit.NetId != 0)
                {
                    if (!UnitTypeName.Contains(unit.definition.unitName) & unit.definition.typeIdentity.missile < 0.5)
                    {
                        string code;
                        if (unit.definition.typeIdentity.air > 0.5)
                        {
                            code = "AIRCRAFT";
                        }
                        else
                        {
                            code = unit.definition.code;
                        }
                        int Index = UnitCodes.IndexOf(code);
                        if (Index != -1)
                        {
                            UnitTypeName.Insert(Index, unit.definition.unitName);
                            UnitCodes.Insert(Index, code);
                            AAThreat.Insert(Index, HostileHazardConfig.GetAAThreat(unit));
                        }
                        else
                        {
                            UnitTypeName.Add(unit.definition.unitName);
                            UnitCodes.Add(code);
                            AAThreat.Add(HostileHazardConfig.GetAAThreat(unit));

                        }

                    }
                }
            }
            catch(Exception EXP)
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Fatal, EXP);
            }
        }
        public static void DumpData(string Path)
        {
            if (Initialised)
            {
                string CurrentCode = "";
                string ReturnString = "";
                for (int i = 0; i < UnitTypeName.Count; i++)
                {
                    if (UnitCodes[i] != CurrentCode)
                    {
                        ReturnString += "["+UnitCodes[i] + "]\n";
                        CurrentCode = UnitCodes[i];
                    }
                    ReturnString += UnitTypeName[i] + " = " + AAThreat[i] +"\n";
                }

                File.WriteAllText(Path, ReturnString);
            }
        }
    }

    public static class DebugMode
    {
        private static Plugin BasePlugin;
        private static ConfigEntry<bool> ConfigEnabled;
        public static void Initialise(Plugin ExternalBasePlugin)
        {
            BasePlugin = ExternalBasePlugin;
            ConfigEnabled = BasePlugin.Config.Bind("General", "Debug Mode", false, "If enabled, mod will create larger debug logs in log output.log. This does cost some performance so only enable it to collect data before submitting a bug report.");

        }

        public static void Log(LogLevel Loglevel, object Data, string CallingScript = "")
        {
            BasePlugin.Log(Loglevel, "[DEBUG MODE+ |"+CallingScript+"@+"+Time.timeSinceLevelLoad+"| ]: " + Data);
        }

        public static void LogInfo(object Data, string CallingScript)
        {
            Log(LogLevel.Info, Data);
        }
        public static void LogError(object Data, string CallingScript)
        {
            Log(LogLevel.Error, Data);
        }
        public static void LogFatal(object Data, string CallingScript)
        {
            Log(LogLevel.Fatal, Data);
        }
    }
}
