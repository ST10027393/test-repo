using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using TechMove_GLMS.Controllers;
using TechMove_GLMS.Models;
using TechMove_GLMS.Services;
using System.Text;

namespace GLMS.Tests.Controllers
{
    public class ClientsControllerTests
    {
        // --- HELPER METHOD ---
        private (ClientsController Controller, Mock<IClientService> MockService) CreateController(string userName, string userRole)
        {
            var mockService = new Mock<IClientService>();
            var controller = new ClientsController(mockService.Object);

            var httpContext = new DefaultHttpContext();
            var sessionMock = new Mock<ISession>();

            byte[] roleBytes = Encoding.UTF8.GetBytes(userRole ?? "");
            byte[] nameBytes = Encoding.UTF8.GetBytes(userName ?? "");
            byte[] userBytes = Encoding.UTF8.GetBytes("loggedInUser");

            // Setup the mock to return fake session data when HttpContext.Session.GetString() is called
            sessionMock.Setup(s => s.TryGetValue("userRole", out roleBytes)).Returns(true);
            sessionMock.Setup(s => s.TryGetValue("userName", out nameBytes)).Returns(true);
            sessionMock.Setup(s => s.TryGetValue("currentUser", out userBytes)).Returns(true);

            httpContext.Session = sessionMock.Object;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Mock TempData to assert success/error messages
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            return (controller, mockService);
        }

        // --- 1. TEST: CLIENT CREATION ---
        [Fact]
        public async Task Create_Post_ValidClient_SavesAndRedirectsToIndex()
        {
            // Arrange
            var (controller, mockService) = CreateController("Genius Muzama", "Admin");
            var newClient = new Client { Name = "Acme Corp", ContactDetails = "acme@test.com", Region = "United States" };

            // Act
            var result = await controller.Create(newClient);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            
            mockService.Verify(s => s.AddClientAsync(newClient, "Genius Muzama"), Times.Once);
            
            Assert.Contains("Client created and assigned to", controller.TempData["SuccessMessage"]?.ToString() ?? "");
        }

        // --- 2. TEST: EDIT (TAMPER PREVENTION) ---
        [Fact]
        public async Task Edit_Post_LogisticsManager_RestoresOriginalAssignee()
        {
            // Arrange
            var (controller, mockService) = CreateController("LogisticsUser", "Logistics Manager");
            
            var originalClient = new Client { ClientId = 1, Name = "Test", AssignedTo = "LogisticsUser" };
            mockService.Setup(s => s.GetClientByIdAsync(1)).ReturnsAsync(originalClient);

            var hackedClientInput = new Client { ClientId = 1, Name = "Test Changed", AssignedTo = "HackerAdmin" };

            // Act
            var result = await controller.Edit(1, hackedClientInput);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            
            Assert.Equal("LogisticsUser", hackedClientInput.AssignedTo);
            mockService.Verify(s => s.UpdateClientAsync(hackedClientInput), Times.Once);
        }

        // --- 3. TEST: DELETE (BUSINESS RULE ENFORCEMENT) ---
        [Fact]
        public async Task DeleteConfirmed_Post_ClientWithActiveContracts_BlocksDeletion()
        {
            // Arrange
            var (controller, mockService) = CreateController("Genius Muzama", "Admin");
            
            mockService.Setup(s => s.CanDeleteClientAsync(99)).ReturnsAsync(false);

            // Act
            var result = await controller.DeleteConfirmed(99);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            
            Assert.Contains("Delete Failed", controller.TempData["ErrorMessage"]?.ToString());
            
            mockService.Verify(s => s.DeleteClientAsync(It.IsAny<int>()), Times.Never);
        }
    }
}