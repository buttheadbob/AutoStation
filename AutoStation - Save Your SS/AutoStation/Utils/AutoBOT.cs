using System;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VRage.Network;
using VRageMath;

namespace AutoStation.Utils
{
    public static class Auto
    {
        private static Timer AutoRun_Timer;
        private static TimerCallback AutoRun_Timer_CB = new TimerCallback(AutoRun); 
        public static HashSet<long> NPCs = new HashSet<long>();

        public static void Init() 
        {
            AutoRun_Timer = new Timer(AutoRun_Timer_CB, null, AutoStation_Main.Instance.Config.DelayStart, AutoStation_Main.Instance.Config.RunFrequency * 1000 * 60);

            if (AutoStation_Main.Instance.Config.GridTrackingMode)
            {
                Tracking.Init();
            }
        }

        public static void Dispose()
        {
            AutoRun_Timer.Dispose();
            Tracking.TrackedGrids.Clear();
            NPCs.Clear();
        }

        public static void AutoRun(object state)
        {
            Stopwatch msTracker = Stopwatch.StartNew();
            NPCs = MySession.Static.Players.GetNPCIdentities();
                        
            if (!AutoStation_Main.Instance.Config.Enable)
                return;            

            if (MySession.Static.IsSaveInProgress)
                return;

            if (!MySession.Static.Ready || MySandboxGame.IsPaused)
                return;

            int GridsCounted = 0;
            int GridsConverted = 0;
            int SmallGrids = 0;

            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, new ParallelOptions() { MaxDegreeOfParallelism = MySandboxGame.NumberOfCores / 2 }, group =>
            {
                if (group.Nodes.Count == 0)
                    return;

                foreach (var GroupNode in group.Nodes)
                {
                    MyCubeGrid Griddy = GroupNode.NodeData;
                                        
                    if (Griddy.IsStatic)  // Already in station mode.
                        continue;

                    if (Griddy.Physics == null || Griddy.IsPreview || Griddy.MarkedForClose)
                        continue;

                    Interlocked.Increment(ref GridsCounted);

                    
                    if (Griddy.GridSizeEnum == VRage.Game.MyCubeSize.Small) // Converting small grids back to dynamic is not easy for players.
                    {
                        Interlocked.Increment(ref SmallGrids);
                        if (state != null && !(bool)state)
                        {
                            continue;
                        }
                    }
                    
                    
                    if (Griddy.BigOwners == null) // Not all blocks have ownership and floating blocks with no owners are still grids.
                        continue;

                    if (PlayerData.IsID_NPC(Griddy.BigOwners.FirstOrDefault()))
                        continue;
                    
                    if (Tracking.HasMoved(Griddy.EntityId, Griddy.PositionComp.GetPosition()))
                        continue;
                    
                    if (Griddy.GetOwnerLogoutTimeSeconds() > AutoStation_Main.Instance.Config.MinutesOffline * 60)                     
                    {                        
                        if (state != null && !(bool)state)
                            if (!Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(Griddy.PositionComp.GetPosition())) && !AutoStation_Main.Instance.Config.ConvertGridsInGravity) // Dont convert grids in gravity unless enabled.
                                continue;

                        Interlocked.Increment(ref GridsConverted);
                        MySandboxGame.Static.Invoke(() =>
                        {
                            
                            Griddy.Physics.Clear(); // Stop any drifting
                            Griddy.Physics.ClearSpeed(); // meh
                            
                            Griddy.RequestConversionToStation();
                            
                        }, "AutoStation");                        
                    }
                }
            });

            msTracker.Stop();
            AutoStation_Main.Log.Info($"{GridsConverted} of {GridsCounted} dynamic grids converted to station mode during AutoConvert.  Found {SmallGrids} small grids.  Took {msTracker.Elapsed.TotalMilliseconds} ms.");
        }
    }    

    public static class PlayerData
    {
        public static bool IsID_NPC(long id)
        {
            if (Auto.NPCs.Contains(id))
                return true;
            else
                return false;            
        }
    }

    public static class Tracking
    {
        public static ConcurrentDictionary<long, Vector3D> TrackedGrids = new ConcurrentDictionary<long, Vector3D>();
        public static void Init()
        {            
            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, new ParallelOptions() { MaxDegreeOfParallelism = MySandboxGame.NumberOfCores / 2 }, group =>
            {
                Dictionary<long, Vector3D> Grids = new Dictionary<long, Vector3D>();

                if (group.Nodes.Count == 0)
                    return;

                foreach (var GroupNode in group.Nodes)
                {
                   
                    if (GroupNode.NodeData.BigOwners == null) // Not all blocks have ownership and floating blocks with no owners are still grids.
                        continue;

                    if (PlayerData.IsID_NPC(GroupNode.NodeData.BigOwners.FirstOrDefault()))
                        continue;

                    TrackedGrids.TryAdd(GroupNode.NodeData.EntityId, GroupNode.NodeData.PositionComp.GetPosition());
                }
            });
        }

        public static bool HasMoved(long id, Vector3D Location)
        {
            if (!AutoStation_Main.Instance.Config.GridTrackingMode)
                return false;

            if (TrackedGrids.TryGetValue(id, out Vector3D SavedLocation))
            {
                if (SavedLocation == Location)
                    return false;
                
                
                TrackedGrids.TryUpdate(id, Location, SavedLocation);
                return true;
                
            }

            TrackedGrids.TryAdd(id, Location);
            return true;
        }
    }

    
}
