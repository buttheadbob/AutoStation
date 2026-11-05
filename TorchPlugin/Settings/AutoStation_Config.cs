using Torch;

namespace AutoStation
{
    public class AutoStation_Config : ViewModel, IAutoStationConfig
    {
        // The plugin will only convert grids to station mode when enabled.
        private bool _Enable = AutoStationCore.DefaultEnable;
        public bool Enable { get => _Enable; set => SetValue(ref _Enable, value); }

        private int _DelayStart = 0; // Delay in minutes before AutoRun starts.
        public int DelayStart { get => _DelayStart; set => SetValue(ref _DelayStart, value);}

        private int _RunFrequency = AutoStationCore.DefaultRunFrequency; // Runs the AutoStation every 30 minutes.
        public int RunFrequency { get => _RunFrequency; set => SetValue(ref _RunFrequency, value);}

        private int _MinutesOffline = AutoStationCore.DefaultMinutesOffline;
        public int MinutesOffline { get => _MinutesOffline; set => SetValue(ref _MinutesOffline, value);}

        private bool _ConvertGridsInGravity;
        public bool ConvertGridsInGravity { get => _ConvertGridsInGravity; set => SetValue(ref _ConvertGridsInGravity, value); }

        private bool _GridTrackingMode;
        public bool GridTrackingMode { get => _GridTrackingMode; set => SetValue(ref _GridTrackingMode, value);}

        private bool _ignoreSubGridsInSpace = AutoStationCore.DefaultIgnoreSubGridsInSpace; // Solar Arrays, and so on... I hope... ferking players!
        public bool IgnoreSubGridsInSpace { get => _ignoreSubGridsInSpace; set => SetValue(ref _ignoreSubGridsInSpace, value); }

        private bool _ignoreSubGridsInGravity = AutoStationCore.DefaultIgnoreSubGridsInGravity; // Wheels, Solar Arrays, and so on... I hope... ferking players!
        public bool IgnoreSubGridsInGravity { get => _ignoreSubGridsInGravity; set => SetValue(ref _ignoreSubGridsInGravity, value); }

        private bool _showConvertedGridsNameLog;
        public bool ShowConvertedGridsNameLog { get => _showConvertedGridsNameLog; set => SetValue(ref _showConvertedGridsNameLog, value); }
        
        private bool _showConvertedGridsOwnerNameLog;
        public bool ShowConvertedGridsOwnerNameLog { get => _showConvertedGridsOwnerNameLog; set => SetValue(ref _showConvertedGridsOwnerNameLog, value); }

        private double _minDistanceToBeConsideredInUse = AutoStationCore.DefaultMinDistanceToBeConsideredInUse;
        public double MinDistanceToBeConsideredInUse { get => _minDistanceToBeConsideredInUse; set => SetValue(ref _minDistanceToBeConsideredInUse, value); }
        
        private bool _stopGridsWithNoOwner;
        public bool StopGridsWithNoOwner { get => _stopGridsWithNoOwner; set => SetValue(ref _stopGridsWithNoOwner, value); }

        private int _minDistanceFromPlayers;
        public int MinDistanceFromPlayers { get => _minDistanceFromPlayers; set => SetValue(ref _minDistanceFromPlayers, value); }

    }
}
