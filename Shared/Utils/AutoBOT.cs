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
using VRage.Groups;
using VRageMath;

namespace AutoStation.Utils
{
    public static class Auto
    {
        private static Timer? _autoRunTimer;
        private static readonly TimerCallback AutoRunTimerCb = TimedAutoRun;
        private static readonly ConcurrentQueue<string> ConversionLog = new ConcurrentQueue<string>();
        private static int _isRunning;

        // A frequency below one minute would make the timer fire without pause.
        private static TimeSpan Period(int frequency) => TimeSpan.FromMinutes(Math.Max(frequency, 1));

        public static void UpdateTimer(int newFrequency)
        {
            _autoRunTimer?.Change(Period(newFrequency), Period(newFrequency));
        }

        public static void Init()
        {
            // Never run on world load: with no start delay, the first run waits one full period.
            TimeSpan period = Period(AutoStationCore.Config.RunFrequency);
            TimeSpan firstRun = AutoStationCore.Config.DelayStart > 0 ? TimeSpan.FromMinutes(AutoStationCore.Config.DelayStart) : period;
            _autoRunTimer = new Timer(AutoRunTimerCb, null, firstRun, period);
        }

        public static void Dispose()
        {
            _autoRunTimer?.Dispose();
            _autoRunTimer = null;
            Tracking.TrackedGrids.Clear();
        }

        // Runs on a pool thread, so it only hands the work to the game thread.
        private static void TimedAutoRun(object? stateInfo)
        {
            try
            {
                MySandboxGame.Static.Invoke(RunFromTimer, "AutoStation");
            }
            catch (Exception e)
            {
                AutoStationCore.Host.LogError(e, "The timed AutoConvert run failed");
            }
        }

        private static void RunFromTimer()
        {
            StartRun();
            UpdateTimer(AutoStationCore.Config.RunFrequency);
        }

        /// <summary>Queues a run on the game thread. Safe to call from any thread.</summary>
        public static void RequestRun(bool forcedByAdmin = false, bool smallGrids = false, bool subGrids = false)
        {
            MySandboxGame.Static.Invoke(() => StartRun(forcedByAdmin, smallGrids, subGrids), "AutoStation");
        }

        // Game thread: copies what cannot be read elsewhere, then leaves the rest of the run to the pool so it does not block the game.
        private static void StartRun(bool forcedByAdmin = false, bool smallGrids = false, bool subGrids = false)
        {
            if (MySession.Static == null || !AutoStationCore.Config.Enable || MySession.Static.IsSaveInProgress || !MySession.Static.Ready || MySandboxGame.IsPaused)
                return;

            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                AutoStationCore.Host.LogInfo("AutoStation already processing grids.");
                return;
            }

            try
            {
                Stopwatch msTracker = Stopwatch.StartNew();
                List<GridInfo> grids = TakeSnapshot();

                // Get online player locations
                List<Vector3D> onlinePlayerLocations = new List<Vector3D>();
                foreach (MyPlayer? onlinePlayer in MySession.Static.Players.GetOnlinePlayers())
                    onlinePlayerLocations.Add(onlinePlayer.GetPosition());

                Task.Run(() => AutoRun(grids, onlinePlayerLocations, msTracker, forcedByAdmin, smallGrids, subGrids));
            }
            catch (Exception e)
            {
                Interlocked.Exchange(ref _isRunning, 0);
                AutoStationCore.Host.LogError(e, "AutoConvert failed");
            }
        }

        /// <summary>Copies every dynamic grid. Game thread only: the groups are plain collections the game changes between updates,
        /// and only there does BigOwners give a subgrid with no owned blocks the owner of its parent grid.</summary>
        private static List<GridInfo> TakeSnapshot()
        {
            List<GridInfo> grids = new List<GridInfo>();

            foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group group in MyCubeGridGroups.Static.Mechanical.Groups)
            {
                bool piloted = IsPiloted(group);

                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in group.Nodes)
                {
                    MyCubeGrid? grid = node.NodeData;
                    if (grid is null)
                        continue;

                    if (grid.IsStatic) // Already in station mode.
                        continue;

                    if (grid.Physics == null || grid.IsPreview || grid.MarkedForClose)
                        continue;

                    List<long>? owners = grid.BigOwners;
                    long owner = owners?.FirstOrDefault() ?? 0;
                    grids.Add(new GridInfo(grid, node.ParentLinks.Count > 0, owners?.Count ?? 0, owner, MySession.Static.Players.TryGetSteamId(owner), grid.GetOwnerLogoutTimeSeconds(), grid.PositionComp.GetPosition(), piloted));
                }
            }

            return grids;
        }

        // Each grid has its own control system, so a subgrid does not report the pilot of the ship it is on.
        private static bool IsPiloted(MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group group)
        {
            foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in group.Nodes)
            {
                if (node.NodeData?.GridSystems?.ControlSystem?.IsControlled == true)
                    return true;
            }

            return false;
        }

        private static void AutoRun(List<GridInfo> grids, List<Vector3D> onlinePlayerLocations, Stopwatch msTracker, bool forcedByAdmin, bool smallGrids, bool subGrids)
        {
            try
            {
                int largeGridsConverted = 0;
                int gridsConverted = 0;
                int subGridsConverted = 0;
                int smallGridsConverted = 0;
                int npcGrids = 0;

                double minDistanceSquared = (double)AutoStationCore.Config.MinDistanceFromPlayers * AutoStationCore.Config.MinDistanceFromPlayers;

                Parallel.ForEach(grids, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(MySandboxGame.NumberOfCores / 3, 1) }, info =>
                {
                    MyCubeGrid grid = info.Grid;

                    // Forced convert by admin command, no gravity options need to be checked.
                    if (forcedByAdmin)
                    {
                        if (info.IsNpc)
                        {
                            Interlocked.Increment(ref npcGrids);
                            return;
                        }
                        
                        if (!smallGrids && grid.GridSizeEnum == VRage.Game.MyCubeSize.Small) return; // Converting small grids back to dynamic is not easy for players.
                        if (!subGrids && info.IsSubGrid) return;

                        if (ConvertThis(info))
                        {
                            if (info.IsSubGrid)
                                Interlocked.Increment(ref subGridsConverted);

                            if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref smallGridsConverted);
                            else
                                Interlocked.Increment(ref largeGridsConverted);

                            Interlocked.Increment(ref gridsConverted);
                        }

                        return;
                    }

                    // A team mate can be flying a grid whose owner is offline. It would stop dead, and only its owner or an admin could turn it back.
                    if (info.IsPiloted)
                        return;

                    // Check if the grid is near players and skip if needed
                    if (AutoStationCore.Config.MinDistanceFromPlayers > 0)
                    {
                        bool skipNeeded = false;
                        foreach (Vector3D position in onlinePlayerLocations)
                        {
                            if (Vector3D.DistanceSquared(position, info.Position) <= minDistanceSquared)
                                skipNeeded = true;
                        }
                        if (skipNeeded) return;
                    }
                    
                    bool inGrav = !Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(info.Position));
                    
                    // What to do with grids that have no owners. (Not all blocks have ownership, and floating blocks with no owners are still grids.)
                    switch (info.OwnerCount)
                    {
                        // No owners, no conversion.
                        case 0 when !AutoStationCore.Config.StopGridsWithNoOwner:
                            return;
                        
                        // No owners, convert the grid.
                        case 0 when AutoStationCore.Config.StopGridsWithNoOwner:
                            if (info.IsSubGrid && ((AutoStationCore.Config.IgnoreSubGridsInSpace && !inGrav) || (AutoStationCore.Config.IgnoreSubGridsInGravity && inGrav)))
                                return;
                            
                            if (ConvertThis(info))
                            {
                                Interlocked.Increment(ref gridsConverted);
                                if (info.IsSubGrid)
                                    Interlocked.Increment(ref subGridsConverted);
                            }
                            
                            return;
                    }

                    // NPC owned grids, no conversion.
                    if (info.IsNpc)
                        return;

                    if (Tracking.HasMoved(grid.EntityId, info.Position))
                        return;

                    if (!AutoStationCore.Config.ConvertGridsInGravity && inGrav) // Don't convert grids in gravity unless enabled.
                        return;

                    if (info.LogoutSeconds <= AutoStationCore.Config.MinutesOffline * 60)
                        return;

                    // This is a large grid subgrid in gravity, ignore the sub-grids in gravity option is disabled.
                    if (inGrav && !AutoStationCore.Config.IgnoreSubGridsInGravity && info.IsSubGrid)
                    {
                        if (ConvertThis(info))
                        {
                            Interlocked.Increment(ref gridsConverted);
                            if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref smallGridsConverted);
                            else
                                Interlocked.Increment(ref largeGridsConverted);

                            if (info.IsSubGrid)
                                Interlocked.Increment(ref subGridsConverted);
                        }
                        
                        return;
                    }

                    // Large grid in gravity, convert in gravity option is enabled.
                    if (inGrav && AutoStationCore.Config.ConvertGridsInGravity && !info.IsSubGrid)
                    {
                        if (ConvertThis(info))
                        {
                            Interlocked.Increment(ref gridsConverted);
                            if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref smallGridsConverted);
                            else
                                Interlocked.Increment(ref largeGridsConverted);

                            if (info.IsSubGrid)
                                Interlocked.Increment(ref subGridsConverted);
                        }
                        
                        return;
                    }

                    // Large subgrid in space, ignore sub-grids option is not checked.
                    if (!inGrav && !AutoStationCore.Config.IgnoreSubGridsInSpace && info.IsSubGrid)
                    {
                        if (ConvertThis(info))
                        {
                            Interlocked.Increment(ref gridsConverted);
                            if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref smallGridsConverted);
                            else
                                Interlocked.Increment(ref largeGridsConverted);

                            if (info.IsSubGrid)
                                Interlocked.Increment(ref subGridsConverted);
                        }

                        return;
                    }

                    // Large grid in space.
                    if (!inGrav && !info.IsSubGrid)
                    {
                        if (ConvertThis(info))
                        {
                            Interlocked.Increment(ref gridsConverted);
                            if (grid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                                Interlocked.Increment(ref smallGridsConverted);
                            else
                                Interlocked.Increment(ref largeGridsConverted);

                            if (info.IsSubGrid)
                                Interlocked.Increment(ref subGridsConverted);
                        }
                    }
                });

                msTracker.Stop();

                StringBuilder log = new StringBuilder().AppendLine();
                while (ConversionLog.TryDequeue(out string? _log))
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

                AutoStationCore.Host.LogInfo(log.ToString());
            }
            catch (Exception e)
            {
                AutoStationCore.Host.LogError(e, "AutoConvert failed");
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ConvertThis(GridInfo info)
        {
            MyCubeGrid grid = info.Grid;

            if (AutoStationCore.Config.ShowConvertedGridsNameLog)
            {
                if (info.OwnerCount > 0)
                {
                    MyIdentity? identity = MySession.Static.Players.TryGetIdentity(info.Owner);

                    ConversionLog.Enqueue(AutoStationCore.Config.ShowConvertedGridsOwnerNameLog
                        ? $"Converting [{grid.DisplayName}] owned by {identity?.DisplayName ?? "unknown"}({info.OwnerSteamId})"
                        : $"Converting [{grid.DisplayName}]");
                }
                else
                {
                    ConversionLog.Enqueue($"Converting grid with no owners [{grid.DisplayName}]");
                }
            }

            MySandboxGame.Static.Invoke(() =>
            {
                grid.Physics?.Clear(); // Stop any drifting
                grid.Physics?.ClearSpeed(); // meh

                grid.RequestConversionToStation();
            }, "AutoStation");

            return true;
        }
    }

    /// <summary>What a run needs from a grid, read on the game thread by <see cref="Auto.TakeSnapshot"/>.</summary>
    internal readonly struct GridInfo(MyCubeGrid grid, bool isSubGrid, int ownerCount, long owner, ulong ownerSteamId, float logoutSeconds, Vector3D position, bool isPiloted)
    {
        public readonly MyCubeGrid Grid = grid;
        public readonly bool IsSubGrid = isSubGrid;
        public readonly int OwnerCount = ownerCount;
        public readonly long Owner = owner;
        public readonly ulong OwnerSteamId = ownerSteamId;
        public readonly float LogoutSeconds = logoutSeconds;
        public readonly Vector3D Position = position;
        public readonly bool IsPiloted = isPiloted;

        // MySession.Static.Players.IdentityIsNpc(id) does not always return true if the owner is an NPC.... so we check for 0 steamID.
        public bool IsNpc => OwnerSteamId == 0;
    }

    public static class Tracking
    {
        public static readonly ConcurrentDictionary<long, Vector3D> TrackedGrids = new ConcurrentDictionary<long, Vector3D>();
        
        /// <summary>Whether a grid has been in use since the last run. One seen for the first time counts as moved, so with tracking on
        /// the first run after a start or restart only records where grids are, and the run after it is the first that can convert them.</summary>
        public static bool HasMoved(long id, Vector3D location)
        {
            if (!AutoStationCore.Config.GridTrackingMode)
                return false;

            if (TrackedGrids.TryGetValue(id, out Vector3D savedLocation))
            {
                if (Vector3D.Distance(savedLocation, location) < AutoStationCore.Config.MinDistanceToBeConsideredInUse)
                    return false;

                TrackedGrids.TryUpdate(id, location, savedLocation);
                return true;
            }

            TrackedGrids.TryAdd(id, location);
            return true;
        }
    }
}
