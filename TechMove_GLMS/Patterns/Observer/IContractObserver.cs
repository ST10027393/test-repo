using TechMove_GLMS.Models;

namespace TechMove_GLMS.Patterns.Observer
{
    public interface IContractObserver
    {
        void Update(Contract contract);
    }
}