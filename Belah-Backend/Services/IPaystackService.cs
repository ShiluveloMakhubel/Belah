using System.Threading.Tasks;
using Belah_Backend.DTOs;

namespace Belah_Backend.Services
{
    public interface IPaystackService
    {
        Task<PaystackInitializeData?> InitializeTransactionAsync(PaystackInitializeRequest request);
        Task<PaystackVerifyData?> VerifyTransactionAsync(string reference);
    }
}
