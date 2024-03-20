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
            Context.Respond("This plugin is used to place grids into station mode when a player has been offline for a preset amount of time.  Grids not in station mode can use many times the resources and processing power compared to stations.  For any questions, additions, or issues... please contact SentorX#4108 on discord or on the Torch Discord server.");
        }

        [Command("ForceRun", "Force the AutoStation to run.")]
        [Permission(MyPromoteLevel.Moderator)]
        public async void ForceRun()
        {
            Context.Respond("Converting compatible grids to station mode.");
            await Utils.Auto.AutoRun();
        }

        [Command("ForceAll_ADMIN", "Forces grids to convert.  ForceALL_ADMIN (bool)[smallGrids] (bool)[subGrids].  Use without the bools to convert small or sub grids will cause only large grids to be converted.")]
        [Permission(MyPromoteLevel.Admin)]
        public async void ForceAll_ADMIN(bool smallGrids=false, bool subGrids=false)
        {
            
            Context.Respond("Converting ALL grids to station mode.");
            await Utils.Auto.AutoRun(true, smallGrids, subGrids);
        }
    }
}
