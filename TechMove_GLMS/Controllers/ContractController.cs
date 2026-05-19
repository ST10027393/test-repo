using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove_GLMS.Models;
using TechMove_GLMS.Services;
using TechMove_GLMS.Patterns.Factory;

namespace TechMove_GLMS.Controllers
{
    public class ContractsController : Controller
    {
        private readonly IContractService _contractService;
        private readonly IClientService _clientService;
        private readonly IContractFactory _contractFactory;

        public ContractsController(IContractService contractService, IClientService clientService, IContractFactory contractFactory)
        {
            _contractService = contractService;
            _clientService = clientService;
            _contractFactory = contractFactory;
        }

        //Get - All contracts with filters
        public async Task<IActionResult> Index(string search, string status, DateTime? start, DateTime? end, string assignedTo)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");

            var filter = new ContractFilterDto
            {
                SearchTerm = search,
                Status = status,
                StartDate = start,
                EndDate = end,
                AssignedTo = assignedTo,
                CurrentUserRole = HttpContext.Session.GetString("userRole"),
                CurrentUserName = HttpContext.Session.GetString("userName")
            };

            var contracts = await _contractService.GetFilteredContractsAsync(filter);
            ViewBag.CurrentRole = filter.CurrentUserRole;
            return View(contracts);
        }

        // GET - Contracts for a specific client
        public async Task<IActionResult> ClientContracts(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) 
            {
                return RedirectToAction("Login", "Auth");
            }

            var filter = new ContractFilterDto
            {
                ClientId = id,
                CurrentUserRole = HttpContext.Session.GetString("userRole"),
                CurrentUserName = HttpContext.Session.GetString("userName")
            };

            var contracts = await _contractService.GetFilteredContractsAsync(filter);
            ViewBag.CurrentRole = filter.CurrentUserRole;
            
            // Pass the ClientId to the view so the user can be routed back easily
            ViewBag.ClientId = id;
            return View(contracts);
        }

        // GET - Create contract
        public async Task<IActionResult> Create()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) 
            {
                return RedirectToAction("Login", "Auth");
            }

            // Only fetch clients this specific user is allowed to see
            var clientFilter = new ClientFilterDto 
            { 
                CurrentUserRole = HttpContext.Session.GetString("userRole"),
                CurrentUserName = HttpContext.Session.GetString("userName")
            };
            
            var availableClients = await _clientService.GetFilteredClientsAsync(clientFilter);

            // Dropdown text = "Company Name - Email" (uses Client ID as value in database)
            var clientSelectList = availableClients.Select(c => new 
            { 
                Id = c.ClientId, 
                DisplayText = $"{c.Name} - {c.ContactDetails}" 
            });

            ViewBag.Clients = new SelectList(clientSelectList, "Id", "DisplayText");
            return View();
        }

        // POST - Create contract
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int ClientId, DateOnly StartDate, DateOnly EndDate, 
        string ServiceLevel, string Status, IFormFile agreementFile)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) 
            {
                return RedirectToAction("Login", "Auth");
            }

            if (agreementFile == null || agreementFile.Length == 0)
            {
                ModelState.AddModelError("", "You must upload a document.");
                return await ReloadCreateViewWithErrors();
            }

            var extension = Path.GetExtension(agreementFile.FileName).ToLowerInvariant();
            if (extension != ".pdf" || agreementFile.ContentType != "application/pdf")
            {
                ModelState.AddModelError("", "SECURITY BLOCK: Only true PDF files are permitted.");
                return await ReloadCreateViewWithErrors();
            }

            string userName = HttpContext.Session.GetString("userName");

            // Process the File upload
            string? savedFilePath = await _contractService.ProcessAndSavePdfAsync(agreementFile);


            if (savedFilePath == null)
            {
                ModelState.AddModelError("", "A valid PDF SLA document is required.");
                return await ReloadCreateViewWithErrors();
            }

            //Instantiate the Contract via the Gang of Four Factory Method
            var newContract = _contractFactory.CreateContract(
                clientId: ClientId,
                assignedTo: userName,
                start: StartDate,
                end: EndDate,
                serviceLevel: ServiceLevel,
                status: Status,
                filePath: savedFilePath
            );

            // Save via the Service Layer
            await _contractService.AddContractAsync(newContract);

            TempData["SuccessMessage"] = "SLA Contract successfully generated and activated via Factory Pattern.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> ReloadCreateViewWithErrors()
        {
            var filter = new ClientFilterDto { CurrentUserRole = HttpContext.Session.GetString("userRole"), CurrentUserName = HttpContext.Session.GetString("userName") };
            var clients = await _clientService.GetFilteredClientsAsync(filter);
            ViewBag.Clients = new SelectList(clients.Select(c => new { Id = c.ClientId, DisplayText = $"{c.Name} - {c.ContactDetails}" }), "Id", "DisplayText");
            return View();
        }

        // GET: /Contracts/Reassign/5
        public async Task<IActionResult> Reassign(int? id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) 
            {
                return RedirectToAction("Login", "Auth");
            }

            // Only Admins can access reassignment page page
            if (HttpContext.Session.GetString("userRole") != "Admin") 
            {
                return Forbid();
            }

            if (id == null) 
            {
                return NotFound();
            }

            var contract = await _contractService.GetContractByIdAsync(id.Value);
            if (contract == null) 
            {
                return NotFound();
            }

            // Fetch all users for the dropdown
            var assignees = await _clientService.GetAvailableAssigneesAsync();
            ViewBag.Assignees = new SelectList(assignees, "Key", "Value", contract.AssignedTo);

            return View(contract);
        }

        // POST: /Contracts/Reassign/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reassign(int id, string AssignedTo)
        {
            if (HttpContext.Session.GetString("userRole") != "Admin") return Forbid();

            var contract = await _contractService.GetContractByIdAsync(id);
            if (contract == null) return NotFound();

            if (!string.IsNullOrEmpty(AssignedTo))
            {
                // This calls the service, triggering the Observer Pattern!
                await _contractService.UpdateContractAssigneeAsync(contract, AssignedTo);
                
                TempData["SuccessMessage"] = $"Contract #{contract.ContractId} reassigned to {AssignedTo}. Observer pattern triggered.";
                return RedirectToAction(nameof(Index));
            }

            var assignees = await _clientService.GetAvailableAssigneesAsync();
            ViewBag.Assignees = new SelectList(assignees, "Key", "Value", contract.AssignedTo);
            return View(contract);
        }
    
        // ==========================================
        // EDIT CONTRACT
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");
            if (id == null) return NotFound();

            var contract = await _contractService.GetContractByIdAsync(id.Value);
            if (contract == null) return NotFound();

            string userRole = HttpContext.Session.GetString("userRole");
            string userName = HttpContext.Session.GetString("userName");

            // SECURITY: Managers can only edit their own assigned SLAs
            if (userRole != "Admin" && contract.AssignedTo != userName)
            {
                TempData["ErrorMessage"] = "Unauthorized: You can only edit contracts assigned to your portfolio.";
                return RedirectToAction(nameof(Index));
            }

            return View(contract);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ContractId,ClientId,AssignedTo,StartDate,EndDate,ServiceLevel,Status,SignedAgreementFilePath")] Contract contract, IFormFile? newAgreementFile)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");
            if (id != contract.ContractId) return NotFound();

            string userRole = HttpContext.Session.GetString("userRole");
            string userName = HttpContext.Session.GetString("userName");

            // SECURITY RE-CHECK
            if (userRole != "Admin" && contract.AssignedTo != userName) return Forbid();

            // Ignore hidden/navigation properties for validation
            ModelState.Remove("Client");
            ModelState.Remove("ServiceRequests");
            ModelState.Remove("newAgreementFile");

            if (newAgreementFile != null)
            {
                var extension = Path.GetExtension(newAgreementFile.FileName).ToLowerInvariant();
                if (extension != ".pdf" || newAgreementFile.ContentType != "application/pdf")
                {
                    ModelState.AddModelError("", "SECURITY BLOCK: Only true PDF files are permitted.");
                    return View(contract);
                }
            }

            if (ModelState.IsValid)
            {
                await _contractService.UpdateContractAsync(contract, newAgreementFile);
                TempData["SuccessMessage"] = $"Contract #{contract.ContractId} successfully updated.";
                return RedirectToAction(nameof(Index));
            }
            
            return View(contract);
        }

        // Delete Contract
        public async Task<IActionResult> Delete(int? id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");
            if (id == null) return NotFound();

            var contract = await _contractService.GetContractByIdAsync(id.Value);
            if (contract == null) return NotFound();

            string userRole = HttpContext.Session.GetString("userRole");
            string userName = HttpContext.Session.GetString("userName");

            if (userRole != "Admin" && contract.AssignedTo != userName)
            {
                TempData["ErrorMessage"] = "Unauthorized: You can only delete contracts assigned to your portfolio.";
                return RedirectToAction(nameof(Index));
            }

            // Run the Business Rule Check
            ViewBag.CanDelete = await _contractService.CanDeleteContractAsync(id.Value);
            
            return View(contract);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");

            // Backend verification to prevent HTML form tampering
            bool canDelete = await _contractService.CanDeleteContractAsync(id);
            if (!canDelete)
            {
                TempData["ErrorMessage"] = "Delete Failed: This contract is active, on hold, or has ongoing service requests.";
                return RedirectToAction(nameof(Index));
            }

            await _contractService.DeleteContractAsync(id);
            TempData["SuccessMessage"] = "Contract permanently deleted.";
            return RedirectToAction(nameof(Index));
        }    
    }
}