using System;

// Loader neutral core, compiled into BOTH the Torch plugin and the Magnetar plugin.
// Everything under Shared/ uses game assemblies (Sandbox.*, VRage.*) only, with no Torch, NLog or PluginSdk references,
// and talks to the plugin that hosts it through IAutoStationHost and IAutoStationConfig.
namespace AutoStation;

/// <summary>The settings both hosts expose (Torch: WPF and a .cfg file, Magnetar: Quasar and XML).</summary>
public interface IAutoStationConfig
{
    bool Enable { get; }
    int DelayStart { get; }
    int RunFrequency { get; }
    int MinutesOffline { get; }
    bool ConvertGridsInGravity { get; }
    bool GridTrackingMode { get; }
    bool IgnoreSubGridsInSpace { get; }
    bool IgnoreSubGridsInGravity { get; }
    bool ShowConvertedGridsNameLog { get; }
    bool ShowConvertedGridsOwnerNameLog { get; }
    double MinDistanceToBeConsideredInUse { get; }
    bool StopGridsWithNoOwner { get; }
    int MinDistanceFromPlayers { get; }
}

/// <summary>What the core needs from whichever plugin loader it runs in.</summary>
public interface IAutoStationHost
{
    IAutoStationConfig Config { get; }
    void LogInfo(string message);
    void LogError(Exception exception, string message);
}

public static class AutoStationCore
{
    public const bool DefaultEnable = true;
    public const int DefaultRunFrequency = 30;
    public const int DefaultMinutesOffline = 60;
    public const bool DefaultIgnoreSubGridsInSpace = true;
    public const bool DefaultIgnoreSubGridsInGravity = true;
    public const double DefaultMinDistanceToBeConsideredInUse = 10.0D;

    public const string AboutText = "This plugin is used to place grids into station mode when a player has been offline for a preset amount of time.  Grids not in station mode can use many times the resources and processing power compared to stations.  For any questions, additions, or issues... please contact SentorX#4108 on discord or on the Torch Discord server.";

    /// <summary>Set by the host plugin in Init, before a session or command can run.</summary>
    public static IAutoStationHost Host { get; set; } = null!;

    public static IAutoStationConfig Config => Host.Config;
}
