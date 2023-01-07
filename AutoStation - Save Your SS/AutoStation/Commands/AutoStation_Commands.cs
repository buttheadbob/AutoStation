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
        public void ForceRun()
        {
            Context.Respond("Converting compatible grids to station mode.");
            Utils.Auto.AutoRun(null);
        }

        [Command("ForceAll_ADMIN", "Forces ALL large grids, small grids, grids in gravity, etc... to station mode (except when in a safezone with conversion disabled.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ForceAll_ADMIN()
        {
            Context.Respond("Converting ALL grids to station mode.");
            Utils.Auto.AutoRun(true);
        }
    }
}
