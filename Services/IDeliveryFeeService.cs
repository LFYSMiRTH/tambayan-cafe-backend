using System.Threading.Tasks;

namespace TambayanCafeAPI.Services
{
    public interface IDeliveryFeeService
    {
        Task<decimal> CalculateDeliveryFeeAsync(string fullAddress);
    }
}