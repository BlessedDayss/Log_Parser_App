using System.Threading.Tasks;

namespace Log_Parser_App.Services;

public interface IFileAssociationService
{
    /// <summary>
    /// Регистрирует ассоциации файлов для текущего приложения
    /// </summary>
    Task RegisterFileAssociationsAsync();
    
    /// <summary>
    /// Проверяет, зарегистрированы ли ассоциации файлов
    /// </summary>
    Task<bool> AreFileAssociationsRegisteredAsync();
}