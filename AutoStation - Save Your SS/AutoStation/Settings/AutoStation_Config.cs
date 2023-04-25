﻿using Sandbox.Graphics.GUI;
using Torch;

namespace AutoStation
{
    public class AutoStation_Config : ViewModel
    {
        // The plugin will only convert grids to station mode when enabled.
        private bool _Enable = true;
        public bool Enable { get => _Enable; set => SetValue(ref _Enable, value); }

        private int _DelayStart = 0; // Delay in milliseconds before AutoRun starts.
        public int DelayStart { get => _DelayStart; set => SetValue(ref _DelayStart, value);}

        private int _RunFrequency = 30; // Runs the AutoStation every 30 minutes.
        public int RunFrequency { get => _RunFrequency; set => SetValue(ref _RunFrequency, value);}

        private int _MinutesOffline = 60;
        public int MinutesOffline { get => _MinutesOffline; set => SetValue(ref _MinutesOffline, value);}

        private bool _ConvertGridsInGravity = false;
        public bool ConvertGridsInGravity { get => _ConvertGridsInGravity; set => SetValue(ref _ConvertGridsInGravity, value); }

        private bool _GridTrackingMode = false;
        public bool GridTrackingMode { get => _GridTrackingMode; set => SetValue(ref _GridTrackingMode, value);}

        private bool _ignoreSubGridsInSpace = false; // Solar Arrays, and so on... I hope... ferking players!
        public bool IgnoreSubGridsInSpace { get => _ignoreSubGridsInSpace; set => SetValue(ref _ignoreSubGridsInSpace, value); }

        private bool _ignoreSubGridsInGravity = true; // Wheels, Solar Arrays, and so on... I hope... ferking players!
        public bool IgnoreSubGridsInGravity { get => _ignoreSubGridsInGravity; set => SetValue(ref _ignoreSubGridsInGravity, value); }

        private bool _showConvertedGridsNameLog = false;
        public bool ShowConvertedGridsNameLog { get => _showConvertedGridsNameLog; set => SetValue(ref _showConvertedGridsNameLog, value); }
        
        private bool _showConvertedGridsOwnerNameLog = false;
        public bool ShowConvertedGridsOwnerNameLog { get => _showConvertedGridsOwnerNameLog; set => SetValue(ref _showConvertedGridsOwnerNameLog, value); }
    }
}
