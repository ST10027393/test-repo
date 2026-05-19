using TechMove_GLMS.Models;

namespace TechMove_GLMS.Patterns.State
{
    public class DraftState : IContractState
    {
        public bool HandleServiceRequest(Contract context, ServiceRequest req)
        {
            //Draft contracts do not allow service requests
            return false; 
        }
    }
}