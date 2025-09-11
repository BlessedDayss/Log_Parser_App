namespace Log_Parser_App.Models.Analytics
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Settings for column widths and visibility in RabbitMQ dashboard
    /// </summary>
    public class ColumnSettings : INotifyPropertyChanged
    {
        private double _sentTimeWidth = 120;
        private double _userNameWidth = 100;
        private double _processUIDWidth = 100;
        private double _nodeWidth = 100;
        private double _errorMessageWidth = 200;
        private double _stackTraceWidth = 150;

        /// <summary>
        /// Width of SentTime column
        /// </summary>
        public double SentTimeWidth
        {
            get => _sentTimeWidth;
            set => SetProperty(ref _sentTimeWidth, value);
        }

        /// <summary>
        /// Width of UserName column
        /// </summary>
        public double UserNameWidth
        {
            get => _userNameWidth;
            set => SetProperty(ref _userNameWidth, value);
        }

        /// <summary>
        /// Width of ProcessUID column
        /// </summary>
        public double ProcessUIDWidth
        {
            get => _processUIDWidth;
            set => SetProperty(ref _processUIDWidth, value);
        }

        /// <summary>
        /// Width of Node column
        /// </summary>
        public double NodeWidth
        {
            get => _nodeWidth;
            set => SetProperty(ref _nodeWidth, value);
        }

        /// <summary>
        /// Width of ErrorMessage column
        /// </summary>
        public double ErrorMessageWidth
        {
            get => _errorMessageWidth;
            set => SetProperty(ref _errorMessageWidth, value);
        }

        /// <summary>
        /// Width of StackTrace column
        /// </summary>
        public double StackTraceWidth
        {
            get => _stackTraceWidth;
            set => SetProperty(ref _stackTraceWidth, value);
        }

        /// <summary>
        /// Reset all column widths to default values
        /// </summary>
        public void ResetToDefaults()
        {
            SentTimeWidth = 120;
            UserNameWidth = 100;
            ProcessUIDWidth = 100;
            NodeWidth = 100;
            ErrorMessageWidth = 200;
            StackTraceWidth = 150;
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
} 