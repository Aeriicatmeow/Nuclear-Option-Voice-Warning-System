using BepInEx.Logging;
using HarmonyLib.Tools;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Yoga;
using System.IO;
using BepInEx.Configuration;
using System.Linq;
using System.Diagnostics.Tracing;
using NuclearOptionVWS;
using HarmonyLib;

namespace Lock_Shoot_Tone_Ping
{

    internal class AudioHandler
    {
        private AudioSource Source;
        private ConfigEntry<int> Volume_Percent;
        private AudioClip[] AllAudio;
        private bool SeeCompleteNoExceptions;
        public static string NoAudio = ":[NONE]:";

        private Dictionary<string, AudioClip> AudioDictionary;
        private Queue<AudioClip> AudioQueue;

        //private float DefaultPitch;
        public AudioHandler(GameObject Host, ConfigEntry<int> Volume_Percent, string Root)
        {

            Plugin.I.Log(LogLevel.Info, $"Attempting to Load Audio from {Root}");
            AllAudio = LoadAudioFromFolder(Root);
            Plugin.I.Log(LogLevel.Info, $"Audio Loaded");
            

            Source = Host.AddComponent<AudioSource>();//We can use one audio source because NoLock doesnt play at the same time as NEZ and Lock now does it?
            Source.playOnAwake = false;
            this.Volume_Percent = Volume_Percent;
            Source.loop = true;
            SeeCompleteNoExceptions = false;
            //DefaultPitch = Source.pitch;

            InitialiseAudioDictionary();
            
        }

        private void InitialiseAudioDictionary()
        {
            AudioDictionary = new Dictionary<string, AudioClip>();
            foreach (AudioClip a in AllAudio)
            {
                AudioDictionary.TryAdd(a.name, a);
            }
            AudioQueue = new Queue<AudioClip>();
        }

        #region AudioPlaying

        public void SetPitch(float Pitch)
        {
            Source.pitch = Pitch;
            //Plugin.I.Log(LogLevel.Info, "Request recieved to set pitch: " + Pitch);
        }
        public void ResetPitch()
        {
            Source.pitch = 1;
            //Plugin.I.Log(LogLevel.Info, "Request recieved to reset pitch");
        }
        public void AmendNoExceptionClause(bool NewValue)
        {
            SeeCompleteNoExceptions = NewValue;
        }
        public void Stop()
        {
            if (SeeCompleteNoExceptions & Source.isPlaying)
            {
                return;
            }
            //Plugin.I.Log(LogLevel.Info,"Stopping old Audio");
            if(Source == null)
            {
                return;//Ok this probably isnt nessecary but i have never used unity functions so if the code below causes a noticeable delay then this is probably helpful.
            }
            Source.Stop();
            Source.clip = null;
            SeeCompleteNoExceptions = false;
        }
        public void PlayAudio(AudioClip Audio, bool SeeComplete = false, float Pitch = 1)//As these are meant for alarms, I will have the audio continue to play the whole audio until different audio is needed.
            //thats 33% more audio in every audio. Such a great innovation for appature science.
        {
            //Plugin.I.Log(LogLevel.Info, "play request recieved");
            //if (Source.clip != null)
            //{
            //    Plugin.I.Log(LogLevel.Message, "Currently: " + Source.clip.name + " will play " + Audio.name + ". Is playing: " + Source.isPlaying);
            //}
            //else
            //{
            //    Plugin.I.Log(LogLevel.Info, "Source is currently null");
            //}
            if (Pitch != float.NaN)
            {
                SetPitch(Pitch);
            }
            if (SeeCompleteNoExceptions & Source.isPlaying)
            {
                return;
            }
            SeeCompleteNoExceptions = SeeComplete;
            Source.volume = Mathf.Min(Mathf.Clamp(Volume_Percent.Value / 100f, 0f, 2f), 2f);//Yes, this is just ripped from yappinator but a bird told me that unty api stuff is faster cos its all in c++

            if (Audio == Source.clip & Source.isPlaying)
            {
                //Plugin.I.Log(LogLevel.Info, "Chose not to play audio. already playing");
            }
            else
            {
                if (Audio != null)
                {
                    Plugin.I.Log(LogLevel.Info, "Attempting to play " + Audio.name);
                    if (Audio != Source.clip && Source.isPlaying)
                    {
                        Stop();
                    }
                    if (SeeCompleteNoExceptions)
                    {
                        Source.loop = false;
                    }
                    else
                    {
                        Source.loop = true;
                    }
                    Source.clip = Audio;
                    Source.Play();

                }
                else
                {
                    if (Source.isPlaying)
                    {
                        Stop();
                    }
                    //Plugin.I.Log(LogLevel.Info, "Playing Null");
                    //Plugin.I.Log(LogLevel.Error, "Could Not Play, Audio Null");
                }
            }
            //Plugin.I.Log(LogLevel.Info, "attempt play finished");
        }
        
        //public AudioClip SimpleSearchForAudio(string Name)
        //{
        //    foreach (AudioClip a in AllAudio)
        //    {
        //        if (a != null)
        //        {
        //            if (Name == a.name)
        //            {
        //                return a;
        //            }
        //        }
        //    }
        //    return null;
        //}
        public Dictionary<string, AudioClip> GetAudioDictionary() => AudioDictionary;
        public AudioClip Search(string Name)
        {
            AudioClip ReturnClip;
            if(AudioDictionary.TryGetValue(Name, out ReturnClip))
            {
                return ReturnClip;
            }
            else
            {
                return null;
            }
        }

        public string[] CreateArrayOfAudioNames()
        {
            Plugin.I.Log(LogLevel.Info, "Generating audio list");
            string[] AudioNames = new string[AllAudio.Length + 1];
            for (int i = 1; i <= AllAudio.Length; i++)
            {
                AudioNames[i] = AllAudio[i - 1].name;
            }
            Plugin.I.Log(LogLevel.Info, "Appending Audio List");
            AudioNames[0] = NoAudio;
            return AudioNames;
        }
        public bool VerifyAudioExists(string Name)
        {
            foreach(AudioClip a in AllAudio)
            {
                if(a.name == Name)
                {
                    return true;
                }
            }
            return false;
        }


        #endregion

        #region Audio Loading
        //technically poor programming ettiquette but ive just come out of an inorganic chemistry exam so i dont care
        public static AudioClip[] LoadAudioFromFolder(string Path)
        {
            if (!Directory.Exists(Path))
            {
                Plugin.I.Log(LogLevel.Error, "Audio Directory Not Found");
                return new AudioClip[0];
            }

            string[] AllFoundAudio = SearchAndFilter(Path, @"\.mp3$|\.ogg$|\.wav$");
            AudioClip[] ReturnArray = new AudioClip[AllFoundAudio.Length];

            Plugin.I.Log(LogLevel.Info, AllFoundAudio.Length+" audio files found");
            

            for (int i = 0; i < AllFoundAudio.Length; i++)
            {
                ReturnArray[i] = LoadAudioFromFile(AllFoundAudio[i]);
            }
            return ReturnArray;
        }
        public static string[] SearchAndFilter(string Path, string Regex)//there is definitely a better way of doing this but I couldnt find it
        {
            //string RegexFilter = "";
            //foreach(string s in Extensions)
            //{
            //    if(RegexFilter.Length > 0)
            //    {
            //        RegexFilter += "|";
            //    }
            //    RegexFilter += "\\" + s +"$";
            //}
            Regex Filter = new Regex(Regex);
            string[] AllPaths = Directory.GetFiles(Path);
            int ReturnArrayCount = 0;
            for(int i = 0; i < AllPaths.Length; i++)
            {
                if (Filter.Match(AllPaths[i]).Success)
                {
                    ReturnArrayCount++;
                }
            }
            //I heard that converting a list to an array was pretty intensive so this way is better. especially for a low number of files.
            string[] ReturnArray = new string[ReturnArrayCount];
            for(int i = 0; i < AllPaths.Length; i++)
            {
                if (Filter.Match(AllPaths[i]).Success)
                {
                    ReturnArrayCount--;
                    ReturnArray[ReturnArrayCount] = AllPaths[i];
                }
            }
            return ReturnArray;
        }
        public static AudioClip LoadAudioFromFile(string Path)
        {
            Match RegexMatch = new Regex(@"^.*\\(.*)\.(\w+)$").Match(Path);
            AudioType Type = GetAudioType(RegexMatch);
            if (Type == AudioType.UNKNOWN)
            {
                Plugin.I.Log(LogLevel.Error, "File Type Unknown: " + Path);
                return null;
            }
            else
            {
                UnityWebRequest Loader = UnityWebRequestMultimedia.GetAudioClip(Path, Type);

                //I hope this doesnt hang. There must be a better way.
                //But it works for Yappinator so it should work for this.
                //Thx Nikkorap btw

                try
                {
                    Plugin.I.Log(LogLevel.Info, "Requesting Audio From Directory " + Path);
                    Loader.SendWebRequest();
                    while (!Loader.isDone) { }

                    if (Loader.error != null)
                    {
                        Plugin.I.Log(LogLevel.Error, "Error Requesting Audio From Directory Using UnityWebRequestMultimMedia\n Error: " + Loader.error);
                        return null;
                    }
                    AudioClip Audio = DownloadHandlerAudioClip.GetContent(Loader);
                    if (Audio && Audio.loadState == AudioDataLoadState.Loaded)
                    {
                        Audio.name = RegexMatch.Groups[1].Value;
                        return Audio;
                    }

                    Plugin.I.Log(LogLevel.Error, "Undefinied Error Loading Audio from " + Path);
                    return null;
                }
                catch (Exception EXP)
                {
                    Plugin.I.Log(LogLevel.Fatal, "Fatal error loading audio: " + EXP);
                    return null;
                }
            }
        }
        private static AudioType GetAudioType(Match RegexMatch)
        {
            return RegexMatch.Groups[2].Value.ToLower() switch
            {
                "wav" => AudioType.WAV,
                "mp3" => AudioType.MPEG,
                "ogg" => AudioType.OGGVORBIS,
                _ => AudioType.UNKNOWN,

            };
        }

        //private static AudioType GetAudioType(string Path)
        //{
        //    Regex Prefix = new Regex(@"^.*\\(.*)\.(\w+)$");
        //    Prefix.Match(Path);
        //    return Prefix.GroupNameFromNumber(1).ToLower() switch
        //    {
        //        "wav" => AudioType.WAV,
        //        "mp3" => AudioType.MPEG,
        //        "ogg" => AudioType.OGGVORBIS,
        //        _  => AudioType.UNKNOWN, 

        //    };
        //}
        #endregion
        #region AudioQueue
        public void Update()
        {
            if (!Source.isPlaying & AudioQueue.Count > 0)
            {
                PlayAudio(AudioQueue.Dequeue(),true);
            }
        }
        public void AddToQueue(string Name)
        {
            AudioQueue.Enqueue(Search(Name));
        }
        public void AddToQueue(AudioClip Audio)
        {
            AudioQueue.AddItem(Audio);
        }
        public void ClearQueue()
        {
            AudioQueue.Clear();
        }
        public Queue<AudioClip> GetQueue() => AudioQueue;
        public int GetQueueLength() => AudioQueue.Count;
        #endregion

        public void InjectAudioClips(AudioClip[] InjectedAudio)
        {
            AudioClip[] NewAudioSet = new AudioClip[AllAudio.Length + InjectedAudio.Length];
            int pointer = 0;
            foreach(AudioClip a in AllAudio)
            {
                NewAudioSet[pointer] = a;
                pointer++;
            }
            foreach(AudioClip a in InjectedAudio)
            {
                NewAudioSet[pointer] = a;
                pointer++;
            }
            AllAudio = NewAudioSet;

            InitialiseAudioDictionary();
        }
    }
}
