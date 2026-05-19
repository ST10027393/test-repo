using Microsoft.EntityFrameworkCore;
using TechMove_GLMS.Data;
using TechMove_GLMS.Models;

namespace TechMove_GLMS.Services
{
    public class ServiceRequestService : IServiceRequestService
    {
        private readonly GlmsDbContext _context;

        public ServiceRequestService(GlmsDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ServiceRequest>> GetFilteredRequestsAsync(ServiceRequestFilterDto filter)
        {
            var query = _context.ServiceRequests
                .Include(sr => sr.Contract)
                .ThenInclude(c => c.Client)
                .AsQueryable();

            // Managers only see their assigned Service Requests
            if (filter.CurrentUserRole != "Admin")
            {
                query = query.Where(sr => sr.AssignedTo == filter.CurrentUserName);
            }

            // Specific Contract
            if (filter.ContractId.HasValue)
            {
                query = query.Where(sr => sr.ContractId == filter.ContractId.Value);
            }

            // Specific Client
            if (filter.ClientId.HasValue)
            {
                query = query.Where(sr => sr.Contract.ClientId == filter.ClientId.Value);
            }

            // Dynamic Search Currency
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(sr => sr.Description.Contains(filter.SearchTerm) || sr.ForeignCurrencyCode.Contains(filter.SearchTerm));
            }

            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(sr => sr.Status == filter.Status);
            }

            return await query.OrderByDescending(sr => sr.RequestId).ToListAsync();
        }

        public async Task<ServiceRequest?> GetRequestByIdAsync(int id)
        {
            return await _context.ServiceRequests
                .Include(sr => sr.Contract)
                .ThenInclude(c => c.Client)
                .FirstOrDefaultAsync(sr => sr.RequestId == id);
        }

        public async Task UpdateRequestAsync(ServiceRequest request)
        {
            _context.ServiceRequests.Update(request);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRequestAsync(int id)
        {
            var request = await _context.ServiceRequests.FindAsync(id);
            if (request != null)
            {
                _context.ServiceRequests.Remove(request);
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddRequestAsync(ServiceRequest request)
        {
            _context.ServiceRequests.Add(request);
            await _context.SaveChangesAsync();
        }
    }
}