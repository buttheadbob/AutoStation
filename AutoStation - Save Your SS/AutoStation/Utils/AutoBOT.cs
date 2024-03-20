using System;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Groups;
using VRageMath;

namespace AutoStation.Utils
{
    public static class Auto
    {
        private static Timer? AutoRun_Timer;
        private static TimerCallback AutoRun_Timer_CB = TimedAutoRun; 
        private static List<string> ConversionLog = new List<string>();
        private static bool isRunning = false;

        public static void Init() 
        {
            AutoRun_Timer = new Timer(AutoRun_Timer_CB, null, AutoStation_Main.Instance!.Config!.DelayStart, AutoStation_Main.Instance.Config.RunFrequency * 1000 * 60);

            if (!AutoStation_Main.Instance.Config.GridTrackingMode) return;
            isRunning = true;
            Tracking.Init();
            isRunning = false;
        }

        public static void Dispose()
        {
            AutoRun_Timer?.Dispose();
            Tracking.TrackedGrids.Clear();
        }
        
        private static async void TimedAutoRun(object stateInfo)
        {
            await AutoRun();
        }

        public static Task AutoRun(bool forcedByAdmin = false, bool smallGrids = false, bool subGrids = false)
        {
            try
            {
                if (isRunning)
                    return Task.CompletedTask;
            
                isRunning = true;
                Stopwatch msTracker = Stopwatch.StartNew();
                            
                if (!AutoStation_Main.Instance!.Config!.Enable)
                    return Task.CompletedTask;            

                if (MySession.Static.IsSaveInProgress)
                    return Task.CompletedTask;

                if (!MySession.Static.Ready || MySandboxGame.IsPaused)
                    return Task.CompletedTask;

                int LargeGridsConverted = 0;
                int GridsConverted = 0;
                int SubGridsConverted = 0;
                int SmallGrids = 0;

                HashSetReader<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups = MyCubeGridGroups.Static.Mechanical.Groups;
            
                Parallel.ForEach(groups, new ParallelOptions {MaxDegreeOfParallelism = MySandboxGame.NumberOfCores / 2}, (group) =>
                {
                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in group.Nodes)
                    {
                        MyCubeGrid? _grid = node.NodeData;
                        if (_grid is null) return;
                        
                        if (_grid.IsStatic)  // Already in station mode.
                            continue;

                        if (_grid.Physics == null || _grid.IsPreview || _grid.MarkedForClose)
                            continue;

                        // Forced convert by admin command, no gravity options need to be checked.
                        if (forcedByAdmin)
                        {
                            if (!smallGrids && _grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) continue;
                            if (!subGrids && node.ParentLinks.Count > 0) continue;

                            if (node.ParentLinks.Count > 0) 
                                Interlocked.Increment(ref SubGridsConverted);
                            
                            if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref SmallGrids);
                            else 
                                Interlocked.Increment(ref LargeGridsConverted);
                            
                            Interlocked.Increment(ref GridsConverted);
                            ConvertThis(_grid);
                        }

                        if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) // Converting small grids back to dynamic is not easy for players.
                        {
                            if (forcedByAdmin)
                            {
                                continue;
                            }
                        }

                        // What to do with grids that have no owners. (Not all blocks have ownership and floating blocks with no owners are still grids.)
                        switch (_grid.BigOwners.Count)
                        {
                            // No owners, no conversion.
                            case 0 when !AutoStation_Main.Instance.Config.StopGridsWithNoOwner:
                                continue;
                            // No owners, convert the grid.
                            case 0 when AutoStation_Main.Instance.Config.StopGridsWithNoOwner:
                                ConvertThis(_grid);
                                Interlocked.Increment(ref GridsConverted);
                                if (node.ParentLinks.Count > 0)
                                    Interlocked.Increment(ref SubGridsConverted);
                                continue;
                        }

                        // NPC owned grids, no conversion.
                        if (PlayerData.IsID_NPC(_grid.BigOwners.FirstOrDefault()))
                            continue;

                        ulong gridOwnerSteamID = MySession.Static.Players.TryGetSteamId(_grid.BigOwners.FirstOrDefault());
                        if (gridOwnerSteamID == 0)  // Since an update, sometimes IsNPC doesnt work, so we check for 0 steamID.
                            continue;
                        
                        if (Tracking.HasMoved(_grid.EntityId, _grid.PositionComp.GetPosition()))
                            continue;

                        bool InGrav = !Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(_grid.PositionComp.GetPosition()));
                        
                        if (!AutoStation_Main.Instance.Config.ConvertGridsInGravity && InGrav ) // Dont convert grids in gravity unless enabled.
                            continue;
                        
                        if (_grid.GetOwnerLogoutTimeSeconds() <= AutoStation_Main.Instance.Config.MinutesOffline * 60)                     
                            continue;

                        // This is a large grid subgrid in gravity, ignore subgrids in gravity option is disabled.
                        if (InGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInGravity && node.ParentLinks.Count > 0)
                        {
                            ConvertThis(_grid);
                            Interlocked.Increment(ref GridsConverted);
                            if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref SmallGrids);
                            else
                                Interlocked.Increment(ref LargeGridsConverted);

                            if (node.ParentLinks.Count > 0)
                                Interlocked.Increment(ref SubGridsConverted);
                        }

                        // Large grid in gravity, convert in gravity option is enabled.
                        if (InGrav && AutoStation_Main.Instance.Config.ConvertGridsInGravity && node.ParentLinks.Count == 0)
                        {
                            ConvertThis(_grid);
                            Interlocked.Increment(ref GridsConverted);
                            if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref SmallGrids);
                            else
                                Interlocked.Increment(ref LargeGridsConverted);

                            if (node.ParentLinks.Count > 0)
                                Interlocked.Increment(ref SubGridsConverted);
                        }
                        
                        // Large subgrid in space, ignore sub-grids option is not checked.
                        if (!InGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInSpace && node.ParentLinks.Count > 0)
                        {
                            ConvertThis(_grid);
                            Interlocked.Increment(ref GridsConverted);
                            if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref SmallGrids);
                            else
                                Interlocked.Increment(ref LargeGridsConverted);

                            if (node.ParentLinks.Count > 0)
                                Interlocked.Increment(ref SubGridsConverted);
                        }
                        
                        // Large grid in space.
                        if (!InGrav && node.ParentLinks.Count == 0)
                        {
                            ConvertThis(_grid);
                            Interlocked.Increment(ref GridsConverted);
                            if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref SmallGrids);
                            else
                                Interlocked.Increment(ref LargeGridsConverted);

                            if (node.ParentLinks.Count > 0)
                                Interlocked.Increment(ref SubGridsConverted);
                        }
                    }
                });
           
                msTracker.Stop();
                
                StringBuilder log = new StringBuilder().AppendLine();
                foreach (string _log in ConversionLog)
                {
                    log.AppendLine(_log);
                }

                log.AppendLine("———————————————————————————————————————————————————");
                log.AppendLine($"Attempted to convert {GridsConverted} dynamic grids to station mode during AutoConvert.");
                log.AppendLine($"Converted {SmallGrids} small grids.");
                log.AppendLine($"Converted {LargeGridsConverted} large grids.");
                log.AppendLine($"Converted {SubGridsConverted} sub-grids.");
                log.AppendLine($"AutoConvert took {msTracker.ElapsedMilliseconds}ms to complete.");
            
                AutoStation_Main.Log.Info(log);
                log.Clear();
                ConversionLog.Clear();
                
                isRunning = false;
                return Task.CompletedTask;
            } catch (Exception e)
            {
                AutoStation_Main.Log.Error(e);
                isRunning = false;
                return Task.CompletedTask;
            }
        }

        private static void ConvertThis(MyCubeGrid grid)
        {
            if (AutoStation_Main.Instance!.Config!.ShowConvertedGridsNameLog)
            {
                string? name = MySession.Static.Players.TryGetIdentity(grid.BigOwners.FirstOrDefault()).DisplayName;
                ConversionLog.Add(AutoStation_Main.Instance.Config.ShowConvertedGridsOwnerNameLog
                    ? $"Converting {grid.DisplayName} owned by {name}"
                    : $"Converting {grid.DisplayName}");}
            
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
            return MySession.Static.Players.IdentityIsNpc(id);
        }
    }

    public static class Tracking
    {
        public static ConcurrentDictionary<long, Vector3D> TrackedGrids = new ConcurrentDictionary<long, Vector3D>();
        
        public static void Init()
        {
            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, new ParallelOptions { MaxDegreeOfParallelism = MySandboxGame.NumberOfCores / 2 }, group =>
            {
                if (group.Nodes.Count == 0)
                    return;

                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node? GroupNode in group.Nodes)
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
            if (!AutoStation_Main.Instance!.Config!.GridTrackingMode)
                return false;

            if (TrackedGrids.TryGetValue(id, out Vector3D SavedLocation))
            {
                if (Vector3D.Distance(SavedLocation, Location) < AutoStation_Main.Instance.Config.MinDistanceToBeConsideredInUse)
                    return false;

                TrackedGrids.TryUpdate(id, Location, SavedLocation);
                return true;
            }

            TrackedGrids.TryAdd(id, Location);
            return true;
        }
    }
}
