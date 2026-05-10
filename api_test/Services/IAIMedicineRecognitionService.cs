using api_test.Models;

namespace api_test.Services
{
    public interface IAIMedicineRecognitionService
    {
        Task<MedicineScanResponseDto> ScanMedicineImageAsync(IFormFile file);
    }
}