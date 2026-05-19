using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using TechMove_GLMS.Controllers;
using TechMove_GLMS.Models;
using TechMove_GLMS.Services;
using TechMove_GLMS.Patterns.Factory;
using System.Text;

namespace GLMS.Tests.Controllers
{
    public class ContractsControllerTests
    {
        private (ContractsController Controller, Mock<IContractService> MockContractService, Mock<IClientService> MockClientService, Mock<IContractFactory> MockFactory) CreateController(string? userName, string? userRole)
        {
            var mockContractService = new Mock<IContractService>();
            var mockClientService = new Mock<IClientService>();
            var mockFactory = new Mock<IContractFactory>();

            var controller = new ContractsController(mockContractService.Object, mockClientService.Object, mockFactory.Object);

            var httpContext = new DefaultHttpContext();
            var sessionMock = new Mock<ISession>();

            byte[] roleBytes = Encoding.UTF8.GetBytes(userRole ?? "");
            byte[] nameBytes = Encoding.UTF8.GetBytes(userName ?? "");
            byte[] userBytes = Encoding.UTF8.GetBytes("loggedInUser");

            sessionMock.Setup(s => s.TryGetValue("userRole", out roleBytes)).Returns(true);
            sessionMock.Setup(s => s.TryGetValue("userName", out nameBytes)).Returns(true);
            sessionMock.Setup(s => s.TryGetValue("currentUser", out userBytes)).Returns(true);

            httpContext.Session = sessionMock.Object;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            return (controller, mockContractService, mockClientService, mockFactory);
        }

        // --- HELPER METHOD: MOCKS A PDF FILE UPLOAD ---
        private IFormFile CreateMockFile(string fileName, string contentType)
        {
            var fileMock = new Mock<IFormFile>();
            var content = "Fake File Content";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
            
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns(fileName);
            fileMock.Setup(_ => _.ContentType).Returns(contentType);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);
            
            return fileMock.Object;
        }

        // --- 1. TEST: CREATE (SUCCESSFUL FACTORY GENERATION) ---
        [Fact]
        public async Task Create_Post_ValidDataAndPdf_CallsFactoryAndSaves()
        {
            // Arrange
            var (controller, mockContractService, mockClientService, mockFactory) = CreateController("Genius Muzama", "Admin");
            var validPdf = CreateMockFile("SLA.pdf", "application/pdf");
            
            var startDate = new DateOnly(2026, 4, 1);
            var endDate = new DateOnly(2027, 4, 1);
            
            var fakeContract = new Contract { ContractId = 1, AssignedTo = "Genius Muzama" };

            mockContractService.Setup(s => s.ProcessAndSavePdfAsync(validPdf)).ReturnsAsync("/contracts/fake_path.pdf");
            
            mockFactory.Setup(f => f.CreateContract(99, "Genius Muzama", startDate, endDate, "1", "Active", "/contracts/fake_path.pdf"))
                       .Returns(fakeContract);

            // Act
            var result = await controller.Create(99, startDate, endDate, "1", "Active", validPdf);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            
            // Verify the Factory and the Save method were actually triggered
            mockFactory.Verify(f => f.CreateContract(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            mockContractService.Verify(s => s.AddContractAsync(fakeContract), Times.Once);
        }

        // --- 2. TEST: CREATE (SECURITY BLOCK FOR NON-PDF FILES) ---
        [Fact]
        public async Task Create_Post_MaliciousFile_BlocksUploadAndReturnsView()
        {
            // Arrange
            var (controller, mockContractService, mockClientService, mockFactory) = CreateController("LogisticsUser", "Logistics Manager");
            
            var maliciousFile = CreateMockFile("virus.exe", "application/x-msdownload");
            
            mockClientService.Setup(s => s.GetFilteredClientsAsync(It.IsAny<ClientFilterDto>())).ReturnsAsync(new List<Client>());

            // Act
            var result = await controller.Create(1, DateOnly.MinValue, DateOnly.MaxValue, "1", "Draft", maliciousFile);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid); // The controller should have flagged a model state error
            Assert.Equal("SECURITY BLOCK: Only true PDF files are permitted.", controller.ModelState[""].Errors[0].ErrorMessage);
            
            // Verify the system NEVER attempted to process the file or create a contract
            mockContractService.Verify(s => s.ProcessAndSavePdfAsync(It.IsAny<IFormFile>()), Times.Never);
            mockFactory.Verify(f => f.CreateContract(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // --- 3. TEST: EDIT (RBAC ENFORCEMENT) ---
        [Fact]
        public async Task Edit_Post_UnauthorizedManager_ReturnsForbidResult()
        {
            // Arrange: Logged in as Manager A
            var (controller, mockContractService, _, _) = CreateController("Manager A", "Logistics Manager");
            
            var hackedContractInput = new Contract { ContractId = 5, AssignedTo = "Manager B" };

            // Act
            var result = await controller.Edit(5, hackedContractInput, null);

            // Assert
            Assert.IsType<ForbidResult>(result); // Immediately kicks them out with a 403 Forbidden
            mockContractService.Verify(s => s.UpdateContractAsync(It.IsAny<Contract>(), It.IsAny<IFormFile>()), Times.Never);
        }

        // --- 4. TEST: DELETE (BUSINESS RULE ENFORCEMENT) ---
        [Fact]
        public async Task DeleteConfirmed_Post_ContractWithActiveServiceRequests_BlocksDeletion()
        {
            // Arrange
            var (controller, mockContractService, _, _) = CreateController("Genius Muzama", "Admin");
            
            // Setup the service to return FALSE (meaning the business rule block was triggered)
            mockContractService.Setup(s => s.CanDeleteContractAsync(12)).ReturnsAsync(false);

            // Act
            var result = await controller.DeleteConfirmed(12);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            
            // Verify the database delete was never called
            mockContractService.Verify(s => s.DeleteContractAsync(It.IsAny<int>()), Times.Never);
            Assert.Contains("Delete Failed", controller.TempData["ErrorMessage"]?.ToString() ?? "");
        }
    }
}