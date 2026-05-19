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
    public class ServiceRequestsControllerTests
    {
        private (ServiceRequestsController Controller, Mock<IServiceRequestService> MockReqService, Mock<IContractService> MockContractService, Mock<IServiceRequestFactory> MockFactory, Mock<IClientService> MockClientService) CreateController(string? userName, string? userRole)
        {
            var mockReqService = new Mock<IServiceRequestService>();
            var mockContractService = new Mock<IContractService>();
            var mockFactory = new Mock<IServiceRequestFactory>();
            var mockClientService = new Mock<IClientService>();

            var controller = new ServiceRequestsController(mockReqService.Object, mockContractService.Object, mockFactory.Object, mockClientService.Object);

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

            return (controller, mockReqService, mockContractService, mockFactory, mockClientService);
        }

        // --- 1. TEST: ADD (SUCCESS WITH GOF PATTERNS) ---
        [Fact]
        public async Task Create_Post_ValidDataAndActiveContract_SavesSuccessfully()
        {
            // Arrange
            var (controller, mockReqService, mockContractService, mockFactory, _) = CreateController("Manager A", "Logistics Manager");
            
            var generatedRequest = new ServiceRequest { RequestId = 1, ForeignCost = 100, LocalCostZar = 1800, AssignedTo = "Manager A" };
            mockFactory.Setup(f => f.CreateRequestAsync(99, "Test Cargo", 100, "USD")).ReturnsAsync(generatedRequest);

            var activeContract = new Contract { ContractId = 99, Status = "Active" };
            mockContractService.Setup(s => s.GetContractByIdAsync(99)).ReturnsAsync(activeContract);

            // Act
            var result = await controller.Create(99, "Test Cargo", 100, "USD");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            
            mockReqService.Verify(s => s.AddRequestAsync(generatedRequest), Times.Once);
        }

        // --- 2. TEST: ADD (STATE PATTERN REJECTION) ---
        [Fact]
        public async Task Create_Post_ExpiredContract_StatePatternBlocksSave()
        {
            // Arrange
            var (controller, mockReqService, mockContractService, mockFactory, _) = CreateController("Manager A", "Logistics Manager");
            
            var generatedRequest = new ServiceRequest { RequestId = 2 };
            mockFactory.Setup(f => f.CreateRequestAsync(50, "Test Cargo", 100, "USD")).ReturnsAsync(generatedRequest);

            var expiredContract = new Contract { ContractId = 50, Status = "Expired" };
            mockContractService.Setup(s => s.GetContractByIdAsync(50)).ReturnsAsync(expiredContract);

            mockContractService.Setup(s => s.GetFilteredContractsAsync(It.IsAny<ContractFilterDto>())).ReturnsAsync(new List<Contract>());

            // Act
            var result = await controller.Create(50, "Test Cargo", 100, "USD");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains("State Validation Failed", controller.ModelState[""].Errors[0].ErrorMessage);
            
            mockReqService.Verify(s => s.AddRequestAsync(It.IsAny<ServiceRequest>()), Times.Never);
        }

        // --- 3. TEST: EDIT (RBAC MANAGERS CANNOT EDIT OTHERS) ---
        [Fact]
        public async Task Edit_Get_ManagerTriesToAccessAnotherManagersRequest_ReturnsForbid()
        {
            // Arrange
            var (controller, mockReqService, _, _, _) = CreateController("Manager A", "Logistics Manager");
            
            var foreignRequest = new ServiceRequest { RequestId = 10, AssignedTo = "Manager B" };
            mockReqService.Setup(s => s.GetRequestByIdAsync(10)).ReturnsAsync(foreignRequest);

            // Act
            var result = await controller.Edit(10);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        // --- 4. TEST: EDIT (RBAC ADMINS HAVE FULL ACCESS) ---
        [Fact]
        public async Task Edit_Get_AdminAccessesAnotherManagersRequest_ReturnsView()
        {
            // Arrange: Logged in as Admin
            var (controller, mockReqService, _, _, mockClientService) = CreateController("Admin User", "Admin");
            
            var foreignRequest = new ServiceRequest { RequestId = 10, AssignedTo = "Manager B" };
            mockReqService.Setup(s => s.GetRequestByIdAsync(10)).ReturnsAsync(foreignRequest);
            
            mockClientService.Setup(s => s.GetAvailableAssigneesAsync()).ReturnsAsync(new Dictionary<string, string>());

            // Act
            var result = await controller.Edit(10);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(foreignRequest, viewResult.Model);
        }

        // --- 5. TEST: DELETE (SUCCESSFUL DELETION) ---
        [Fact]
        public async Task DeleteConfirmed_Post_ValidId_DeletesAndRedirects()
        {
            // Arrange
            var (controller, mockReqService, _, _, _) = CreateController("Admin User", "Admin");

            // Act
            var result = await controller.DeleteConfirmed(55);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            
            mockReqService.Verify(s => s.DeleteRequestAsync(55), Times.Once);
        }

        // --- 6. TEST: VIEW (RBAC DTO VERIFICATION) ---
        [Fact]
        public async Task Index_Get_PassesCorrectSessionDataToFilterDto()
        {
            // Arrange
            var (controller, mockReqService, _, _, _) = CreateController("Manager A", "Logistics Manager");
            
            ServiceRequestFilterDto capturedDto = null;
            mockReqService.Setup(s => s.GetFilteredRequestsAsync(It.IsAny<ServiceRequestFilterDto>()))
                          .Callback<ServiceRequestFilterDto>(dto => capturedDto = dto)
                          .ReturnsAsync(new List<ServiceRequest>());

            // Act
            await controller.Index("Search", "Pending", null);

            // Assert
            Assert.NotNull(capturedDto);
            Assert.Equal("Logistics Manager", capturedDto.CurrentUserRole);
            Assert.Equal("Manager A", capturedDto.CurrentUserName);
            Assert.Equal("Search", capturedDto.SearchTerm);
            Assert.Equal("Pending", capturedDto.Status);
        }
    }
}