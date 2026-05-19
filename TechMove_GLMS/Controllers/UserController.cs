using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMove_GLMS.Data;

namespace TechMove_GLMS.Controllers
{
    public class UsersController : Controller
    {
        private readonly GlmsDbContext _context;

        public UsersController(GlmsDbContext context)
        {
            _context = context;
        }

        // GET: /Users/
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("userRole") != "Admin")
            {
                return RedirectToAction("Login", "Auth");
            }

            // Fetch all users from the SQL database
            var users = await _context.Users.OrderBy(u => u.FirstName).ToListAsync();
            
            return View(users);
        }
    }
}