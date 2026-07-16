using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Assertions.Must;
using BepInEx.Logging;
using UnityEngine.Playables;
using NuclearOptionVWS;
using BepInEx.Configuration;
using System.Linq;
using Unity.Audio;

namespace Lock_Shoot_Tone_Ping
{
    internal class ExternalPackHandler
    {
        public const string ConfigFileName = "Configs.txt";
        public const string DefaultNotated = ":[Default]:";
        public const char PackDataSplitChar = '=';
        private string DefaultPath;

        private Plugin MainPlugin;
        private ConfigEntry<string> CFG_EncodingType;
        private string CurrentPack;

        private List<PackAudioHandler> AudioHandlersForDifferentPacks;
        public AudioHandler GeneralAudioHandler;

        public ExternalPackHandler(string Root, GameObject Host, ConfigEntry<int> Volume, Plugin Plugin,out ConfigEntry<string> EncodingType, out AudioHandler GeneralAudioHandler)
        {
            MainPlugin = Plugin;
            string[] EncodingTypes = { "Raw", "Streamlined", "Simplified" };
            EncodingType = MainPlugin.Config.Bind("External Packs", "PackConfigEncoding", "Streamlined", new ConfigDescription("What Pack Encoding Type do you want to save the current configs as (change will occur when you close the game or when you change active pack)", new AcceptableValueList<string>(EncodingTypes)));
            this.CFG_EncodingType = EncodingType;

            string FileModName = Plugin.I.GetFileModName();
            string PRoot = $"{Root}\\Packs";
            DefaultPath = Root + "\\Audio";
            GeneralAudioHandler = new AudioHandler(Host, Volume, DefaultPath);
            this.GeneralAudioHandler = GeneralAudioHandler;
            if (!Directory.Exists(PRoot))
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Error, "External packs Folder Not Found [In External Pack Handler]. Replacement being generated");
                Directory.CreateDirectory(PRoot);
            }

            AudioHandlersForDifferentPacks = new List<PackAudioHandler>();


            
            //In reference to specifically ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
            //Is this a shit way of doing it?
            //Most certainly.
            //But as ive made the audio handler file mostly discrete, this is also the easiest way of doing this.
            //If the people demand more stuff to do with packs, redo this part properly please.

            foreach (string Path in Directory.GetDirectories(PRoot))
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Info, "Analysing pack at: " + Path);
                if (!File.Exists(Path + "\\" + ConfigFileName))
                {
                    Plugin.I.Log(BepInEx.Logging.LogLevel.Error, "Config File Not Found. Pack Is not suitable for use. Rejecting Pack");
                }
                else
                {

                    AudioHandlersForDifferentPacks.Add(new PackAudioHandler(Path));
                }
            }

            //Plugin.Plugin.I.Log.LogInfo(DefaultConfigPath);
            Plugin.I.Log(LogLevel.Info, "Injecting Default Pack into Handler");
            AudioHandlersForDifferentPacks.Insert(0, PackAudioHandler.GenerateDefaultStandin(this));
            Plugin.I.Log(LogLevel.Info, "Injecting Audio into AudioHandler");
            GeneralAudioHandler.InjectAudioClips(GenerateArrayAllAudioEver());
            Plugin.I.Log(LogLevel.Info, "External Pack Handler Generated");
        }
       
        public string GetDefaultConfigPath() => DefaultPath+ "\\" + ConfigFileName;
        public string GetDefaultPath() => DefaultPath;
        public string[] GeneratePackNamesArray()
        {
            
            string[] Names = new string[AudioHandlersForDifferentPacks.Count];
            for(int i = 0; i < Names.Length; i++)
            {
                Names[i] = AudioHandlersForDifferentPacks[i].Name;
            }
            return Names;
        }
        public PackAudioHandler GetPackAudioHandlerFromName(string Name)
        {
            foreach(PackAudioHandler PAH in AudioHandlersForDifferentPacks)
            {
                if(PAH.Name == Name)
                {
                    return PAH;
                }
            }

            return new PackAudioHandler();
        }
        public int GetNumberOfLoadedPacks()
        {
            int i = 0;
            try
            {
                 i = AudioHandlersForDifferentPacks.Count;
            }
            catch(Exception EXP)
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Fatal, EXP);
            }
            return i;
        }

        public AudioClip[] GenerateArrayAllAudioEver()
        {
            Plugin.I.Log(LogLevel.Info, "Number Of Packs: " + AudioHandlersForDifferentPacks.Count);
            int totalCount = 0;
            foreach(PackAudioHandler PAH in AudioHandlersForDifferentPacks)
            {
                if (!PAH.IsNull)
                {
                    totalCount += PAH.Audio.Length;
                    //Plugin.I.Log(LogLevel.Info, totalCount);
                }
            }
            AudioClip[] ReturnArray = new AudioClip[totalCount];
            totalCount = 0;
            foreach (PackAudioHandler PAH in AudioHandlersForDifferentPacks)
            {
                if (!PAH.IsNull)
                {
                    foreach (AudioClip a in PAH.Audio)
                    {
                        //Plugin.I.Log(LogLevel.Info, totalCount);
                        ReturnArray[totalCount] = a;
                        totalCount++;
                    }
                }
            }
            return ReturnArray;
        }

        #region from LSTP main plugin class
        private void WriteConfigsToExternalFile(string Path, Dictionary<string,ConfigEntryBase> BigConfigDictionary)
        {
            //string text = "";
            //foreach(string s in BigConfigDictionary.Keys)
            //{
            //    text += s + "#" + BigConfigDictionary[s].BoxedValue  +"\n";
            //}
            //File.WriteAllText(Path, text);
            if (CFG_EncodingType.Value == "Streamlined")
            {
                Plugin.I.Log(LogLevel.Info,"Writing StreamLined");
                File.WriteAllLines(Path, PackAudioHandler.ConvertToStreamLined(GetCurrentConfig(BigConfigDictionary), false));
            }
            else if (CFG_EncodingType.Value == "Simplified")
            {
                Plugin.I.Log(LogLevel.Info,"Writing Simplified");
                File.WriteAllLines(Path, PackAudioHandler.ConvertToStreamLined(GetCurrentConfig(BigConfigDictionary), true));
            }
            else
            {
                Plugin.I.Log(LogLevel.Info,"Writing Raw");

                File.WriteAllLines(Path, GetCurrentConfig(BigConfigDictionary));
            }
        }
        private string[] GetCurrentConfig(Dictionary<string,ConfigEntryBase> BigConfigDictionary)
        {
            Regex RevertPrefix = new Regex(@"^\[.+\] (.+)");
            int index = 0;
            string[] CFG = new string[BigConfigDictionary.Count];
            foreach (string s in BigConfigDictionary.Keys)
            {
                object CurrentValue = BigConfigDictionary[s].BoxedValue;
                if (CurrentValue.GetType() == typeof(string))
                {
                    Match m = RevertPrefix.Match((string)CurrentValue);
                    if (m.Success & CurrentPack != ExternalPackHandler.DefaultNotated)
                    {
                        CurrentValue = m.Groups[1].Value;
                    }
                }

                CFG[index] = s + PackDataSplitChar + CurrentValue;

                index++;
            }
            return CFG;
        }
        private static void LoadConfigsFromText(PackAudioHandler PackAudioHandler, Dictionary<string, ConfigEntryBase> Dictionary, bool AudioNamePrefix = false, bool ResetUndefined = true)//this doesnt work for the arrays of configs. how odd.
        {
            if (PackAudioHandler.Name == ExternalPackHandler.DefaultNotated)
            {
                AudioNamePrefix = false;
                Plugin.I.Log(LogLevel.Info,"AudioName prefix set to false as defualt pack is being loaded ad so has no prefix");
            }

            if (ResetUndefined & PackAudioHandler.Configs.Length > 0)
            {
                foreach (string s in Dictionary.Keys)
                {
                    ConfigEntryBase CFG = Dictionary[s];
                    CFG.BoxedValue = CFG.DefaultValue;
                }
            }
            else
            {
                Plugin.I.Log(LogLevel.Info,"Refusing to wipe existing configs. (in line with configs)");
            }

            Regex ConfigComb = new Regex(@"^(.+)\" + PackDataSplitChar + "(.+)$");
            Regex NumberOnlyCheck = new Regex(@"^[\d|\.]+$");
            string[] Data;

            bool IsSimplified;

            if (PackAudioHandler.DetectEncodingType(PackAudioHandler.Configs, out IsSimplified) == 's')
            {
                Plugin.I.Log(LogLevel.Info,"Streamlined data detected. Converting to Raw");
                if (IsSimplified)
                {
                    Plugin.I.Log(LogLevel.Info,"Simplfied Streamline data detected.");
                }
                Data = PackAudioHandler.ConvertToRaw(PackAudioHandler.Configs, IsSimplified);
            }
            else
            {
                Plugin.I.Log(LogLevel.Info,"Data is already Raw");
                Data = PackAudioHandler.Configs;
            }
            bool[] HasEntry = new bool[Dictionary.Count];
            foreach (string s in Data)
            {
                if (ConfigComb.Match(s).Success)
                {
                    string[] data = s.Split(PackDataSplitChar);
                    if (Dictionary.Keys.Contains(data[0]))
                    {
                        //[0] is the config field, [1] is the value;
                        ConfigEntryBase SpecificConfig = Dictionary[data[0]];

                        //Plugin.I.Log.LogInfo("Processing " + SpecificConfig.Definition);
                        //Plugin.I.Log.LogInfo("OLD:" + SpecificConfig.BoxedValue);
                        if (SpecificConfig.GetType() == typeof(ConfigEntry<bool>))
                        {
                            SpecificConfig.BoxedValue = data[1] switch
                            {
                                "True" => true,
                                "False" => false,
                                _ => false
                            };
                            //Plugin.I.Log.LogInfo("This is boolean");
                        
                        }
                        else if (SpecificConfig.GetType() == typeof(ConfigEntry<string>))
                        {
                            if (AudioNamePrefix)
                            {
                                SpecificConfig.BoxedValue = PackAudioHandler.Prefix(data[1]);
                            }
                            else
                            {
                                SpecificConfig.BoxedValue = data[1];
                            }
                            //Plugin.I.Log.LogInfo("replacing " + SpecificConfig.Definition + " with " + data[1]);

                        }
                        else if (SpecificConfig.GetType() == typeof(ConfigEntry<int>))
                        {
                            if (NumberOnlyCheck.Match(data[1]).Success)
                            {
                                SpecificConfig.BoxedValue = System.Convert.ToInt32(data[1]);

                                //Plugin.I.Log.LogInfo("replacing " + SpecificConfig.Definition + " with " + data[1]);
                            }
                            else
                            {
                                Plugin.I.Log(LogLevel.Error, "Integer expected. value could not be parsed to integer. value: " + data[1]);
                            }
                        }
                        else if (SpecificConfig.GetType() == typeof(ConfigEntry<float>))
                        {
                            if (NumberOnlyCheck.Match(data[1]).Success)
                            {
                                SpecificConfig.BoxedValue = float.Parse(data[1]);

                                //Plugin.I.Log.LogInfo("replacing " + SpecificConfig.Definition + " with " + data[1]);
                            }
                            else
                            {
                                Plugin.I.Log(LogLevel.Error, "Float expected. value could not be parsed to Float. value: " + data[1]);
                            }
                        }
                        else
                        {
                            Plugin.I.Log(LogLevel.Error, "Unknown config. " + SpecificConfig.GetType());
                        }

                        //Plugin.I.Log.LogInfo("NEW:" + SpecificConfig.BoxedValue);

                    }
                }

            }
        }
        #endregion
    }

    internal class PackAudioHandler
    {
        public AudioClip[] Audio;
        public string Name;
        public string[] Configs;
        public string Path;
        public bool IsNull;

        private const char PackSplitChar = '=';

        private static Regex LastInPath = new Regex(@"^.*[\\]([^\\]*$)");
        public PackAudioHandler(AudioClip[] Audio, string PackName, string[] PackConfigs, string Path)
        {
            this.Audio = Audio;
            Name = PackName;
            Configs = PackConfigs;
            this.Path = Path;
            IsNull = false;
        }
        public PackAudioHandler(string Path)
        {
            this.Path = Path;
            if (File.Exists(GetConfigPath()))
            {
                Plugin.I.Log(LogLevel.Info, "fetching Audio");
                Audio = AudioHandler.LoadAudioFromFolder(Path);
                Plugin.I.Log(LogLevel.Info, "fetching Config");
                Configs = File.ReadAllLines(GetConfigPath());
                Plugin.I.Log(LogLevel.Info, "Defining commonly used variables");
                Name = LastInPath.Match(Path).Groups[1].Value;
                IsNull = false;

                foreach(AudioClip a in Audio)
                {
                    a.name = Prefix(a.name);
                }
                Plugin.I.Log(LogLevel.Info, Name + " Is fetched!");
            }
            else
            {
                Plugin.I.Log(LogLevel.Error, "Config not found in: " + Path + ". Refusing to generate pack. Marked as Null");
                IsNull = true;
            }

        }
        public PackAudioHandler()
        {
            IsNull = true;
        }
        public static PackAudioHandler GenerateDefaultStandin(ExternalPackHandler PackHandler)
        {
            PackAudioHandler PAH;
            if (File.Exists(PackHandler.GetDefaultConfigPath()))
            {
                 PAH = new PackAudioHandler(new AudioClip[0], ExternalPackHandler.DefaultNotated, File.ReadAllLines(PackHandler.GetDefaultConfigPath()), PackHandler.GetDefaultPath());
            }
            else
            {
                PAH = new PackAudioHandler(new AudioClip[0], ExternalPackHandler.DefaultNotated, new string[0], PackHandler.GetDefaultPath());
                File.WriteAllText(PackHandler.GetDefaultConfigPath(), "");
            }
            return PAH;
        }
        public string GetConfigPath() => Path + "\\" + ExternalPackHandler.ConfigFileName;
        public string GetAudioNamePrefix() => "[" + Name + "] ";
        public string Prefix(string Base) => GetAudioNamePrefix() + Base;
        public static char DetectEncodingType(string[] Data, out bool IsSimplified)
        {
            IsSimplified = true;
            Regex Comb = new Regex(@"^(.+)\" + PackSplitChar + "(.+)$");
            Regex IsSimplifiedComb = new Regex(@"Set \d Sounds");
            foreach (string s in Data)
            {
                //Plugin.I.Log(LogLevel.Info, s);

                if (IsSimplifiedComb.Match(s).Success)
                {
                    IsSimplified = false;
                }

            }
            foreach (string s in Data)
            {
                //Plugin.I.Log(LogLevel.Info, s);

                if (!Comb.Match(s).Success)
                {
                    return 's';
                }
               // Plugin.I.Log(LogLevel.Info, s);
                
            }
            IsSimplified = false;
            return 'r';
        }
        public static string[] ConvertToStreamLined(string[] Raw, bool IsSimplified = false)
        {
            int Count = 0;
            List<StreamLinedCategory> Categories = new List<StreamLinedCategory>();
            foreach(string s in Raw)
            {
                
                string[] dat = s.Split('.');
                StreamLinedCategory tmp = StreamLinedCategory.SearchByName(dat[0], Categories);
                if (tmp == null)
                {
                    //Plugin.I.Log(LogLevel.Info, "Category Doesnt Already Exist, Creating New");
                    tmp = new StreamLinedCategory(dat[0]);
                    Categories.Add(tmp);
                    Count+=2;
                }
                tmp.Data.Add(dat[1]);
                Count++;
            }

            if (IsSimplified)
            {

                Plugin.I.Log(LogLevel.Info, "All Categories:");
                foreach(StreamLinedCategory SLC in Categories)
                {
                    Plugin.I.Log(LogLevel.Info,SLC.Header);
                }

                Regex WeaponSetMarker = new Regex(@".+_Set$");
                string PrevWeaponSet = "";
                foreach(string s in Raw)
                {
                    string[] dat = s.Split('.');
                    string[] dat2 = dat[1].Split(PackSplitChar);
                    if (WeaponSetMarker.Match(dat2[0]).Success)
                    {
                        string WeaponSpecific = dat2[0];
                        StreamLinedCategory CurrentEntry = StreamLinedCategory.SearchByName(dat[0], Categories);
                        if (WeaponSpecific != PrevWeaponSet)
                        {
                            CurrentEntry.RemoveDataWith(WeaponSpecific);
                            PrevWeaponSet = WeaponSpecific;
                            CurrentEntry.Data.Add("-" + WeaponSpecific);
                            //Count Neutral.
                            Plugin.I.Log(LogLevel.Info, "New SubCategory: " + WeaponSpecific);
                        }

                        CurrentEntry.CombineData(
                            StreamLinedCategory.SearchByName("Set " + dat2[1] + " Sounds", Categories)
                            );
                        Count -= 2;
                    }
                }
            }

            string[] ReturnFile = new string[Count];
            Count = 0;

            Plugin.I.Log(LogLevel.Info, "Final Approach");
            Regex SetDetector = new Regex(@"Set (\d+) Sounds");
            foreach (StreamLinedCategory s in Categories)
            {
                if (!SetDetector.Match(s.Header).Success || !IsSimplified)
                {
                    Count++;
                    ReturnFile[Count] = "[" + s.Header + "]";
                    Count++;
                    foreach (string d in s.Data)
                    {
                        if (d[0] != '-')
                        {
                            
                            string[] dat = d.Split(PackSplitChar);
                            ReturnFile[Count] = dat[0] + PackSplitChar + dat[1];
                            
                        }
                        else
                        {
                            
                            ReturnFile[Count] = d;
                        }
                        Count++;
                    }
                }
            }
            return ReturnFile;
            
        }
        public static string[] ConvertToRaw(string[] StreamLined, bool IsSimplified = false)
        {
            Plugin.I.Log(LogLevel.Info, "Converting to Raw");

            //foreach(string s in StreamLined)
            //{
            //    Plugin.I.Log(LogLevel.Info, s);
            //}

            string ReturnString = "";

            Regex CategoryComb = new Regex(@"^\[(.+)\]$");
            Regex FieldComb = new Regex(@"^(.+)\"+PackSplitChar+"(.+)$");
            Regex SimplifiedCategoryComb = new Regex(@"-(.+)");
            string Category = "";
            Plugin.I.Log(LogLevel.Info, "First Part Of Converter");
            foreach(string s in StreamLined)
            {
                Match m = CategoryComb.Match(s);
                if (m.Success)
                {
                    Category = m.Groups[1].Value;
                }
                else if (SimplifiedCategoryComb.Match(s).Success & IsSimplified)
                {
                    Category = SimplifiedCategoryComb.Match(s).Groups[1].Value;
                }
                else
                {
                    m = FieldComb.Match(s);
                    if (m.Success)
                    {
                        ReturnString += Category+"."+ m.Groups[1].Value + PackSplitChar + m.Groups[2].Value + "\n";
                    }
                }
            }


            if (IsSimplified)
            {
                Plugin.I.Log(LogLevel.Info, "Second part (due to simplified format)");
                //Plugin.I.Log(LogLevel.Info, ReturnString);
                StreamLined = ReturnString.Split("\n");
                ReturnString = "";
                int InternalCounter = -1;
                string OldPrefix = "";
                string LastArchset = "";
                Regex SoundSetComb = new Regex(@"^\d\)\s");//new Regex(@".*\d\)\s(Nez|Shoot|Locking)Sound$");
                foreach (string s in StreamLined)
                {
                    if (s.Length > 0)
                    {
                        Plugin.I.Log(LogLevel.Info, s);
                        string[] dat = s.Split('.');
                        string dat2 = dat[1].Split(PackSplitChar)[0];
                        if (SoundSetComb.Match(dat2).Success)
                        {
                            if (OldPrefix != dat[0])
                            {
                                OldPrefix = dat[0];

                                InternalCounter++;
                                Plugin.I.Log(LogLevel.Info, dat[0]);

                                ReturnString += LastArchset+"."+dat[0] + PackSplitChar.ToString() + InternalCounter.ToString() + "\n";



                            }
                            ReturnString += "Set " + InternalCounter + " Sounds." + dat[1] + "\n";
                        }
                        else
                        {
                            LastArchset = dat[0];
                            ReturnString += s + "\n";
                        }

                    }
                }
               
            }

            //Plugin.I.Log(LogLevel.Info,"RAW INFERRED:");
            //Plugin.I.Log(LogLevel.Info, ReturnString);
            return ReturnString.Split("\n");
        }
        internal class StreamLinedCategory
        {
            public string Header;
            public List<string> Data;
            public StreamLinedCategory(string Header)
            {
                this.Header = Header;
                Data = new List<string>();
            }
            public static bool ContainsName(string Name, List<StreamLinedCategory> List)
            {
                foreach(StreamLinedCategory s in List)
                {
                    if (s.Header == Name)
                    {
                        return true;
                    }
                }
                return false;
            }
            public static StreamLinedCategory SearchByName(string Name, List<StreamLinedCategory> List)
            {
                foreach (StreamLinedCategory s in List)
                {
                    if (s.Header == Name)
                    {
                        return s;
                    }
                }
                //Plugin.I.Log(LogLevel.Error, "Category Not Found " + Name);
                return null;
            }

            public static bool CheckIfSameData(StreamLinedCategory S1, StreamLinedCategory S2)
            {
                bool ReturnValue = true;
                if(S1.Data.Count == S2.Data.Count)
                {
                    foreach(string s in S1.Data)
                    {
                        if (!S2.Data.Contains(s))
                        {
                            ReturnValue = false;
                        }
                    }
                }
                else
                {
                    ReturnValue = false;
                }
                return ReturnValue;
            }
            public void CombineData(StreamLinedCategory OtherCategory, string PrependIdentifyer = "")
            {
                foreach(string s in OtherCategory.Data)
                {
                    this.Data.Add(PrependIdentifyer+s);
                }
            }
            public void RemoveDataWith(string String)
            {
                Regex R = new Regex(String);
                for(int i = 0; i < Data.Count; i++)
                {
                    if (R.Match(Data[i]).Success)
                    {
                        Data.RemoveAt(i);
                        i--;
                        if (i >= Data.Count)
                        {
                            break;
                        }
                    }


                }
            }
        }
    }
}
