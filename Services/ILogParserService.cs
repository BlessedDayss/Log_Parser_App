using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogParserApp.Models;

namespace LogParserApp.Services
{
    /// <summary>
    /// Сервис для парсинга лог-файлов
    /// </summary>
    public interface ILogParserService
    {
        /// <summary>
        /// Выполняет парсинг лог-файла и возвращает коллекцию записей
        /// </summary>
        /// <param name="filePath">Путь к файлу лога</param>
        /// <returns>Коллекция записей лога</returns>
        Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath);
        
        /// <summary>
        /// Выполняет фильтрацию лог-записей по SQL-подобному запросу
        /// </summary>
        /// <param name="logEntries">Исходная коллекция записей</param>
        /// <param name="query">SQL-подобный запрос</param>
        /// <returns>Отфильтрованные записи</returns>
        Task<IEnumerable<LogEntry>> ExecuteQueryAsync(IEnumerable<LogEntry> logEntries, string query);
        
        /// <summary>
        /// Определяет формат лог-файла
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <returns>Определенный формат (Standard, Common, CSV, Unknown)</returns>
        Task<string> DetectLogFormatAsync(string filePath);
        
        /// <summary>
        /// Фильтрует записи и возвращает только записи с уровнем ERROR
        /// </summary>
        /// <param name="logEntries">Исходная коллекция записей</param>
        /// <returns>Отфильтрованные записи с уровнем ERROR</returns>
        Task<IEnumerable<LogEntry>> FilterErrorsAsync(IEnumerable<LogEntry> logEntries);
        
        /// <summary>
        /// Выполняет парсинг лог-файла пакетов (.log) и возвращает коллекцию записей
        /// </summary>
        /// <param name="filePath">Путь к файлу лога пакетов</param>
        /// <returns>Коллекция записей лога пакетов</returns>
        Task<IEnumerable<PackageLogEntry>> ParsePackageLogFileAsync(string filePath);
    }
} 