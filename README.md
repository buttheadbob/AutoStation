<p align="center">
  <a href="https://discord.gg/rSuxGrHrrt">
    <img src="https://img.shields.io/badge/VERSION-1.1.0.w2t-red" alt="Discord Server">
  </a>
  <a href="https://discord.gg/rSuxGrHrrt">
    <img src="https://discordapp.com/api/guilds/1089078620829536269/widget.png?style=shield" alt="Discord Server">
  </a>
  </p> 

# AutoStation

## Physics calculations on dynamic grids cost the server many times more than there station counterparts.  
Use this plugin convert inactive grids to station mode and help preserver simspeed.  

### Options
```Enable``` The plugin will not convert any grids when disabled.  
  
```Delay Start in Minutes``` How long between the server starting and the AutoConvert starting.  
  
```Frequency in Minutes``` How often to check and convert grids to station mode.  
  
```Minutes Offline``` How long a player needs to be offline before his grids are able to be converted.  
** A grid is never converted while a player is flying it, or any grid joined to it by a rotor, piston or wheel.  This does not include grids docked by a connector or landing gear.  Only ```ForceAll_ADMIN``` ignores this.  
  
```Convert Grids In Gravity``` This will convert grids in gravity when enabled.   
** This could cause problems for players whos grids are converted in voxels.  
  
```Track Grids``` When enabled, allows players to use team member grids without fear it will  
convert to station mode as long as the grid is not in the same place across 2 conversion runs.  
The first run after a start or restart only records where grids are, so the second is the first that can convert them.  
```Minimum Distance``` Used in consideration if the grid should be considered not moving, even when it technically is.  This helps with grids floating around stations and other similar scenarios.  

** The cost to track grids is extremely minimal.  

### Admin Commands  
```!AS About``` Gives basic information about this plugin.  
  
```!AS ForceRun``` Forces the AutoConvert to check and convert grids.  
  
```!AS ForceAll_ADMIN [bool ForceSmallGrids] [bool ForceSubGrids]``` Use without the bools to convert small or sub grids will cause only large grids to be converted. 

### Torch and Magnetar  
AutoStation runs on both, and both builds share the same code in `Shared`.  
```TorchPlugin``` The Torch plugin, configured on its tab in Torch.  
  
```ServerPlugin``` The Magnetar plugin, configured in Quasar under Server plugins.  Its options and commands match Torch.  
  
The two keep separate config files, so settings do not carry over from one to the other.  

### Building  
Run `Setup (run before opening solution).bat` once after cloning, then `build.bat`.  
```build.bat``` Builds both for release.  Add `torch` or `magnetar` to build one, for example `build.bat magnetar`.  
  
The Torch build needs Visual Studio and creates `Build\AutoStation.zip`.  The Magnetar build needs the .NET SDK and a Magnetar install, or the path to `PluginSdk.dll` set in `Directory.Build.props`.  
  
Each `build.bat` run raises the last part of the version in `Versioning\AssemblyVersion.cs` and copies it into `TorchPlugin\manifest.xml`.  `AutoStation.xml` is the MagnetarHub manifest.  

### License  
Public domain ([The Unlicense](LICENSE.txt)): anyone may copy, modify, use and redistribute it for any purpose, no conditions.  

For more information or help, join my discord server --> https://discord.gg/rSuxGrHrrt
