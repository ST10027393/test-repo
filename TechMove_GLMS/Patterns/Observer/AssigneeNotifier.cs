using System.Diagnostics;
using TechMove_GLMS.Models;

namespace TechMove_GLMS.Patterns.Observer
{
    public class AssigneeNotifier : IContractObserver
    {
        public void Update(Contract contract)
        {
            // In a fully developed SOA phase, this would trigger an Email Service or Push Notification.
            // For this monolithic MVP phase, we log the workflow event.
            Debug.WriteLine($"[SYSTEM NOTIFICATION] Contract {contract.ContractId} has been reassigned to {contract.AssignedTo}.");
        }
    }
}