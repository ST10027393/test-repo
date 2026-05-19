using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TechMove_GLMS.Models;
using TechMove_GLMS.Services;

namespace TechMove_GLMS.Controllers
{
    public class ClientsController : Controller
    {
        private readonly IClientService _clientService;

        public ClientsController(IClientService clientService)
        {
            _clientService = clientService;
        }

        // GET: /Clients/ 
        public async Task<IActionResult> Index(string search, string region, DateTime? date, string assignedTo)
        {
            // Session management, block unauthenticated users
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser")))
            {
                return RedirectToAction("Login", "Auth");
            }

            // Filter DTO using input form
            var filter = new ClientFilterDto
            {
                SearchTerm = search,
                Region = region,
                DateCreated = date,
                AssignedTo = assignedTo,
                CurrentUserRole = HttpContext.Session.GetString("userRole"),
                CurrentUserName = HttpContext.Session.GetString("userName")
            };

            var clients = await _clientService.GetFilteredClientsAsync(filter);
            
            ViewBag.Regions = new SelectList(_clientService.GetGlobalRegions());
            ViewBag.CurrentRole = filter.CurrentUserRole; // Pass to view to hide/show Admin columns
            
            return View(clients);
        }

        public IActionResult Create()
        {
            // Session management, block unauthenticated users
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser")))
            {
                return RedirectToAction("Login", "Auth");
            }

            ViewBag.Regions = new SelectList(_clientService.GetGlobalRegions());
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,ContactDetails,Region")] Client client)
        {
            // Session management, block unauthenticated users
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser")))
            {
                return RedirectToAction("Login", "Auth");
            }

            // Auto assigned fields
            ModelState.Remove("CurrencyCode");
            ModelState.Remove("AssignedTo");
            ModelState.Remove("Contracts"); 
            // Validation check on form fields
            if (ModelState.IsValid)
            {
                string creatorName = HttpContext.Session.GetString("userName") ?? "System Admin";
                await _clientService.AddClientAsync(client, creatorName);
                TempData["SuccessMessage"] = $"Client created and assigned to {creatorName}.";
                return RedirectToAction(nameof(Index));
            }
            // Repopulation upon failure
            ViewBag.Regions = new SelectList(_clientService.GetGlobalRegions());
            return View(client);
        }
    
        // GET: /Clients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) 
            {
                return RedirectToAction("Login", "Auth");
            }
            
            if (id == null) 
            {
                return NotFound();
            }

            var client = await _clientService.GetClientByIdAsync(id.Value);
            if (client == null) 
            {
                return NotFound();
            }

            string userRole = HttpContext.Session.GetString("userRole");
            string userName = HttpContext.Session.GetString("userName");

            // Security - Logistics Managers can only edit their own clients.
            if (userRole != "Admin" && client.AssignedTo != userName)
            {
                TempData["ErrorMessage"] = "Unauthorized: You can only edit clients assigned to your portfolio.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Regions = new SelectList(_clientService.GetGlobalRegions(), client.Region);
            ViewBag.CurrentRole = userRole;

            if (userRole == "Admin")
            {
                var assignees = await _clientService.GetAvailableAssigneesAsync();
                
                // "Key" = Username (saves to DB), "Value" = Name + Email (shows in UI)
                ViewBag.Assignees = new SelectList(assignees, "Key", "Value", client.AssignedTo);
            }
            
            return View(client);
        }

        // POST: /Clients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ClientId,Name,ContactDetails,Region,AssignedTo,CreatedAt")] Client client)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");
            if (id != client.ClientId) return NotFound();

            string userRole = HttpContext.Session.GetString("userRole");
            
            // Ignore hidden fields for validation
            ModelState.Remove("CurrencyCode");
            ModelState.Remove("Contracts");

            if (ModelState.IsValid)
            {
                try
                {
                    // 3. TAMPER PREVENTION: If they aren't an admin, force the AssignedTo field 
                    // back to its original database value, just in case they hacked the HTML form.
                    if (userRole != "Admin")
                    {
                        var originalClient = await _clientService.GetClientByIdAsync(id);
                        client.AssignedTo = originalClient.AssignedTo;
                    }

                    await _clientService.UpdateClientAsync(client);
                    TempData["SuccessMessage"] = $"Client {client.Name} successfully updated.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (await _clientService.GetClientByIdAsync(client.ClientId) == null) return NotFound();
                    else throw;
                }
            }
            
            ViewBag.Regions = new SelectList(_clientService.GetGlobalRegions(), client.Region);
            ViewBag.CurrentRole = userRole;
            if (userRole == "Admin")
            {
                var assignees = await _clientService.GetAvailableAssigneesAsync();
                ViewBag.Assignees = new SelectList(assignees, "Key", "Value", client.AssignedTo);
            }
            return View(client);
        }

        // GET: /Clients/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) 
            {
                return RedirectToAction("Login", "Auth");
            }
            if (id == null) 
            {
                return NotFound();
            }

            var client = await _clientService.GetClientByIdAsync(id.Value);
            if (client == null) 
            {
                return NotFound();
            }

            string userRole = HttpContext.Session.GetString("userRole");
            string userName = HttpContext.Session.GetString("userName");

            // Managers can only delete their own assigned clients
            if (userRole != "Admin" && client.AssignedTo != userName)
            {
                TempData["ErrorMessage"] = "Unauthorized: You can only delete clients assigned to your portfolio.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CanDelete = await _clientService.CanDeleteClientAsync(id.Value);
            
            return View(client);
        }

        // POST: /Clients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) return RedirectToAction("Login", "Auth");

            // Re-verify the business rule on the backend 
            bool canDelete = await _clientService.CanDeleteClientAsync(id);
            if (!canDelete)
            {
                TempData["ErrorMessage"] = "Delete Failed: This client currently has Active or On Hold contracts.";
                return RedirectToAction(nameof(Index));
            }

            await _clientService.DeleteClientAsync(id);
            
            TempData["SuccessMessage"] = "Client and all associated historical data were permanently deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}