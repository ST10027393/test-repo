using TechMove_GLMS.Models;

namespace TechMove_GLMS.Patterns.Factory
{
    public interface IContractFactory
    {
        Contract CreateContract(int clientId, string assignedTo, DateOnly start, DateOnly end, string serviceLevel, string status, string? filePath);
    }
}