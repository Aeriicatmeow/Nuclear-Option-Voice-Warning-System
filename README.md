# (Aeriicat's) Nuclear Option Voice Warning System
This mod expands on the voice warning system in the base game adding a variety of callouts for external hazards (such as enemy aircraft, missiles or ground units) and internal hazards (Such as altitude warnings, AoA and Gforce warnings as well as recommendation for ejection)

With each external hazard, A bearing to the hazard, as well as its altitude to relative to the aircraft is given. Any advice for counterplay (such as with missiles) will also follow if the option for this is enabled.

This mod has inherited a fair bit of code for Lock Shoot Tone Ping and so has a very similar config and file handling system to that mod.
Its installation instructions are also identical but I will cover them again along side how to operate the pack handling system in this README too.

## INSTALLATION GUIDE

Copy Dll into Bepinex plugins folder.

Run the game, this will generate the required file structure for the mod. The file should also be automatically moved into its own folder under the same name:
```
  Plugins
    ¬NuclearOption-VWS
      ¬NuclearOptionVWS.Dll
      ¬Audio
      ¬Packs
        ¬[External Pack Folders Here]
```

You then want to add any audio files you may want to use to the audio folder and then open the game.
In game, you want to open the Bepinex Configuration Manager (F1 Key by default) and for each sound category you want to select the relevant file name from the dropdown list.
Alternately, you can manually edit you Configs.txt file 

Alternately, I may add a compressed file of the entire mod and the audio files and packs that I use for the NOMM edition of the mod to allow users to get into the mod more quickly

## AUDIO CUES

### BEARING CUES "Position Audio":
```
Please note that position is determined relative to you cockpit. As a result, left and right will be inverted if you are upside-down.
The same is true to 'Low' and 'High'

  12 Oclock - Audio to be played to denote than an enemy is straight ahead.
  3 Oclock - Audio to be played to denote than an enemy is to your right.
  6 Oclock - Audio to be played to denote than an enemy is behind you.
  9 Oclock - Audio to be played to denote than an enemy is to your left.

  Low - Audio to be played when the enemy is significantly below your cockpit (determined by the Significant Angle Value)
  high - Audio to be played when the enemy is significantly above your cockpit (determined by the Significant Angle Value)
```
### HAZARD CUES
Each Hazard type is assigned a priority. This determines what band a hazard is in. A hazards priority will increase as it gets closer to you but will never be put into the band above. Priority is exponential meaning that a hazard right next to you will have a significantly higher priority than a hazard far away.

All hazards in the highest band will be called out in order of priority. This means that if multiple enemies are in the same priority band, the enemy closest to you will be called out first.

If multiple enemies are in different bands, only the enemies in the highest band will be called out (e.g. lets say an Aerosentry SPAAG is assigned band 4 and an ifrit is in band 7, the VWS will only call out the ifrit until the ifrit is out of range (and so nolonger considered by the VWS) or is destroyed. Only then will the SPAAG start being called out. If you wanted both the ifrit and the SPAAG to be called out, you would assign both threats the same priority in the configs.

Each hazard has a computed threat it may pose to you the player. I will leave attached a spreadsheet of every known unit so you can determine what the minimun threat threshold should be.
Any threats below the minimum threat threshold will be ignored and will not be called out by the VWS

#### GROUND HAZARDS
```
Manpads - Audio to be played to alert you to usually the existence of ground troops with AA rocket launchers (usually IRM S-1s.
SAM - Audio to be played to alert you to usually the existence of a vehicle which could fire an AA rocket at you.
SPAAG - Audio to be played to alert you to usually the existence of a vehicle which is armed with an AA gun only.
Low Priority Misc Ground - Audio to be played to alert you to usually the existence of any other ground unit that is in the lower 50% of the threat level that may be posed to you (Usually APCs and IFVs)
High Priority Misc Ground - Audio to be played to alert you to usually the existence of any other ground unit that is in the higher 50% of the threat level that may be posed to you (Naval units and Boltstrike Radar installations (As only the Boltstrike launchers are considered to be SAMs))

A unit will be considered to be high priority misc ground if it does not fulfill any of the other categories AND:
its AA threat > 0.5+(MinimunThreat)/2
```
#### AIR HAZARDS
```
Air - Audio to be played to alert you to usually the existence of enemy aircraft.
Missile - Audio to be played to alert you to usually the existence of an enemy missile

Missiles have two priorities. One for non-locked missiles and ones for missiles locked onto specifically you.
If you only want to be alerted to missiles that are locked onto you, check the "Only Call Out Locked" box

When a missile appears on the map, the VWS will attempt to determine if the missile is locked onto you or not. At times, it is able to determine if a missile is locked onto you before you receive the missile lock warning.

If you have 'Instruct Countermeasures' enabled, only locked missiles will have countermeasure instructions. As a result, you can determine if a missile is locked onto you by listening out for the counterplay audio
```
#### CONTROL HAZARDS "Instruction hazards"
These are hazards that are called out that are to do with the aircrafts status.
##### ALTITUDE
```
Altitude - Audio played when you are projected to hit the ground in less than 2*(Seconds to Collision)
Critical Altitude - Audio played when you are projected to hit the ground in less than (Seconds to Collision)
Dangerous Altitude - Audio played when your relative altitude is below (Dangerous Altitude)
Roll Left/Roll Right/Pull Up - Audio to played instructing you on how to evade hitting the ground
```
##### FUEL
```
Check Fuel - Audio to be played when aircraft fuel is below 30%
Low Fuel - Audio to be played when aircraft fuel is below 10%
BINGO Fuel - Audio to be played when you have just enough fuel to return to your nearest airfield.
```
BINGO Fuel is calculated using your average speed and fuel consumption. and assumes that on the return journey, you are using the same throttle value that you have been using for most of your flight until that point. It also assumes that you return straight to the airfield with no deviation in path at any point.
Under these conditions, you should be able to land with <5% fuel back at the airfield after turning around after a BINGO Fuel warning.

BINGO fuel assumes that your aircraft is in nominal condition. Loss of engines or fuel tanks may mean that you may return to the airfield with more or less than 5% fuel remaining.

In my testing, I have found BINGO fuel to be a good indicator of when to return to the airfield and I am yet to run out of fuel before returning to the airfield provided that i have travelled back to the airfield in the straight line with as few deviations as possible.

In a combat situation, this obviously is not possible and so I recommend running your aircraft on MIL after a BINGO fuel warning to ensure that you still have some fuel to evade missiles and so on.

##### COUNTERMEASURES AND COUNTERPLAY
```
IR:
Decrease throttle - Audio to be played instructing you to decrease your throttle as your engine is currently producing a large IR signature which can be easily tracked by IR missiles
Flare  -Audio to be played instructing you to flare

Radar:
Notch - Audio to be played instructing you to notch an incoming radar missile
Jammer - Audio to be played instructing you to jam an incoming radar missile

Misc:
Suffix Depleted - Audio to be played after either 'Notch' or 'Flare' to tell you that you're capacitor is either so low tat jamming will be ineffective or that you are running out of flare (<14 flares remain)
```

##### MISC
```
AoA - Audio to be played when your aircraft is approaching or is stalling
OverG - Audio to be played when your aircraft is exceeding a GForce limit you have set (Gforce Tolerance)
Eject - Audio to be played advising you to eject after your aircraft is critically damaged or the cockpit is burning up such that you (the pilot) are at risk of burning to death in the next few seconds.
It is up to you if you want to abide by this recommendation obviously.
```

## EXTERNAL PACK HANDLING (IMPORTED FOR LOCK SHOOT TONE PING)
### EXTERNAL PACK INSTALLATION (AND EDITING) GUIDE

All packs must contain:
- Audio Files
- A config file (named Configs.txt)

In order to install external packs, simply extract said pack into NuclearOption-VWS/Packs.

An example of the intended file structure once and external pack is installed is as follows:

```
  Plugins
    ¬NuclearOption-VWS
      ¬NuclearOptionVWS.Dll
      ¬Audio
      ¬Packs
        ¬ExamplePack
          ¬ExampleAudio.wav
          ¬Configs.txt
```

After this, you then want to open the game and change the 'Selected Pack' value from :DEFAULT: to the name of your installed pack. 

Please note that doing so will will then replace all of the settings that you have loaded with that of the installed external pack. That said, your settings at the time that you loaded the external pack will be saved in Audio/Configs.txt
These settings can be loaded by selecting the :DEFAULT: pack in the dropdown menu.

Please note, that once you have installed an external pack, you are free to edit its configs as if it were your default pack in exactly the same way that you would change the settings of the mod normally. Any changes to the configs are saved either when you switch packs (to a different pack) or when you close the game.

Please note that if you intend on sharing an external pack you have edited, if you have changed the audio, please ensure that the name of the in brackets next to the audio in your settings are the name of the external pack. 

Example (GOOD):
```
[ExamplePack] ExampleAudio
```
If you decide to use audio from a different pack or from your default audio folder, although it may work on your client, it may fail to work on other peoples as the audio itself in will not be saved in the external pack's folder

Example (BAD):
```
If I were editing "ExamplePack":
[AeriiCats Audio] Rita-Attack-Air

In this case, if I were to export ExamplePack, another user would not be able to load it correctly as the audio would be saved in "AeriiCats Audio" folder instead of in the "ExamplePack" folder
```

### EXTERNAL PACK CREATION GUIDE

#### CONFIG FORMATS:

There are 2 accepted Confg formats: Raw, Streamlined.

Here are an example of all of the config formats.

RAW:
```
[ConfigCategory].ConfigField=VALUE
```
Raw is lossless and all data can be retrieved from saving a config as Raw

Streamlined:
```
[ConfigCategory]
ConfigField=VALUE
```
Streamlined is lossless and all data can be retrieved from saving a config as Streamlined. It also also fairly human readable

Although the code for loading simplified format still exists in the mod, you cannot save in simplified format as it is exactly the same as streamlined.
As a result, simplified code saving has been removed.

#### PACK CREATION:

There are two methods of external pack creation:


1) You export an existing pack (your default settings works too)

Using this method, you can simply copy, paste and rename your default audio folder to the name of your pack and then compress it and share it. All configs that you had when your exported the audio folder will be carried over and re-created for anyone else installing that pack.

Alternately, you can also edit existing packs and then compress and share those. Please do however take note of the naming convention of audio files in the configuration manager. These are stated in the section above.
Another thing to note is that audio in the audio folder will not have a pack associated directly with it and so will not have the name of its pack of origin next to it in the bepinex configuration manager.

Example:
```
Any External Pack:
[ExamplePack] ExampleAudio

Any Audio in the Audio Folder:
ExampleAudio
```
Please note that if your pack uses audio from other packs, unless you also export those and force the end user to also install those packs as well, the sets which use said audio will be unable to be recreated and in its absence the sound that will be played will either be the sound which occupied that slot previously or :NONE: depending on the user's external pack settings.

Another thing to note:
The mod is unable to tell this difference between two files which have the same name but different file extensions. As the mod will consider both files to have the same name and will always play the sound which has the leading alphabetical letter in its file extension.

I.e. mp3 > ogg > wav

so an mp3 will always be played if you give the program an audio file with the name but on is an mp3 and the other is an ogg. and so on and so fourth for other audio files with the same name but different file extensions.


2) Do it manually

If you have a lot of external audio packs installed, you might find it easier to create the audio pack and its configs manually.

I have left templates for each of the acceptable config formats detailed above. Alternately, you can edit existing configs or if youre a masochist, you can write it all by hand.
When defining audio files in configs, refer to the file by name and do not write its file extension. Reason for this is stated above. Please note that the mod is case sensitive. Do not leave spaces unless there are spaces in the file name. 

If you are writing in Streamline, please do note that the order the lines are in matter too:

i.e.: The category a field is in will extend to the next time a category is defined (using []).
the same is true with subcategories although they are defined with a - at the start of the line.

Please note that the mod does not support comments in the config file. At best, the mod will ignore them. At worst, the mod will crash.

Again, I have example configs for you to download so you can edit those if you plan on making your external pack manually.

## Final Notes

Please note that this README is up to date with version 1.0.0
If I have forgotten to update the README for the current version, tell me. 

The development of this mod took longer than i expected (over a month in total) due to update 0.34 forcing me to check back over my code as well as the NOMM registry being locked down due to the QoL mod drama.

The first version of the mod was completed on 22/7/26 although i found that its structure was very difficult to work with a number of bugs present in the program were hard to patch and so i remade how the different hazards are handled.

I will continue to look over this mod to patch out the all of the bugs that will appear in the coming weeks but at present, I am happy with the state of the mod.
If you encounter any issues please feel free to contact me on discord either through the official nuclear option discord server or through Primerva2082. Or alternately, please feel free to raise an issue request here.

Finally, as this mod is open source - like all of my mods. Feel free to fork it and make your own version


Thats All,
AeriiCat 11/8/26
