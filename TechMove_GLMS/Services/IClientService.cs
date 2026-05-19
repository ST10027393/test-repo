using TechMove_GLMS.Models;

namespace TechMove_GLMS.Services
{
    public interface IClientService
    {
        Task<IEnumerable<Client>> GetFilteredClientsAsync(ClientFilterDto filter);
        Task AddClientAsync(Client client, string currentUserName);
        List<string> GetGlobalRegions();
        //Edit client functionality
        Task<Client?> GetClientByIdAsync(int id);
        Task UpdateClientAsync(Client client); 
        Task<Dictionary<string, string>> GetAvailableAssigneesAsync();

        //Delete client functionality
        Task<bool> CanDeleteClientAsync(int clientId);
        Task DeleteClientAsync(int clientId);
    }
}