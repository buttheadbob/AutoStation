using AutoStation.Utils;
using PluginSdk.Commands;
using VRage.Game.ModAPI;

namespace AutoStation.MagnetarHost;

/// <summary>Magnetar wrapper with the same commands and levels as the Torch plugin: !AS About, !AS ForceRun, !AS ForceAll_ADMIN.</summary>
[CommandRoot("AS", "AutoStation", "convert inactive grids to station mode")]
public sealed class AutoStationCommands : CommandModule
{
    [Command("About", "Get information about the AutoStation Plugin.")]
    [Permission(MyPromoteLevel.None)]
    public string About() => AutoStationCore.AboutText;

    [Command("ForceRun", "Force the AutoStation to run.")]
    [Permission(MyPromoteLevel.Moderator)]
    public void ForceRun()
    {
        Context.Respond("Converting compatible grids to station mode.");
        Auto.RequestRun();
    }

    [Command("ForceAll_ADMIN", "Forces grids to convert.  ForceALL_ADMIN (bool)[smallGrids] (bool)[subGrids].  Use without the bools to convert small or sub grids will cause only large grids to be converted.")]
    [Permission(MyPromoteLevel.Admin)]
    public void ForceAll_ADMIN(bool smallGrids = false, bool subGrids = false)
    {
        Context.Respond("Converting ALL grids to station mode.");
        Auto.RequestRun(true, smallGrids, subGrids);
    }
}
