using TechMove_GLMS.Models;

namespace TechMove_GLMS.Patterns.State
{
    public class ExpiredState : IContractState
    {
        public bool HandleServiceRequest(Contract context, ServiceRequest req)
        {
            //Expired contracts do not allow service requests
            return false; 
        }
    }
}