namespace Log_Parser_App.Services
{
	using System.Threading.Tasks;

	#region Interface: IFileAssociationService

	public interface IFileAssociationService
	{

		#region Methods: Public

		Task RegisterFileAssociationsAsync();

		Task<bool> AreFileAssociationsRegisteredAsync();

		#endregion

	}

	#endregion

}
