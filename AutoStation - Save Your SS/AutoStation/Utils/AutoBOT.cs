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
        private static Timer? AutoRun_Timer;
        private static readonly TimerCallback AutoRun_Timer_CB = TimedAutoRun; 
        private static readonly List<string> ConversionLog = new List<string>();
        private static bool isRunning = false;

        public static void UpdateTimer(int newFrequency)
        {
            AutoRun_Timer?.Change(TimeSpan.FromMinutes(newFrequency), TimeSpan.FromMinutes(newFrequency));
        }

        public static void Init() 
        {
            AutoRun_Timer = new Timer(AutoRun_Timer_CB, null, TimeSpan.FromMinutes(AutoStation_Main.Instance!.Config!.DelayStart),  TimeSpan.FromMinutes(AutoStation_Main.Instance.Config.RunFrequency));

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
            AutoRun_Timer?.Change(TimeSpan.FromMinutes(AutoStation_Main.Instance!.Config!.RunFrequency), TimeSpan.FromMinutes(AutoStation_Main.Instance!.Config!.RunFrequency));
        }

        public static Task AutoRun(bool forcedByAdmin = false, bool smallGrids = false, bool subGrids = false)
        {
            try
            {
                if (isRunning)
                {
                    AutoStation_Main.Log.Info("AutoStation already processing grids.");
                    return Task.CompletedTask;
                }

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
                int NPCGrids = 0;

                HashSetReader<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups = MyCubeGridGroups.Static.Mechanical.Groups;

                if (groups.Count > 400)
                {
                    Parallel.ForEach(groups, new ParallelOptions { MaxDegreeOfParallelism = MySandboxGame.NumberOfCores / 3 }, (group) =>
                    {
                        foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in group.Nodes)
                        {
                            MyCubeGrid? _grid = node.NodeData;
                            if (_grid is null)
                                continue;

                            if (_grid.IsStatic) // Already in station mode.
                                continue;

                            if (_grid.Physics == null || _grid.IsPreview || _grid.MarkedForClose)
                                continue;

                            // Forced convert by admin command, no gravity options need to be checked.
                            if (forcedByAdmin)
                            {
                                if (PlayerData.IsID_NPC(_grid.BigOwners.FirstOrDefault()))
                                {
                                    Interlocked.Increment(ref NPCGrids);
                                    continue;
                                }
                                
                                if (ConvertThis(_grid))
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
                                }

                                continue;
                            }

                            if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) // Converting small grids back to dynamic is not easy for players.
                            {
                                if (forcedByAdmin && smallGrids == false) continue;
                            }

                            // What to do with grids that have no owners. (Not all blocks have ownership and floating blocks with no owners are still grids.)
                            switch (_grid.BigOwners.Count)
                            {
                                // No owners, no conversion.
                                case 0 when !AutoStation_Main.Instance.Config.StopGridsWithNoOwner:
                                    continue;
                                // No owners, convert the grid.
                                case 0 when AutoStation_Main.Instance.Config.StopGridsWithNoOwner:
                                    if (ConvertThis(_grid))
                                    {
                                        Interlocked.Increment(ref GridsConverted);
                                        if (node.ParentLinks.Count > 0)
                                            Interlocked.Increment(ref SubGridsConverted);
                                    }
                                    
                                    continue;
                            }

                            // NPC owned grids, no conversion.
                            if (PlayerData.IsID_NPC(_grid.BigOwners.FirstOrDefault()))
                                continue;

                            ulong gridOwnerSteamID = MySession.Static.Players.TryGetSteamId(_grid.BigOwners.FirstOrDefault());
                            if (gridOwnerSteamID == 0) // Since an update, sometimes IsNPC doesnt work, so we check for 0 steamID.
                                continue;

                            if (Tracking.HasMoved(_grid.EntityId, _grid.PositionComp.GetPosition()))
                                continue;

                            bool InGrav = !Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(_grid.PositionComp.GetPosition()));

                            if (!AutoStation_Main.Instance.Config.ConvertGridsInGravity && InGrav) // Dont convert grids in gravity unless enabled.
                                continue;

                            if (_grid.GetOwnerLogoutTimeSeconds() <= AutoStation_Main.Instance.Config.MinutesOffline * 60)
                                continue;

                            // This is a large grid subgrid in gravity, ignore subgrids in gravity option is disabled.
                            if (InGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInGravity && node.ParentLinks.Count > 0)
                            {
                                if (ConvertThis(_grid))
                                {
                                    Interlocked.Increment(ref GridsConverted);
                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);
                                }
                                
                                continue;
                            }

                            // Large grid in gravity, convert in gravity option is enabled.
                            if (InGrav && AutoStation_Main.Instance.Config.ConvertGridsInGravity && node.ParentLinks.Count == 0)
                            {
                                if (ConvertThis(_grid))
                                {
                                    Interlocked.Increment(ref GridsConverted);
                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);
                                }
                                
                                continue;
                            }

                            // Large subgrid in space, ignore sub-grids option is not checked.
                            if (!InGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInSpace && node.ParentLinks.Count > 0)
                            {
                                if (ConvertThis(_grid))
                                {
                                    Interlocked.Increment(ref GridsConverted);
                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);
                                }

                                continue;
                            }

                            // Large grid in space.
                            if (!InGrav && node.ParentLinks.Count == 0)
                            {
                                if (ConvertThis(_grid))
                                {
                                    Interlocked.Increment(ref GridsConverted);
                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);
                                }
                            }
                        }
                    });
                }
                else
                {
                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group group in groups)
                    {
                        foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in group.Nodes)
                        {
                            MyCubeGrid? _grid = node.NodeData;
                            if (_grid is null)
                                continue;

                            if (_grid.IsStatic) // Already in station mode.
                                continue;

                            if (_grid.Physics == null || _grid.IsPreview || _grid.MarkedForClose)
                                continue;

                            // Forced convert by admin command, no gravity options need to be checked.
                            if (forcedByAdmin)
                            {
                                if (PlayerData.IsID_NPC(_grid.BigOwners.FirstOrDefault()))
                                {
                                    Interlocked.Increment(ref NPCGrids);
                                    continue;
                                }
                                
                                if (!smallGrids && _grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) continue;
                                if (!subGrids && node.ParentLinks.Count > 0) continue;

                                if (ConvertThis((MyCubeGrid?)_grid))
                                {
                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);

                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    Interlocked.Increment(ref GridsConverted);
                                }

                                continue;
                            }

                            if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) // Converting small grids back to dynamic is not easy for players.
                            {
                                if (forcedByAdmin && smallGrids == false) continue;
                            }

                            // What to do with grids that have no owners. (Not all blocks have ownership and floating blocks with no owners are still grids.)
                            switch (_grid.BigOwners.Count)
                            {
                                // No owners, no conversion.
                                case 0 when !AutoStation_Main.Instance.Config.StopGridsWithNoOwner:
                                    continue;
                                // No owners, convert the grid.
                                case 0 when AutoStation_Main.Instance.Config.StopGridsWithNoOwner:
                                    if (ConvertThis(_grid))
                                    {
                                        Interlocked.Increment(ref GridsConverted);
                                        if (node.ParentLinks.Count > 0)
                                            Interlocked.Increment(ref SubGridsConverted);
                                    }
                                    
                                    continue;
                            }

                            // NPC owned grids, no conversion.
                            if (PlayerData.IsID_NPC(_grid.BigOwners.FirstOrDefault()))
                                continue;

                            ulong gridOwnerSteamID = MySession.Static.Players.TryGetSteamId(_grid.BigOwners.FirstOrDefault());
                            if (gridOwnerSteamID == 0) // Since an update, sometimes IsNPC doesnt work, so we check for 0 steamID.
                                continue;

                            if (Tracking.HasMoved(_grid.EntityId, _grid.PositionComp.GetPosition()))
                                continue;

                            bool InGrav = !Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(_grid.PositionComp.GetPosition()));

                            if (!AutoStation_Main.Instance.Config.ConvertGridsInGravity && InGrav) // Dont convert grids in gravity unless enabled.
                                continue;

                            if (_grid.GetOwnerLogoutTimeSeconds() <= AutoStation_Main.Instance.Config.MinutesOffline * 60)
                                continue;

                            // This is a large grid subgrid in gravity, ignore subgrids in gravity option is disabled.
                            if (InGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInGravity && node.ParentLinks.Count > 0)
                            {
                                if (ConvertThis(_grid))
                                {
                                    Interlocked.Increment(ref GridsConverted);
                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);
                                }
                                
                                continue;
                            }

                            // Large grid in gravity, convert in gravity option is enabled.
                            if (InGrav && AutoStation_Main.Instance.Config.ConvertGridsInGravity && node.ParentLinks.Count == 0)
                            {
                                if (ConvertThis(_grid))
                                {
                                    Interlocked.Increment(ref GridsConverted);
                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);
                                }

                                continue;
                            }

                            // Large subgrid in space, ignore sub-grids option is not checked.
                            if (!InGrav && !AutoStation_Main.Instance.Config.IgnoreSubGridsInSpace && node.ParentLinks.Count > 0)
                            {
                                if (ConvertThis(_grid))
                                {
                                    Interlocked.Increment(ref GridsConverted);
                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);
                                }

                                continue;
                            }

                            // Large grid in space.
                            if (!InGrav && node.ParentLinks.Count == 0)
                            {
                                if (ConvertThis(_grid))
                                {
                                    Interlocked.Increment(ref GridsConverted);
                                    if (_grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                        Interlocked.Increment(ref SmallGrids);
                                    else
                                        Interlocked.Increment(ref LargeGridsConverted);

                                    if (node.ParentLinks.Count > 0)
                                        Interlocked.Increment(ref SubGridsConverted);
                                }
                            }
                        }
                    }
                }

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
                log.AppendLine($"Ignored {NPCGrids} NPC owned grids/subgrids.");
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
                isRunning = false;
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

                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node? GroupNode in group.Nodes)
                    {
                        if (GroupNode.NodeData.BigOwners == null) // Not all blocks have ownership and floating blocks with no owners are still grids.
                            continue;

                        if (PlayerData.IsID_NPC(GroupNode.NodeData.BigOwners.FirstOrDefault()))
                            continue;

                        TrackedGrids.TryAdd(GroupNode.NodeData.EntityId, GroupNode.NodeData.PositionComp.GetPosition());
                    }
                });
            } catch (Exception e)
            {
                AutoStation_Main.Log.Error(e);
            }
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
