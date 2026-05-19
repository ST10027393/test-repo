using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TechMove_GLMS.Data;
using TechMove_GLMS.Models;

namespace TechMove_GLMS.Services
{
    public class ContractService : IContractService
    {
        private readonly GlmsDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        // Dependency Injection
        public ContractService(GlmsDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IEnumerable<Contract>> GetFilteredContractsAsync(ContractFilterDto filter)
        {
            await AutoExpireContractsAsync();

            // Data aggregation with related Client data for display purposes
            var query = _context.Contracts.Include(c => c.Client).AsQueryable();

            // RBAC - lock non-admins to their own contracts
            if (filter.CurrentUserRole != "Admin")
            {
                query = query.Where(c => c.AssignedTo == filter.CurrentUserName);
            }

            // Logic for  "View Contracts" button on the Client Directory
            if (filter.ClientId.HasValue)
            {
                query = query.Where(c => c.ClientId == filter.ClientId.Value);
            }

            // Dynamic filtering based on user input
            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(c => c.Status == filter.Status);
            }

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                // Searches the joined Client table for matching company names
                query = query.Where(c => c.Client.Name.Contains(filter.SearchTerm));
            }

            if (!string.IsNullOrEmpty(filter.AssignedTo))
            {
                //Filter by specific Account Manager 
                query = query.Where(c => c.AssignedTo == filter.AssignedTo);
            }

            if (filter.StartDate.HasValue)
            {
                //Filter by Start Date
                var filterStart = DateOnly.FromDateTime(filter.StartDate.Value);
                query = query.Where(c => c.StartDate >= filterStart);
            }

            if (filter.EndDate.HasValue)
            {
                //Filter by End Date
                var filterEnd = DateOnly.FromDateTime(filter.EndDate.Value);
                query = query.Where(c => c.EndDate <= filterEnd);
            }

            return await query.OrderByDescending(c => c.StartDate).ToListAsync();
        }

        public async Task<string?> ProcessAndSavePdfAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            // Secure server path - wwwroot/contracts
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "contracts");
            
            // Create the directory if it doesn't exist yet
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // GUID to prevent filename collisions
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName) ;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Return the relative path to save into the SQL Database
            return "/contracts/" + uniqueFileName;
        }

        public async Task AddContractAsync(Contract contract)
        {
            _context.Add(contract);
            await _context.SaveChangesAsync();
        }

        public async Task<Contract?> GetContractByIdAsync(int id)
        {
            await AutoExpireContractsAsync();
            
            // Include Client to show the company name on reassignment screen
            return await _context.Contracts.Include(c => c.Client).FirstOrDefaultAsync(c => c.ContractId == id);
        }

        public async Task UpdateContractAssigneeAsync(Contract contract, string newAssignee)
        {
            // Attach observer
            contract.AttachObserver(new Patterns.Observer.AssigneeNotifier());

            // Trigger observer logic
            contract.ChangeAssignee(newAssignee);

            // Save to db
            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateContractAsync(Contract contract, IFormFile? newAgreementFile)
        {
            // Overwrite old pdf file path
            if (newAgreementFile != null && newAgreementFile.Length > 0)
            {
                string? newFilePath = await ProcessAndSavePdfAsync(newAgreementFile);
                if (newFilePath != null)
                {
                    contract.SignedAgreementFilePath = newFilePath;
                }
            }

            // re-attach observer
            contract.AttachObserver(new TechMove_GLMS.Patterns.Observer.AssigneeNotifier());

            // Trigger the GoF Observer Pattern 
            contract.ChangeStatus(contract.Status); 

            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CanDeleteContractAsync(int contractId)
        {
            // Load the ServiceRequests to check business rule
            var contract = await _context.Contracts
                .Include(c => c.ServiceRequests)
                .FirstOrDefaultAsync(c => c.ContractId == contractId);

            if (contract == null) return false;

            // Business rule - Cannot delete an On Hold or Active contract
            if (contract.Status == "Active" || contract.Status == "On Hold") return false;

            // Business rule 2 - Cannot delete a contract that has associated Service Requests
            if (contract.ServiceRequests.Any()) return false;

            return true;
        }

        public async Task DeleteContractAsync(int contractId)
        {
            var contract = await _context.Contracts.FindAsync(contractId);
            if (contract != null)
            {
                _context.Contracts.Remove(contract);
                await _context.SaveChangesAsync();
            }
        }

        // Method to check if contract is expired
        private async Task AutoExpireContractsAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            
            // End date elapsed but contract status not expired
            var expiredContracts = await _context.Contracts
                .Where(c => c.EndDate < today && (c.Status != "Expired" || c.Status == "Draft"))
                .ToListAsync();

            if (expiredContracts.Any())
            {
                foreach (var contract in expiredContracts)
                {
                    // Changes contract state
                    contract.ChangeStatus("Expired"); 
                }
                
                _context.Contracts.UpdateRange(expiredContracts);
                await _context.SaveChangesAsync();
            }
        }
    }
}