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
using VRage.Collections;
using VRage.Groups;
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
            
            HashSetReader<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups = MyCubeGridGroups.Static.Mechanical.Groups;
            
            Parallel.ForEach(groups, new ParallelOptions() {MaxDegreeOfParallelism = MySandboxGame.NumberOfCores / 2}, (group) =>
            {
                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in group.Nodes)
                {
                    MyCubeGrid _grid = node.NodeData;
                    
                    if (_grid.IsStatic)  // Already in station mode.
                        continue;

                    if (_grid.Physics == null || _grid.IsPreview || _grid.MarkedForClose)
                        continue;

                    Interlocked.Increment(ref GridsCounted);

                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) // Converting small grids back to dynamic is not easy for players.
                    {
                        Interlocked.Increment(ref SmallGrids);
                        if (state != null && !(bool)state)
                        {
                            continue;
                        }
                    }

                    if (_grid.BigOwners == null) // Not all blocks have ownership and floating blocks with no owners are still grids.
                        continue;

                    if (PlayerData.IsID_NPC(_grid.BigOwners.FirstOrDefault()))
                        continue;
                    
                    if (Tracking.HasMoved(_grid.EntityId, _grid.PositionComp.GetPosition()))
                        continue;
                    
                    // Forced convert by admin command, no gravity options need to be checked.
                    if (state != null && (bool) state)
                    {
                        Interlocked.Increment(ref GridsConverted);
                        ConvertThis(_grid);
                    }

                    bool InGrav = !Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(_grid.PositionComp.GetPosition()));
                    
                    if (!AutoStation_Main.Instance.Config.ConvertGridsInGravity && InGrav ) // Dont convert grids in gravity unless enabled.
                        continue;
                    
                    if (_grid.GetOwnerLogoutTimeSeconds() <= AutoStation_Main.Instance.Config.MinutesOffline * 60)                     
                        continue;

                    // This is a large grid subgrid in gravity, ignore subgrids in gravity option is disabled.
                    if (InGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInGravity && _grid.Parent != null)
                        ConvertThis(_grid);

                    // Large grid in gravity, convert in gravity option is enabled.
                    if (InGrav && AutoStation_Main.Instance.Config.ConvertGridsInGravity && _grid.Parent == null)
                        ConvertThis(_grid);
                    
                    // Large grid in space, convert subgrids option is not enabled
                    if (!InGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInSpace && _grid.Parent != null)
                        ConvertThis(_grid);

                }
            });
           
            msTracker.Stop();
            AutoStation_Main.Log.Info($"{GridsConverted} of {GridsCounted} dynamic grids converted to station mode during AutoConvert.  Found {SmallGrids} small grids.  Took {msTracker.Elapsed.TotalMilliseconds} ms.");
        }

        private static void ConvertThis(MyCubeGrid grid)
        {
            MySandboxGame.Static.Invoke(() =>
            {
                grid.Physics.Clear(); // Stop any drifting
                grid.Physics.ClearSpeed(); // meh
                            
                grid.RequestConversionToStation();
            }, "AutoStation");
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
