using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Torch;

namespace AutoStation_SaveYourSS
{
    public class AutoStation_Config : ViewModel
    {
        // The plugin will only convert grids to station mode when enabled.
        private bool _Enable = true;
        public bool Enable { get => _Enable; set => SetValue(ref _Enable, value); }

        private int _DelayStart = 0; // Delay in milliseconds before AutoRun starts.
        public int DelayStart { get => _DelayStart; set => SetValue(ref _DelayStart, value);}

        private int _RunFrequency = 1800000; // Runs the AutoStation every 30 minutes.
        public int RunFrequency { get => _RunFrequency; set => SetValue(ref _RunFrequency, value);}

        private int _MinutesOffline = 60;
        public int MinutesOffline { get => _MinutesOffline; set => SetValue(ref _MinutesOffline, value);}

        private bool _ConvertGridsInGravity = false;
        public bool ConvertGridsInGravity { get => _ConvertGridsInGravity; set => SetValue(ref _ConvertGridsInGravity, value); }

    }
}
