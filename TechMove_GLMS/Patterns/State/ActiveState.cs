using TechMove_GLMS.Models;

namespace TechMove_GLMS.Patterns.State
{
    public class ActiveState : IContractState
    {
        public bool HandleServiceRequest(Contract context, ServiceRequest req)
        {
            //Active contracts allow service requests
            return true; 
        }
    }
}