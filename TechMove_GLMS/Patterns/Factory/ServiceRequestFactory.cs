using TechMove_GLMS.Models;
using TechMove_GLMS.Services;

namespace TechMove_GLMS.Patterns.Factory
{
    public interface IServiceRequestFactory
    {
        Task<ServiceRequest> CreateRequestAsync(int contractId, string description, decimal foreignCost, string currencyCode);
    }

    public class ServiceRequestFactory : IServiceRequestFactory
    {
        private readonly ICurrencyService _currencyService;

        public ServiceRequestFactory(ICurrencyService currencyService)
        {
            _currencyService = currencyService;
        }

        public async Task<ServiceRequest> CreateRequestAsync(int contractId, string description, decimal foreignCost, string currencyCode)
        {
            // 1. Fetch live exchange rate and calculate
            decimal localCostZar = await _currencyService.ConvertToZarAsync(foreignCost, currencyCode);

            // 2. Instantiate and return the fully formed object
            return new ServiceRequest
            {
                ContractId = contractId,
                Description = description,
                ForeignCost = foreignCost,
                ForeignCurrencyCode = currencyCode.ToUpper(),
                LocalCostZar = localCostZar,
                Status = "Pending Integration" // Initial workflow status
            };
        }
    }
}