using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStation_SaveYourSS.Utils
{
    public static class Auto
    {
        private static Timer AutoRun_Timer;
        private static TimerCallback AutoRun_Timer_CB = new TimerCallback(AutoRun);
        
        public static readonly Logger Log = LogManager.GetLogger("AutoStation");

        public static void Init() 
        {
            AutoRun_Timer = new Timer(AutoRun_Timer_CB, EventLog.EvenLogged(), AutoStation.Instance.Config.DelayStart, AutoStation.Instance.Config.RunFrequency);
        }

        public static void Dispose()
        {
            AutoRun_Timer.Dispose();
        }

        private static void AutoRun(object state)
        {
            if (!AutoStation.Instance.Config.Enable)
                return;

            if (MySession.Static.IsSaveInProgress)
                return;

            if (!MySession.Static.Ready || MySandboxGame.IsPaused)
                return;

            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group =>
            {
                if (group.Nodes.Count == 0)
                    return;

                foreach (var GroupNode in group.Nodes)
                {
                    if (GroupNode.NodeData.Physics == null || GroupNode.NodeData.IsPreview || GroupNode.NodeData.MarkedForClose)
                        continue;

                    // We only need one grid from this group, the gridstamp will find the biggest.
                    if (GroupNode.NodeData.GetOwnerLogoutTimeSeconds() > AutoStation.Instance.Config.MinutesOffline * 60000)
                    {
                        if (GroupNode.NodeData.Physics.Gravity == VRageMath.Vector3.Zero && !AutoStation.Instance.Config.ConvertGridsInGravity) // Dont convert grids in gravity unless enabled.
                            continue;

                        GroupNode.NodeData.Physics.Clear(); // Stop any drifting
                        GroupNode.NodeData.Physics.ClearSpeed(); // meh
                        
                        
                        if (MySandboxGame.ConfigDedicated.SessionSettings.StationVoxelSupport) // Unsupported Stations Mode requires a different call to convert to station, or risk crashing.
                        {
                            GroupNode.NodeData.ConvertToStatic();
                        }
                        else
                        {
                            GroupNode.NodeData.Physics.ConvertToStatic();
                        }
                    }
                }
            });

        }
    }

    public static class EventLog
    {
        public static readonly Logger AutoRunLogger = LogManager.GetLogger("AutoStation - AutoRun");
        public static bool EvenLogged()
        {
            AutoRunLogger.Info("Running AutoStation");
            return true;
        }
    }
}
