using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TechMove_GLMS.Controllers;
using TechMove_GLMS.Models;
using TechMove_GLMS.Data; 
using Xunit;

namespace TechMove_GLMS.Tests
{
    public class AuthControllerTests
    {
        // Helper method to simulate a web request session AND a fake database
        private AuthController CreateControllerWithSession(string? userRole)
        {
            // Mock the Database Context
            var mockContext = new Mock<GlmsDbContext>();
            
            // Create the controller, passing in the fake database context
            var controller = new AuthController(mockContext.Object);
            
            // Mock the Session and HttpContext
            var mockSession = new Mock<ISession>();
            
            byte[] roleBytes = userRole != null ? System.Text.Encoding.UTF8.GetBytes(userRole) : null;
            mockSession.Setup(s => s.TryGetValue("userRole", out roleBytes)).Returns(userRole != null);

            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(s => s.Session).Returns(mockSession.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = mockHttpContext.Object
            };

            return controller;
        }

        [Fact]
        public async Task Login_Post_InvalidModelState_ReturnsViewResult() 
        {
            // Arrange
            var mockContext = new Mock<GlmsDbContext>();
            var controller = new AuthController(mockContext.Object);
            
            controller.ModelState.AddModelError("Email", "Required");
            
            // Fix CS9035: Satisfy the 'required' modifier by passing an empty string
            var loginModel = new LoginModel { Email = "", Password = "password123" };

            // Act
            // Fix xUnit1031: Use 'await' instead of '.Result'
            var result = await controller.Login(loginModel); 

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(loginModel, viewResult.Model); 
        }

        [Fact]
        public void Register_Get_UnauthorizedUser_RedirectsToHome()
        {
            // Arrange
            var controller = CreateControllerWithSession(null);

            // Act
            var result = controller.Register();

            // Assert
            var redirectToActionResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectToActionResult.ActionName);
            Assert.Equal("Home", redirectToActionResult.ControllerName);
        }

        [Fact]
        public void Register_Get_AdminUser_ReturnsViewResult()
        {
            // Arrange
            var controller = CreateControllerWithSession("Admin");

            // Act
            var result = controller.Register();

            // Assert
            Assert.IsType<ViewResult>(result);
        }
    }
}