using Microsoft.Extensions.Logging;
using Npgsql;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Log_Parser_App.Services
{
    public class Log4NetService : ILog4NetService
    {
        private readonly ILogger<Log4NetService> _logger;
        private readonly IPostgreSQLSettingsService _settingsService;

        public Log4NetService(ILogger<Log4NetService> logger, IPostgreSQLSettingsService settingsService)
        {
            _logger = logger;
            _settingsService = settingsService;
        }

        public async Task InitializeDatabaseAsync()
        {
            _logger.LogInformation("Initializing Log4Net database");
            
            var settings = await _settingsService.LoadSettingsAsync();
            if (!settings.IsEnabled)
            {
                _logger.LogInformation("PostgreSQL integration is disabled");
                return;
            }

            // Test connection and create database if needed
            if (await _settingsService.TestConnectionAsync(settings))
            {
                await _settingsService.CreateDatabaseIfNotExistsAsync(settings);
                await CreateTableIfNotExistsAsync(settings);
            }
        }

        public async Task SaveLog4NetLogsAsync(List<Log_Parser_App.Models.Log4NetLogEntry> logEntries)
        {
            _logger.LogInformation($"Saving {logEntries.Count} Log4Net log entries to PostgreSQL");
            
            var settings = await _settingsService.LoadSettingsAsync();
            if (!settings.IsEnabled)
            {
                _logger.LogInformation("PostgreSQL integration is disabled, skipping save");
                return;
            }

            try
            {
                using var connection = new NpgsqlConnection(settings.ConnectionString);
                await connection.OpenAsync();

                // Clear existing data
                using var clearCmd = new NpgsqlCommand("DELETE FROM log4net", connection);
                await clearCmd.ExecuteNonQueryAsync();

                // Insert new data
                foreach (var entry in logEntries)
                {
                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO log4net (""Date"", ""Host"", ""Site"", ""Thread"", ""Level"", ""Logger"", ""User"", ""Message"", ""Exception"", ""MessageObject"") 
                        VALUES (@date, @host, @site, @thread, @level, @logger, @user, @message, @exception, @messageObject)", connection);

                    cmd.Parameters.AddWithValue("date", entry.Date);
                    cmd.Parameters.AddWithValue("host", (object?)entry.Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("site", (object?)entry.Site ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("thread", (object?)entry.Thread ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("level", (object?)entry.Level ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("logger", (object?)entry.Logger ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("user", (object?)entry.User ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("message", (object?)entry.Message ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("exception", (object?)entry.Exception ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("messageObject", (object?)entry.MessageObject ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation($"Successfully saved {logEntries.Count} Log4Net entries to PostgreSQL");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Log4Net entries to PostgreSQL");
                throw;
            }
        }

        public async Task<List<Log_Parser_App.Models.Log4NetLogEntry>> GetLog4NetLogsAsync()
        {
            _logger.LogInformation("Retrieving Log4Net log entries from PostgreSQL");
            
            var settings = await _settingsService.LoadSettingsAsync();
            if (!settings.IsEnabled)
            {
                _logger.LogInformation("PostgreSQL integration is disabled");
                return new List<Log_Parser_App.Models.Log4NetLogEntry>();
            }

            var entries = new List<Log_Parser_App.Models.Log4NetLogEntry>();

            try
            {
                using var connection = new NpgsqlConnection(settings.ConnectionString);
                await connection.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
                    SELECT ""Id"", ""Date"", ""Host"", ""Site"", ""Thread"", ""Level"", ""Logger"", ""User"", ""Message"", ""Exception"", ""MessageObject""
                    FROM log4net 
                    ORDER BY ""Date"" DESC", connection);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    entries.Add(new Log_Parser_App.Models.Log4NetLogEntry
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                        Host = reader.IsDBNull(reader.GetOrdinal("Host")) ? null : reader.GetString(reader.GetOrdinal("Host")),
                        Site = reader.IsDBNull(reader.GetOrdinal("Site")) ? null : reader.GetString(reader.GetOrdinal("Site")),
                        Thread = reader.IsDBNull(reader.GetOrdinal("Thread")) ? null : reader.GetString(reader.GetOrdinal("Thread")),
                        Level = reader.IsDBNull(reader.GetOrdinal("Level")) ? null : reader.GetString(reader.GetOrdinal("Level")),
                        Logger = reader.IsDBNull(reader.GetOrdinal("Logger")) ? null : reader.GetString(reader.GetOrdinal("Logger")),
                        User = reader.IsDBNull(reader.GetOrdinal("User")) ? null : reader.GetString(reader.GetOrdinal("User")),
                        Message = reader.IsDBNull(reader.GetOrdinal("Message")) ? null : reader.GetString(reader.GetOrdinal("Message")),
                        Exception = reader.IsDBNull(reader.GetOrdinal("Exception")) ? null : reader.GetString(reader.GetOrdinal("Exception")),
                        MessageObject = reader.IsDBNull(reader.GetOrdinal("MessageObject")) ? null : reader.GetString(reader.GetOrdinal("MessageObject"))
                    });
                }

                _logger.LogInformation($"Retrieved {entries.Count} Log4Net entries from PostgreSQL");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Log4Net entries from PostgreSQL");
                throw;
            }

            return entries;
        }

        public async Task<bool> IsDatabaseAvailable()
        {
            _logger.LogInformation("Checking if Log4Net database is available");
            
            var settings = await _settingsService.LoadSettingsAsync();
            if (!settings.IsEnabled)
            {
                return false;
            }

            return await _settingsService.TestConnectionAsync(settings);
        }

        public async Task EnsureDatabaseExistsAsync()
        {
            _logger.LogInformation("Ensuring Log4Net database exists");
            
            var settings = await _settingsService.LoadSettingsAsync();
            if (!settings.IsEnabled)
            {
                _logger.LogInformation("PostgreSQL integration is disabled");
                return;
            }

            await _settingsService.CreateDatabaseIfNotExistsAsync(settings);
            await CreateTableIfNotExistsAsync(settings);
        }

        public async Task<bool> RestoreFromBackupAsync(string backupFilePath)
        {
            _logger.LogInformation("Restoring Log4Net database from backup: {BackupFile}", backupFilePath);

            var settings = await _settingsService.LoadSettingsAsync();
            if (!settings.IsEnabled)
            {
                _logger.LogInformation("PostgreSQL integration is disabled");
                return false;
            }

            try
            {
                // Ensure database exists
                await EnsureDatabaseExistsAsync();

                // Find pg_restore executable
                var pgRestorePath = FindPgRestoreExecutable();
                if (string.IsNullOrEmpty(pgRestorePath))
                {
                    _logger.LogError("pg_restore executable not found");
                    return false;
                }

                // Execute pg_restore
                var startInfo = new ProcessStartInfo
                {
                    FileName = pgRestorePath,
                    Arguments = $"-h {settings.Host} -p {settings.Port} -U {settings.Username} -d {settings.Database} -v \"{backupFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Set password via environment variable
                startInfo.EnvironmentVariables["PGPASSWORD"] = settings.Password;

                _logger.LogInformation($"Executing: {startInfo.FileName} {startInfo.Arguments}");

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    _logger.LogError("Failed to start pg_restore process");
                    return false;
                }

                await process.WaitForExitAsync();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation($"pg_restore completed successfully. Output: {output}");
                    return true;
                }
                else
                {
                    _logger.LogError($"pg_restore failed. Exit Code: {process.ExitCode}. Output: {output}. Error: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pg_restore operation");
                return false;
            }
        }

        private async Task CreateTableIfNotExistsAsync(PostgreSQLSettings settings)
        {
            try
            {
                using var connection = new NpgsqlConnection(settings.ConnectionString);
                await connection.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS log4net (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Date"" TIMESTAMP NOT NULL,
                        ""Host"" VARCHAR(255),
                        ""Site"" VARCHAR(255),
                        ""Thread"" VARCHAR(255),
                        ""Level"" VARCHAR(20),
                        ""Logger"" VARCHAR(255),
                        ""User"" VARCHAR(255),
                        ""Message"" TEXT,
                        ""Exception"" TEXT,
                        ""MessageObject"" TEXT
                    )", connection);

                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Log4Net table created or verified");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Log4Net table");
                throw;
            }
        }

        private string? FindPgRestoreExecutable()
        {
            var possiblePaths = new[]
            {
                "pg_restore", // In PATH
                @"C:\Program Files\PostgreSQL\16\bin\pg_restore.exe",
                @"C:\Program Files\PostgreSQL\15\bin\pg_restore.exe",
                @"C:\Program Files\PostgreSQL\14\bin\pg_restore.exe",
                @"C:\Program Files\PostgreSQL\13\bin\pg_restore.exe"
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    if (File.Exists(path) || (path == "pg_restore" && IsInPath(path)))
                    {
                        return path;
                    }
                }
                catch
                {
                    // Continue to next path
                }
            }

            return null;
        }

        private bool IsInPath(string fileName)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                return process != null;
            }
            catch
            {
                return false;
            }
        }
    }
}