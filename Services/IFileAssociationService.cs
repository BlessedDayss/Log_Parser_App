namespace Log_Parser_App.Services
{
    using System.Threading.Tasks;

    public interface IFileAssociationService
    {

        Task RegisterFileAssociationsAsync();
        Task<bool> AreFileAssociationsRegisteredAsync();
    }
}