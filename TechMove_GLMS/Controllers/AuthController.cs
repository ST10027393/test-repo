// FILE: TechMove_GLMS/Controllers/AuthController.cs
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TechMove_GLMS.Models;
using Firebase.Auth; 
using Newtonsoft.Json; 
using TechMove_GLMS.Data;

namespace TechMove_GLMS.Controllers;

public class AuthController : Controller
{
    private readonly FirebaseAuthProvider auth;
    private readonly GlmsDbContext _context;

    public AuthController(GlmsDbContext context)
    {
        _context = context; 
        
        auth = new FirebaseAuthProvider(new FirebaseConfig(Environment.GetEnvironmentVariable("GLMSAuthApp")));
    }

    // --- LOGIN LOGIC ---
    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginModel login)
    {
        if (!ModelState.IsValid) return View(login);

        try
        {
            var fbAuthLink = await auth.SignInWithEmailAndPasswordAsync(login.Email, login.Password);
            string uid = fbAuthLink.User.LocalId;

            // Look up the user in  SQL database to get their Role
            var localUser = _context.Users.FirstOrDefault(u => u.FirebaseUid == uid);

            if (localUser != null)
            {
                // Store identity and role in the session
                HttpContext.Session.SetString("currentUser", uid);
                HttpContext.Session.SetString("userRole", localUser.Role);
                HttpContext.Session.SetString("userName", (localUser.FirstName+localUser.Surname).Trim());

                return RedirectToAction("Index", "Home");
            }
            
            ModelState.AddModelError(string.Empty, "User record not found in the local system.");
        }
        catch (FirebaseAuthException ex)
        {
            var firebaseEx = JsonConvert.DeserializeObject<FirebaseErrorModel>(ex.ResponseData);
            ModelState.AddModelError(string.Empty, firebaseEx.error.message);
        }

        return View(login);
    }

    // --- ADMIN-ONLY REGISTRATION LOGIC ---
    [HttpGet]
    public IActionResult Register()
    {
        // Kick out anyone who isn't an Admin
        if (HttpContext.Session.GetString("userRole") != "Admin")
        {
            return RedirectToAction("Index", "Home"); 
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterModel reg)
    {
        if (HttpContext.Session.GetString("userRole") != "Admin")
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid) return View(reg);

        try
        {
            // Create the user in Firebase
            var fbAuthLink = await auth.CreateUserWithEmailAndPasswordAsync(reg.Email, reg.Password);
            string newUid = fbAuthLink.User.LocalId;

            // Save the user's role and details in the local SQL database
            var newUser = new Models.User
            {
                FirebaseUid = newUid,
                Email = reg.Email,
                FirstName = reg.FirstName,
                Surname = reg.Surname,
                Role = reg.Role,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Redirect back to potential user list page or home after successful creation
            //To do: Implement observer pattern to notify Admins of new user registration
            TempData["SuccessMessage"] = $"User {reg.FirstName} {reg.Surname} successfully registered as {reg.Role}.";
            return RedirectToAction("Index", "Home"); 
        }
        catch (FirebaseAuthException ex)
        {
            var firebaseEx = JsonConvert.DeserializeObject<FirebaseErrorModel>(ex.ResponseData);
            ModelState.AddModelError(string.Empty, firebaseEx.error.message);
        }

        return View(reg);
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}
