using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace AutoStation_SaveYourSS.Commands
{
    [Category("AS")]
    public class AutoStation_Commands : CommandModule
    {
        public AutoStation Plugin => (AutoStation)Context.Plugin;

        [Command("About", "Get information about the AutoStation Plugin.")]
        [Permission(MyPromoteLevel.None)]
        public void About()
        {
            Context.Respond("This plugin is used to place grids into station mode when a player has been offline for a preset amount of time.  Grids not in station mode can use many times the resources and processing power compared to stations.  For any questions, additions, or issues... please contact SenatorX#4108 on discord or on the Torch Discord server.");
        }

        [Command("Stats", "Will give a list of how many grids are in station mode, how many in dynamic mode, and how many are large/small grid.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Stats() 
        {
            Context.Respond("Not Finished");
        }

        [Command("ForceAll", "Force all large grids who meet the requirements to station mode.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ForceAll()
        {
            Context.Respond("Not Finished");
        }

        [Command("ForceAll_ADMIN", "Force all large grids regardless if they meet the requirements to station mode.  Be careful using this, grids on planets will not be placed into station mode as well.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ForceAll_ADMIN()
        {
            Context.Respond("Not Finished");
        }

        [Command("ForceAll_ADMIN_PLANET", "The exact same as ForceAll_ADMIN except large grids on planets will be placed into station mode as well.  ")]
        [Permission(MyPromoteLevel.Admin)]
        public void ForceAll_ADMIN_PLANET()
        {
            Context.Respond("Not Finished");
        }
    }
}
