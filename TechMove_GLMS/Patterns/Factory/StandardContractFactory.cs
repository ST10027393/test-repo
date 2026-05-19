using TechMove_GLMS.Models;
using TechMove_GLMS.Patterns.State;

namespace TechMove_GLMS.Patterns.Factory
{
    public class StandardContractFactory : IContractFactory
    {
        public Contract CreateContract(int clientId, string assignedTo, DateOnly start, DateOnly end, string serviceLevel, string status, string? filePath)
        {
            var contract = new Contract
            {
                ClientId = clientId,
                AssignedTo = assignedTo,
                StartDate = start,
                EndDate = end,
                ServiceLevel = serviceLevel,
                Status = status,
                SignedAgreementFilePath = filePath
            };

            // If saved as a "Draft", enforce a mandatory 7-day review period 
            // pushing the Start Date forward.
            if (status == "Draft" && start <= DateOnly.FromDateTime(DateTime.Now))
            {
                contract.StartDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7));
            }

            if (status == "Active") contract.SetState(new ActiveState());
            else contract.SetState(new DraftState());

            return contract;
        }
    }
}