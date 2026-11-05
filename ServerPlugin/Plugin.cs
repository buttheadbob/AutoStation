using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using AutoStation.Utils;
using PluginSdk.Commands;
using PluginSdk.Config;
using PluginSdk.Logging;
using Sandbox.Game.World;
using VRage.FileSystem;
using VRage.Plugins;

namespace AutoStation.MagnetarHost;

/// <summary>Magnetar host: Quasar config and PluginSdk commands. The conversion logic is in the shared code.</summary>
// ReSharper disable once UnusedType.Global
public class Plugin : IPlugin, IAutoStationHost
{
    public const string Name = "AutoStation";
    private static readonly Logger Log = Logger.Create(Name);

    private AutoStationConfig _config = new();
    private string _configPath = "";

    // Quasar finds the config through this public PluginConfig-typed property and renders its editor.
    public AutoStationConfig PluginConfig => _config;

    // IAutoStationHost
    IAutoStationConfig IAutoStationHost.Config => _config;
    void IAutoStationHost.LogInfo(string message) => Log.Info(message);
    void IAutoStationHost.LogError(Exception exception, string message) => Log.Error(message, exception);

    public void Init(object gameInstance)
    {
        Log.Info("Loading");

        _configPath = Path.Combine(MyFileSystem.UserDataPath, Name + ".cfg");
        try
        {
            _config = ConfigStorage.LoadXml<AutoStationConfig>(_configPath);
        }
        catch (Exception e)
        {
            Log.Error("Could not read the config, using defaults", e);
            _config = new AutoStationConfig();
        }
        // Edits made in Quasar arrive through the config's setters: persist them.
        _config.PropertyChanged += OnConfigChanged;

        AutoStationCore.Host = this;

        // The same two events Torch reports as its Loaded and Unloading session states.
        MySession.AfterLoading += OnSessionLoaded;
        MySession.OnUnloading += OnSessionUnloading;

        ServerCommands.Register(Assembly.GetExecutingAssembly());
        Log.Info("Loaded (Magnetar)");
    }

    public void Update()
    {
    }

    public void Dispose()
    {
        MySession.AfterLoading -= OnSessionLoaded;
        MySession.OnUnloading -= OnSessionUnloading;
        _config.PropertyChanged -= OnConfigChanged;
        Auto.Dispose();
    }

    private static void OnSessionLoaded()
    {
        Log.Info("Session Loaded!");
        Auto.Init();
    }

    private static void OnSessionUnloading()
    {
        Log.Info("Session Unloading!");
        Auto.Dispose();
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        SaveConfig();

        if (e.PropertyName == nameof(AutoStationConfig.RunFrequency))
            Auto.UpdateTimer(_config.RunFrequency);
    }

    private void SaveConfig()
    {
        try
        {
            ConfigStorage.SaveXml(_config, _configPath);
        }
        catch (Exception e)
        {
            Log.Error("Could not save the config", e);
        }
    }
}
