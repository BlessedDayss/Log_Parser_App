using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Log_Parser_App.Models
{
    public class PostgreSQLSettings : INotifyPropertyChanged
    {
        private string _host = "localhost";
        private int _port = 5432;
        private string _username = "postgres";
        private string _password = "postgres";
        private string _database = "log4net";
        private bool _isEnabled = true;

        [Required]
        [Display(Name = "Host")]
        public string Host
        {
            get => _host;
            set
            {
                _host = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionString));
            }
        }

        [Required]
        [Range(1, 65535)]
        [Display(Name = "Port")]
        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionString));
            }
        }

        [Required]
        [Display(Name = "Username")]
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionString));
            }
        }

        [Required]
        [Display(Name = "Password")]
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionString));
            }
        }

        [Required]
        [Display(Name = "Database")]
        public string Database
        {
            get => _database;
            set
            {
                _database = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionString));
            }
        }

        [Display(Name = "Enable PostgreSQL")]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};";
        
        public string AdminConnectionString => $"Host={Host};Port={Port};Database=postgres;Username={Username};Password={Password};";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public PostgreSQLSettings Clone()
        {
            return new PostgreSQLSettings
            {
                Host = Host,
                Port = Port,
                Username = Username,
                Password = Password,
                Database = Database,
                IsEnabled = IsEnabled
            };
        }
    }
}
