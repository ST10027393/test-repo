using Microsoft.EntityFrameworkCore;
using TechMove_GLMS.Data;
using TechMove_GLMS.Models;

namespace TechMove_GLMS.Services
{
    public class ClientService : IClientService
    {
        private readonly GlmsDbContext _context;

        public ClientService(GlmsDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Client>> GetFilteredClientsAsync(ClientFilterDto filter)
        {
            // LINQ query
            var query = _context.Clients.AsQueryable();

            // Row-level security: If not Admin, strictly lock to their own clients
            if (filter.CurrentUserRole != "Admin")
            {
                query = query.Where(c => c.AssignedTo == filter.CurrentUserName);
            }
            // Admin user filter (by assigned user)
            else if (!string.IsNullOrEmpty(filter.AssignedTo))
            {
                query = query.Where(c => c.AssignedTo == filter.AssignedTo);
            }

            // Dyanmic filtering based on provided criteria
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(c => c.Name.Contains(filter.SearchTerm) || c.ContactDetails.Contains(filter.SearchTerm));
            }

            if (!string.IsNullOrEmpty(filter.Region))
            {
                query = query.Where(c => c.Region == filter.Region);
            }

            if (filter.DateCreated.HasValue)
            {
                query = query.Where(c => c.CreatedAt.Date == filter.DateCreated.Value.Date);
            }

            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }

        public async Task AddClientAsync(Client client, string currentUserName)
        {
            // Auto-assign the creator
            client.AssignedTo = currentUserName;
            
            // Map the selected region to its currency automatically
            client.CurrencyCode = MapRegionToCurrency(client.Region);
            
            _context.Add(client);
            await _context.SaveChangesAsync();
        }

        public List<string> GetGlobalRegions()
        {
            return new List<string> { "South Africa", "United States", "European Union", "United Kingdom", "Asia Pacific", "Global" };
        }

        private string MapRegionToCurrency(string region)
        {
            return region switch
            {
                "South Africa" => "ZAR",
                "United States" => "USD",
                "European Union" => "EUR",
                "United Kingdom" => "GBP",
                "Asia Pacific" => "JPY",
                "Global" => "USD",
                _ => "USD" // Default fallback
            };
        }
    
        // Ftch single client
        public async Task<Client?> GetClientByIdAsync(int id)
        {
            // AsNoTracking() - EF Core optimization, for reading data.
            return await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientId == id);
        }

        // Process client update
        public async Task UpdateClientAsync(Client client)
        {
            // Automatically re-evaluate the currency in case of Region change
            client.CurrencyCode = MapRegionToCurrency(client.Region);
            
            _context.Clients.Update(client);
            await _context.SaveChangesAsync();
        }

        public async Task<Dictionary<string, string>> GetAvailableAssigneesAsync()
        {
            var users = await _context.Users.ToListAsync();

            return users
                .Select(u => new 
                { 
                    Name = (u.FirstName + u.Surname).Trim(), 
                    Email = u.Email.Trim() 
                })
                .DistinctBy(u => u.Name) 
                .OrderBy(u => u.Name)
                .ToDictionary(
                    u => u.Name,                      
                    u => $"{u.Name} ({u.Email})"     
                );
        }
    
        public async Task<bool> CanDeleteClientAsync(int clientId)
        {
            // Check contract status before deletion
            bool hasBlockingContracts = await _context.Contracts
                .AnyAsync(c => c.ClientId == clientId && (c.Status == "Active" || c.Status == "On Hold"));
            
            return !hasBlockingContracts;
        }

        public async Task DeleteClientAsync(int clientId)
        {
            var client = await _context.Clients.FindAsync(clientId);
            if (client != null)
            {
                _context.Clients.Remove(client);
                // As seen on sql script, the ON DELETE CASCADE constraint 
                //will automatically hunt down and destroy all related Contracts and Service Request
                //once changes are saved.
                await _context.SaveChangesAsync(); 
            }
        }
    }
}