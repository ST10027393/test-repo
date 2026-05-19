using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove_GLMS.Models;
using TechMove_GLMS.Services;
using TechMove_GLMS.Patterns.Factory;

namespace TechMove_GLMS.Controllers
{
    public class ServiceRequestsController : Controller
    {
        private readonly IServiceRequestService _requestService;
        private readonly IContractService _contractService;
        private readonly IServiceRequestFactory _factory;
        private readonly IClientService _clientService; // For the Reassign dropdown

        public ServiceRequestsController(IServiceRequestService requestService, IContractService contractService, IServiceRequestFactory factory, IClientService clientService)
        {
            _requestService = requestService;
            _contractService = contractService;
            _factory = factory;
            _clientService = clientService;
        }

        public async Task<IActionResult> Index(string search, string status, int? clientId)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");

            var filter = new ServiceRequestFilterDto
            {
                SearchTerm = search,
                Status = status,
                ClientId = clientId,
                CurrentUserRole = HttpContext.Session.GetString("userRole"),
                CurrentUserName = HttpContext.Session.GetString("userName")
            };

            var requests = await _requestService.GetFilteredRequestsAsync(filter);
            ViewBag.CurrentRole = filter.CurrentUserRole;
            return View(requests);
        }

        // Get - create
        public async Task<IActionResult> Create(int? contractId)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");
            
            // Allow them to select a contract if they didn't come from the Contracts page
            var contractsFilter = new ContractFilterDto { CurrentUserRole = HttpContext.Session.GetString("userRole"), 
            CurrentUserName = HttpContext.Session.GetString("userName") };
            var contracts = await _contractService.GetFilteredContractsAsync(contractsFilter);
            
            ViewBag.Contracts = new SelectList(contracts.Select(c => new { Id = c.ContractId, Text = $"#{c.ContractId} - {c.Client.Name}" }), 
            "Id", "Text", contractId);
            return View();
        }

        // Post - create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int ContractId, string Description, decimal ForeignCost, string ForeignCurrencyCode)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");

            try
            {
                string userName = HttpContext.Session.GetString("userName");

                // Build the object and hit the External Currency API (factory)
                var newRequest = await _factory.CreateRequestAsync(ContractId, Description, ForeignCost, ForeignCurrencyCode);
                newRequest.AssignedTo = userName; // Set the default owner

                // Fetch the parent Contract and ask if it accepts jobs (state)
                var contract = await _contractService.GetContractByIdAsync(ContractId);
                
                if (contract == null || !contract.CanAcceptServiceRequest(newRequest))
                {
                    ModelState.AddModelError("", "State Validation Failed: The selected SLA Contract is Expired, Draft, or On Hold.");
                    return await ReloadCreateView();
                }

                // Save to database
                await _requestService.AddRequestAsync(newRequest);

                TempData["SuccessMessage"] = "Freight job successfully validated by State Pattern and processed via API.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Api crash
                ModelState.AddModelError("", ex.Message);
                return await ReloadCreateView();
            }
        }

        // Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var request = await _requestService.GetRequestByIdAsync(id.Value);
            if (request == null) return NotFound();
            return View(request);
        }

        // Get - edit 
        public async Task<IActionResult> Edit(int? id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");
            if (id == null) return NotFound();

            var request = await _requestService.GetRequestByIdAsync(id.Value);
            if (request == null) return NotFound();

            string userRole = HttpContext.Session.GetString("userRole");
            if (userRole != "Admin" && request.AssignedTo != HttpContext.Session.GetString("userName")) return Forbid();

            ViewBag.CurrentRole = userRole;
            if (userRole == "Admin")
            {
                var assignees = await _clientService.GetAvailableAssigneesAsync();
                ViewBag.Assignees = new SelectList(assignees, "Key", "Value", request.AssignedTo);
            }

            return View(request);
        }

        // Post - edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RequestId,ContractId,Description,ForeignCost,ForeignCurrencyCode,LocalCostZar,Status,AssignedTo")] ServiceRequest request)
        {
            string userRole = HttpContext.Session.GetString("userRole");
            if (userRole != "Admin" && request.AssignedTo != HttpContext.Session.GetString("userName")) return Forbid();

            ModelState.Remove("Contract"); 

            if (ModelState.IsValid)
            {
                await _requestService.UpdateRequestAsync(request);
                TempData["SuccessMessage"] = "Service Request updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(request);
        }

        // Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var request = await _requestService.GetRequestByIdAsync(id.Value);
            if (request == null) return NotFound();
            return View(request);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _requestService.DeleteRequestAsync(id);
            TempData["SuccessMessage"] = "Service Request deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Helper for returning view on error
        private async Task<IActionResult> ReloadCreateView()
        {
            var filter = new ContractFilterDto { CurrentUserRole = HttpContext.Session.GetString("userRole"), CurrentUserName = HttpContext.Session.GetString("userName") };
            var contracts = await _contractService.GetFilteredContractsAsync(filter);
            ViewBag.Contracts = new SelectList(contracts.Select(c => new { Id = c.ContractId, Text = $"#{c.ContractId} - {c.Client.Name}" }), "Id", "Text");
            return View();
        }
    }
}