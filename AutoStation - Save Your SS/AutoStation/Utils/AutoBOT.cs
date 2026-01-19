using System;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private static Timer? _autoRunTimer;
        private static readonly TimerCallback AutoRunTimerCb = TimedAutoRun; 
        private static readonly List<string> ConversionLog = new List<string>();
        private static bool _isRunning;

        public static void UpdateTimer(int newFrequency)
        {
            _autoRunTimer?.Change(TimeSpan.FromMinutes(newFrequency), TimeSpan.FromMinutes(newFrequency));
        }

        public static void Init() 
        {
            _autoRunTimer = new Timer(AutoRunTimerCb, null, TimeSpan.FromMinutes(AutoStation_Main.Instance!.Config!.DelayStart),  TimeSpan.FromMinutes(AutoStation_Main.Instance.Config.RunFrequency));

            if (!AutoStation_Main.Instance.Config.GridTrackingMode) return;
            _isRunning = true;
            Tracking.Init();
            _isRunning = false;
        }

        public static void Dispose()
        {
            _autoRunTimer?.Dispose();
            Tracking.TrackedGrids.Clear();
        }
        
        private static async void TimedAutoRun(object stateInfo)
        {
            try
            {
                await AutoRun();
                _autoRunTimer?.Change(TimeSpan.FromMinutes(AutoStation_Main.Instance!.Config!.RunFrequency), TimeSpan.FromMinutes(AutoStation_Main.Instance!.Config!.RunFrequency));
            }
            catch (Exception e)
            {
                AutoStation_Main.Log.Error(e);
            }
        }

        public static Task AutoRun(bool forcedByAdmin = false, bool smallGrids = false, bool subGrids = false)
        {
            try
            {
                if (_isRunning)
                {
                    AutoStation_Main.Log.Info("AutoStation already processing grids.");
                    return Task.CompletedTask;
                }

                _isRunning = true;
                Stopwatch msTracker = Stopwatch.StartNew();

                if (!AutoStation_Main.Instance!.Config!.Enable || MySession.Static.IsSaveInProgress || !MySession.Static.Ready || MySandboxGame.IsPaused)
                    return Task.CompletedTask;

                int largeGridsConverted = 0;
                int gridsConverted = 0;
                int subGridsConverted = 0;
                int smallGridsConverted = 0;
                int npcGrids = 0;

                HashSetReader<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups = MyCubeGridGroups.Static.Mechanical.Groups;
                
                // Get online player locations
                int minDistanceSquared = AutoStation_Main.Instance.Config.MinDistanceFromPlayers * AutoStation_Main.Instance.Config.MinDistanceFromPlayers;
                List<Vector3D> onlinePlayerLocations = new List<Vector3D>();
                foreach (MyPlayer? onlinePlayer in MySession.Static.Players.GetOnlinePlayers())
                    onlinePlayerLocations.Add(onlinePlayer.GetPosition());
                
                Parallel.ForEach(groups, new ParallelOptions { MaxDegreeOfParallelism = MySandboxGame.NumberOfCores / 3 }, (group) =>
                {
                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in group.Nodes)
                    {
                        MyCubeGrid? grid = node.NodeData;
                        if (grid is null)
                            continue;

                        if (grid.IsStatic) // Already in station mode.
                            continue;

                        if (grid.Physics == null || grid.IsPreview || grid.MarkedForClose)
                            continue;

                        // Forced convert by admin command, no gravity options need to be checked.
                        if (forcedByAdmin)
                        {
                            if (PlayerData.IsID_NPC(grid.BigOwners.FirstOrDefault()))
                            {
                                Interlocked.Increment(ref npcGrids);
                                continue;
                            }
                            
                            if (ConvertThis(grid))
                            {
                                if (!smallGrids && grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) continue;
                                if (!subGrids && node.ParentLinks.Count > 0) continue;

                                if (node.ParentLinks.Count > 0)
                                    Interlocked.Increment(ref subGridsConverted);

                                if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                    Interlocked.Increment(ref smallGridsConverted);
                                else
                                    Interlocked.Increment(ref largeGridsConverted);

                                Interlocked.Increment(ref gridsConverted);
                            }

                            continue;
                        }

                        if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) // Converting small grids back to dynamic is not easy for players.
                            if (forcedByAdmin && !smallGrids) continue;

                        // Check if the grid is near players and skip if needed
                        if (AutoStation_Main.Instance.Config.MinDistanceFromPlayers > 0)
                        {
                            bool skipNeeded = false;
                            foreach (Vector3D position in onlinePlayerLocations)
                            {
                                if (Vector3D.DistanceSquared(position, grid.PositionComp.GetPosition()) <= minDistanceSquared)
                                    skipNeeded = true;
                            }
                            if (skipNeeded) continue;
                        }
                        
                        // What to do with grids that have no owners. (Not all blocks have ownership, and floating blocks with no owners are still grids.)
                        switch (grid.BigOwners.Count)
                        {
                            // No owners, no conversion.
                            case 0 when !AutoStation_Main.Instance.Config.StopGridsWithNoOwner:
                                continue;
                            
                            // No owners, convert the grid.
                            case 0 when AutoStation_Main.Instance.Config.StopGridsWithNoOwner:
                                if (node.ParentLinks.Count > 0 && AutoStation_Main.Instance.Config.IgnoreSubGridsInSpace)
                                    continue;
                                
                                if (ConvertThis(grid))
                                {
                                    Interlocked.Increment(ref gridsConverted);
                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref subGridsConverted);
                                }
                                
                                continue;
                        }

                        // NPC owned grids, no conversion.
                        if (PlayerData.IsID_NPC(grid.BigOwners.FirstOrDefault()))
                            continue;

                        ulong gridOwnerSteamId = MySession.Static.Players.TryGetSteamId(grid.BigOwners.FirstOrDefault());
                        if (gridOwnerSteamId == 0) // Since an update, sometimes IsNPC doesnt work, so we check for 0 steamID.
                            continue;

                        if (Tracking.HasMoved(grid.EntityId, grid.PositionComp.GetPosition()))
                            continue;

                        bool inGrav = !Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(grid.PositionComp.GetPosition()));

                        if (!AutoStation_Main.Instance.Config.ConvertGridsInGravity && inGrav) // Don't convert grids in gravity unless enabled.
                            continue;

                        if (grid.GetOwnerLogoutTimeSeconds() <= AutoStation_Main.Instance.Config.MinutesOffline * 60)
                            continue;

                        // This is a large grid subgrid in gravity, ignore the sub-grids in gravity option is disabled.
                        if (inGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInGravity && node.ParentLinks.Count > 0)
                        {
                            if (ConvertThis(grid))
                            {
                                Interlocked.Increment(ref gridsConverted);
                                if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                    Interlocked.Increment(ref smallGridsConverted);
                                else
                                    Interlocked.Increment(ref largeGridsConverted);

                                if (node.ParentLinks.Count > 0)
                                    Interlocked.Increment(ref subGridsConverted);
                            }
                            
                            continue;
                        }

                        // Large grid in gravity, convert in gravity option is enabled.
                        if (inGrav && AutoStation_Main.Instance.Config.ConvertGridsInGravity && node.ParentLinks.Count == 0)
                        {
                            if (ConvertThis(grid))
                            {
                                Interlocked.Increment(ref gridsConverted);
                                if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                    Interlocked.Increment(ref smallGridsConverted);
                                else
                                    Interlocked.Increment(ref largeGridsConverted);

                                if (node.ParentLinks.Count > 0)
                                    Interlocked.Increment(ref subGridsConverted);
                            }
                            
                            continue;
                        }

                        // Large subgrid in space, ignore sub-grids option is not checked.
                        if (!inGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInSpace && node.ParentLinks.Count > 0)
                        {
                            if (ConvertThis(grid))
                            {
                                Interlocked.Increment(ref gridsConverted);
                                if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                    Interlocked.Increment(ref smallGridsConverted);
                                else
                                    Interlocked.Increment(ref largeGridsConverted);

                                if (node.ParentLinks.Count > 0)
                                    Interlocked.Increment(ref subGridsConverted);
                            }

                            continue;
                        }

                        // Large grid in space.
                        if (!inGrav && node.ParentLinks.Count == 0)
                        {
                            if (ConvertThis(grid))
                            {
                                Interlocked.Increment(ref gridsConverted);
                                if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                    Interlocked.Increment(ref smallGridsConverted);
                                else
                                    Interlocked.Increment(ref largeGridsConverted);

                                if (node.ParentLinks.Count > 0)
                                    Interlocked.Increment(ref subGridsConverted);
                            }
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
                log.AppendLine($"Attempted to convert {gridsConverted} dynamic grids to station mode during AutoConvert.");
                log.AppendLine($"Converted {smallGridsConverted} small grids.");
                log.AppendLine($"Converted {largeGridsConverted} large grids.");
                log.AppendLine($"Converted {subGridsConverted} sub-grids.");
                log.AppendLine($"Ignored {npcGrids} NPC owned grids/subgrids.");
                log.AppendLine($"AutoConvert took {msTracker.Elapsed.TotalMilliseconds:N4}ms to complete.");

                AutoStation_Main.Log.Info(log);
                log.Clear();
                ConversionLog.Clear();
                
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                AutoStation_Main.Log.Error(e);
                return Task.CompletedTask;
            }
            finally
            {
                _isRunning = false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ConvertThis(MyCubeGrid? grid)
        {
            if (grid is null) return false;

            if (AutoStation_Main.Instance!.Config!.ShowConvertedGridsNameLog)
            {
                if (grid.BigOwners.Count > 0)
                {
                    MyIdentity? identity = MySession.Static.Players.TryGetIdentity(grid.BigOwners.FirstOrDefault());
                    
                    ConversionLog.Add(AutoStation_Main.Instance.Config.ShowConvertedGridsOwnerNameLog
                        ? $"Converting [{grid.DisplayName}] owned by {identity.DisplayName}({MySession.Static.Players.TryGetSteamId(identity.IdentityId)})"
                        : $"Converting [{grid.DisplayName}]");
                }
                else
                {
                    ConversionLog.Add($"Converting grid with no owners [{grid.DisplayName}]");
                }
            }

            MySandboxGame.Static.Invoke(() =>
            {
                grid.Physics.Clear(); // Stop any drifting
                grid.Physics.ClearSpeed(); // meh
                            
                grid.RequestConversionToStation();
            }, "AutoStation");

            return true;
        }
    }

    public static class PlayerData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsID_NPC(long id)
        {
            // MySession.Static.Players.IdentityIsNpc(id) does not always return true if the owner is an NPC.... so we check for 0 steamID.
            return MySession.Static.Players.TryGetSteamId(id) == 0;
        }
    }

    public static class Tracking
    {
        public static readonly ConcurrentDictionary<long, Vector3D> TrackedGrids = new ConcurrentDictionary<long, Vector3D>();
        
        public static void Init()
        {
            try
            {
                Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, new ParallelOptions { MaxDegreeOfParallelism = MySandboxGame.NumberOfCores / 4 }, group =>
                {
                    if (group.Nodes.Count == 0)
                        return;

                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node? groupNode in group.Nodes)
                    {
                        if (groupNode.NodeData.BigOwners == null) // Not all blocks have ownership and floating blocks with no owners are still grids.
                            continue;

                        if (PlayerData.IsID_NPC(groupNode.NodeData.BigOwners.FirstOrDefault()))
                            continue;

                        TrackedGrids.TryAdd(groupNode.NodeData.EntityId, groupNode.NodeData.PositionComp.GetPosition());
                    }
                });
            } catch (Exception e)
            {
                AutoStation_Main.Log.Error(e);
            }
        }

        public static bool HasMoved(long id, Vector3D location)
        {
            if (!AutoStation_Main.Instance!.Config!.GridTrackingMode)
                return false;

            if (TrackedGrids.TryGetValue(id, out Vector3D savedLocation))
            {
                if (Vector3D.Distance(savedLocation, location) < AutoStation_Main.Instance.Config.MinDistanceToBeConsideredInUse)
                    return false;

                TrackedGrids.TryUpdate(id, location, savedLocation);
                return true;
            }

            TrackedGrids.TryAdd(id, location);
            return true;
        }
    }
}
