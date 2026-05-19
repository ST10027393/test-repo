using TechMove_GLMS.Models;

namespace TechMove_GLMS.Services
{
    public interface IServiceRequestService
    {
        Task<IEnumerable<ServiceRequest>> GetFilteredRequestsAsync(ServiceRequestFilterDto filter);
        Task<ServiceRequest?> GetRequestByIdAsync(int id);
        Task UpdateRequestAsync(ServiceRequest request);
        Task DeleteRequestAsync(int id);
        Task AddRequestAsync(ServiceRequest request);
    }
}