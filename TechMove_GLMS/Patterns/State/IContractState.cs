using TechMove_GLMS.Models;

namespace TechMove_GLMS.Patterns.State
{
    public interface IContractState
    {
        // Validates if a service request can be raised in this state
        bool HandleServiceRequest(Contract context, ServiceRequest req);
    }
}