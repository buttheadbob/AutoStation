using AutoStation.Utils;
using NLog;
using System;
using System.IO;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;

namespace AutoStation
{
    public class AutoStation_Main : TorchPluginBase, IWpfPlugin
    {

        public static readonly Logger Log = LogManager.GetLogger("AutoStation");
        private static readonly string CONFIG_FILE_NAME = "AutoStation___Save_Your_SSConfig.cfg";

        private AutoStation_Control _control;
        public UserControl GetControl() => _control ?? (_control = new AutoStation_Control());

        private Persistent<AutoStation_Config> _config;
        public AutoStation_Config Config => _config?.Data;
        public static AutoStation_Main Instance { get; private set; }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            SetupConfig();

            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (sessionManager != null)
                sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager loaded!");
            Instance = this;
            Save();
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            switch (state)
            {
                case TorchSessionState.Loaded:
                    Log.Info("Session Loaded!");
                    Auto.Init();
                    break;

                case TorchSessionState.Unloading:
                    Log.Info("Session Unloading!");
                    Auto.Dispose();
                    break;
            }
        }

        private void SetupConfig()
        {
            var configFile = Path.Combine(StoragePath, CONFIG_FILE_NAME);

            try
            {

                _config = Persistent<AutoStation_Config>.Load(configFile);

            }
            catch (Exception e)
            {
                Log.Warn(e);
            }

            if (_config?.Data == null)
            {

                Log.Info("Create Default Config, because none was found!");

                _config = new Persistent<AutoStation_Config>(configFile, new AutoStation_Config());
                _config.Save();
            }
        }

        public void Save()
        {
            try
            {
                _config.Save();
                Log.Info("Configuration Saved.");
            }
            catch (IOException e)
            {
                Log.Warn(e, "Configuration failed to save");
            }
        }
    }
}
