namespace Log_Parser_App.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Win32;


    public class WindowsFileAssociationService : IFileAssociationService
    {
        private readonly List<string> _supportedExtensions = [
            ".log", ".txt", ".log.txt", ".json"
        ];

        private const string ProgId = "LogParserApp";
        private const string FileTypeDescription = "Log File";
        private const string AppName = "Log Parser App";

        public async Task RegisterFileAssociationsAsync() {
            if (!OperatingSystem.IsWindows()) {
                return;
            }

            await Task.Run(() => {
                try {
                    string executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (string.IsNullOrEmpty(executablePath)) {
                        return;
                    }
                    string iconPath = GetIconPath(executablePath);
                    using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}")) {
                        key?.SetValue("", FileTypeDescription);
                        key?.SetValue("FriendlyTypeName", FileTypeDescription);
                        using (var iconKey = key?.CreateSubKey("DefaultIcon")) {
                            iconKey?.SetValue("", iconPath);
                        }
                        using (var commandKey = key?.CreateSubKey(@"shell\open\command")) {
                            commandKey?.SetValue("", $"\"{executablePath}\" \"%1\"");
                        }
                    }

                    foreach (string extension in _supportedExtensions) {
                        using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
                        using var openWithKey = extensionKey?.CreateSubKey("OpenWithProgids");
                        openWithKey?.SetValue(ProgId, new byte[0], RegistryValueKind.None);
                    }
                    string fileName = Path.GetFileName(executablePath);
                    using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{fileName}")) {
                        appKey?.SetValue("FriendlyAppName", AppName);

                        using (var openWithKey = appKey?.CreateSubKey("shell\\open\\command")) {
                            openWithKey?.SetValue("", $"\"{executablePath}\" \"%1\"");
                        }
                        using (var iconKey = appKey?.CreateSubKey("DefaultIcon")) {
                            iconKey?.SetValue("", iconPath);
                        }

                        // Регистрируем поддерживаемые типы файлов
                        using (var supportedTypesKey = appKey?.CreateSubKey("SupportedTypes")) {
                            foreach (string extension in _supportedExtensions) {
                                supportedTypesKey?.SetValue(extension, "");
                            }
                        }
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"Ошибка при регистрации ассоциаций файлов: {ex.Message}");
                }
            });
        }

        public async Task<bool> AreFileAssociationsRegisteredAsync() {
            if (!OperatingSystem.IsWindows()) {
                return false;
            }

            return await Task.Run(() => {
                try {
                    using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}");
                    return key != null;
                } catch {
                    return false;
                }
            });
        }

        private static string GetIconPath(string executablePath) {
            try {
                string exeDir = Path.GetDirectoryName(executablePath) ?? "";
                string assetsDir = Path.Combine(exeDir, "Assets");
                string[] possibleIcons = [
                    Path.Combine(assetsDir, "log-parser-icon.ico"),
                    Path.Combine(assetsDir, "logparserv1.ico"),
                    Path.Combine(assetsDir, "parser-logo.ico"),
                    Path.Combine(exeDir, "log-parser-icon.ico"),
                    Path.Combine(exeDir, "parser-logo.ico")
                ];

                foreach (string iconPath in possibleIcons) {
                    if (File.Exists(iconPath)) {
                        return iconPath;
                    }
                }
                return $"{executablePath},0";
            } catch {
                return $"{executablePath},0";
            }
        }
    }
}