using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Log_Parser_App.Services;

public class WindowsFileAssociationService : IFileAssociationService
{
    private readonly List<string> _supportedExtensions = new() { ".log", ".txt", ".log.txt", ".json" };
    private const string ProgId = "LogParserApp";
    private const string FileTypeDescription = "Log File";
    private const string AppName = "Log Parser App";

    public async Task RegisterFileAssociationsAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Работает только в Windows
        }

        await Task.Run(() =>
        {
            try
            {
                string executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(executablePath))
                {
                    return;
                }

                // Определяем путь к иконке
                string iconPath = GetIconPath(executablePath);

                // Регистрируем ProgID
                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
                {
                    key?.SetValue("", FileTypeDescription);
                    key?.SetValue("FriendlyTypeName", FileTypeDescription);
                    
                    // Иконка приложения
                    using (var iconKey = key?.CreateSubKey("DefaultIcon"))
                    {
                        iconKey?.SetValue("", iconPath);
                    }

                    // Команда открытия
                    using (var commandKey = key?.CreateSubKey(@"shell\open\command"))
                    {
                        commandKey?.SetValue("", $"\"{executablePath}\" \"%1\"");
                    }
                }

                // Регистрируем расширения файлов
                foreach (var extension in _supportedExtensions)
                {
                    using (var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}"))
                    {
                        // Добавляем в OpenWithProgIds
                        using (var openWithKey = extensionKey?.CreateSubKey("OpenWithProgids"))
                        {
                            openWithKey?.SetValue(ProgId, new byte[0], RegistryValueKind.None);
                        }
                    }
                }

                // Регистрируем приложение в списке приложений для открытия файлов
                string fileName = Path.GetFileName(executablePath);
                using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{fileName}"))
                {
                    appKey?.SetValue("FriendlyAppName", AppName);
                    
                    using (var openWithKey = appKey?.CreateSubKey("shell\\open\\command"))
                    {
                        openWithKey?.SetValue("", $"\"{executablePath}\" \"%1\"");
                    }

                    // Добавляем иконку
                    using (var iconKey = appKey?.CreateSubKey("DefaultIcon"))
                    {
                        iconKey?.SetValue("", iconPath);
                    }

                    // Регистрируем поддерживаемые типы файлов
                    using (var supportedTypesKey = appKey?.CreateSubKey("SupportedTypes"))
                    {
                        foreach (var extension in _supportedExtensions)
                        {
                            supportedTypesKey?.SetValue(extension, "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при регистрации ассоциаций файлов: {ex.Message}");
            }
        });
    }

    public async Task<bool> AreFileAssociationsRegisteredAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                // Проверяем регистрацию ProgID
                using (var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}"))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        });
    }

    private string GetIconPath(string executablePath)
    {
        try
        {
            // Попытка найти иконки
            string exeDir = Path.GetDirectoryName(executablePath) ?? "";
            string assetsDir = Path.Combine(exeDir, "Assets");
            
            // Возможные пути к иконкам
            string[] possibleIcons = new[] {
                Path.Combine(assetsDir, "log-parser-icon.ico"),
                Path.Combine(assetsDir, "logparserv1.ico"),
                Path.Combine(assetsDir, "parser-logo.ico"),
                Path.Combine(exeDir, "log-parser-icon.ico"),
                Path.Combine(exeDir, "parser-logo.ico")
            };

            foreach (var iconPath in possibleIcons)
            {
                if (File.Exists(iconPath))
                {
                    return iconPath;
                }
            }

            // Если иконка не найдена, используем сам исполняемый файл
            return $"{executablePath},0";
        }
        catch
        {
            // В случае ошибки используем исполняемый файл
            return $"{executablePath},0";
        }
    }
}