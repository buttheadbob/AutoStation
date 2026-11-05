using PluginSdk.Config;

namespace AutoStation.MagnetarHost;

/// <summary>Magnetar config. Quasar renders the editor from these attributes, there is no UI code.</summary>
[Section("general", caption: "General")]
[Section("gravity", caption: "Gravity")]
[Section("tracking", caption: "Grid tracking")]
[Section("subgrids", caption: "Subgrids")]
[Section("unowned", caption: "Unowned grids")]
public class AutoStationConfig : PluginConfig, IAutoStationConfig
{
    [BoolOption("The plugin will only convert grids to station mode when enabled", Parent = "general")]
    public bool Enable { get; set => SetField(ref field, value); } = AutoStationCore.DefaultEnable;

    [BoolOption("Show grid names in the log", Parent = "general")]
    public bool ShowConvertedGridsNameLog { get; set => SetField(ref field, value); }

    [BoolOption("Show owner names in the log, with the grid names", Parent = "general")]
    public bool ShowConvertedGridsOwnerNameLog { get; set => SetField(ref field, value); }

    [IntOption(0, 10080, "Delay start in minutes: time from when the world loads until the automatic converter starts running. At 0, the first run comes after one Frequency period", Parent = "general")]
    public int DelayStart { get; set => SetField(ref field, value); }

    [IntOption(1, 10080, "Frequency in minutes: how often AutoConvert runs", Parent = "general")]
    public int RunFrequency { get; set => SetField(ref field, value); } = AutoStationCore.DefaultRunFrequency;

    [IntOption(0, 525600, "Minutes offline: how long the grid owner needs to be offline before converting to station", Parent = "general")]
    public int MinutesOffline { get; set => SetField(ref field, value); } = AutoStationCore.DefaultMinutesOffline;

    // The upper limit keeps its square inside an int.
    [IntOption(0, 46340, "Minimum distance from players: any grid inside this range is skipped from all checks, except when an admin runs it. 0 ignores player distances", Parent = "general")]
    public int MinDistanceFromPlayers { get; set => SetField(ref field, value); }

    [BoolOption("Convert grids in gravity. Not recommended: any grids on or under the surface may become stuck in a voxel and require players to dig them out before converting to dynamic mode again", Parent = "gravity")]
    public bool ConvertGridsInGravity { get; set => SetField(ref field, value); }

    [BoolOption("Track grids: allows players to use team mates' grids without them turning to station mode, as long as they don't stay in one location for 2 consecutive checks. The first check after a start or restart only records where grids are", Parent = "tracking")]
    public bool GridTrackingMode { get; set => SetField(ref field, value); }

    [DoubleOption(0, 1000000, "Minimum distance a grid must have moved to be considered in use. This helps detect slow drifting ships that are not in use", Parent = "tracking")]
    public double MinDistanceToBeConsideredInUse { get; set => SetField(ref field, value); } = AutoStationCore.DefaultMinDistanceToBeConsideredInUse;

    [BoolOption("Ignore subgrids in space, example: solar panel arrays", Parent = "subgrids")]
    public bool IgnoreSubGridsInSpace { get; set => SetField(ref field, value); } = AutoStationCore.DefaultIgnoreSubGridsInSpace;

    [BoolOption("Ignore subgrids in gravity, example: wheels and solar panel arrays", Parent = "subgrids")]
    public bool IgnoreSubGridsInGravity { get; set => SetField(ref field, value); } = AutoStationCore.DefaultIgnoreSubGridsInGravity;

    [BoolOption("Convert unowned grids, example: chunks of armor blocks floating around after battles. These are not tracked and do not follow the gravity rules. Careful with this, it may create a field of death blocks for players to fly into", Parent = "unowned")]
    public bool StopGridsWithNoOwner { get; set => SetField(ref field, value); }
}
