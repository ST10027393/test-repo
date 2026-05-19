using Microsoft.AspNetCore.Http;
using TechMove_GLMS.Models;

namespace TechMove_GLMS.Services
{
    public interface IContractService
    {
        Task<IEnumerable<Contract>> GetFilteredContractsAsync(ContractFilterDto filter);
        Task<string?> ProcessAndSavePdfAsync(IFormFile? file);
        
        Task AddContractAsync(Contract contract);
        Task<Contract?> GetContractByIdAsync(int id);
        Task UpdateContractAssigneeAsync(Contract contract, string newAssignee);

        Task UpdateContractAsync(Contract contract, IFormFile? newAgreementFile);
        Task<bool> CanDeleteContractAsync(int contractId);
        Task DeleteContractAsync(int contractId);
    }
}