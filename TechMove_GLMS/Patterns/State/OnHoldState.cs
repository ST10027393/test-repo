using TechMove_GLMS.Models;

namespace TechMove_GLMS.Patterns.State
{
    public class OnHoldState : IContractState
    {
        public bool HandleServiceRequest(Contract context, ServiceRequest req)
        {
            //OnHold contracts do not allow service requests
            return false; 
        }
    }
}