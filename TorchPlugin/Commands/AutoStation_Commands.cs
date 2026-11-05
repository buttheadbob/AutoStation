using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace AutoStation.Commands
{
    [Category("AS")]
    public class AutoStation_Commands : CommandModule
    {
        public AutoStation_Main Plugin => (AutoStation_Main)Context.Plugin;

        [Command("About", "Get information about the AutoStation Plugin.")]
        [Permission(MyPromoteLevel.None)]
        public void About()
        {
            Context.Respond(AutoStationCore.AboutText);
        }

        [Command("ForceRun", "Force the AutoStation to run.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ForceRun()
        {
            Context.Respond("Converting compatible grids to station mode.");
            Utils.Auto.RequestRun();
        }

        [Command("ForceAll_ADMIN", "Forces grids to convert.  ForceALL_ADMIN (bool)[smallGrids] (bool)[subGrids].  Use without the bools to convert small or sub grids will cause only large grids to be converted.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ForceAll_ADMIN(bool smallGrids=false, bool subGrids=false)
        {
            Context.Respond("Converting ALL grids to station mode.");
            Utils.Auto.RequestRun(true, smallGrids, subGrids);
        }
    }
}
