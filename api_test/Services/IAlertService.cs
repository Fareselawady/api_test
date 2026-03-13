using api_test.Models;

namespace api_test.Services
{
    public interface IAlertService
    {
        Task<List<AlertDto>> GetAllAlertsAsync(int userId);

        /// <summary>الإشعارات الغير مقروءة بس</summary>
        Task<List<AlertDto>> GetUnreadAlertsAsync(int userId);

        /// <summary>عدد الإشعارات الغير مقروءة (للـ badge)</summary>
        Task<int> GetUnreadCountAsync(int userId);

        /// <summary>علّم إشعار واحد كمقروء</summary>
        Task<bool> MarkAsReadAsync(int alertId, int userId);

        /// <summary>علّم كل الإشعارات كمقروءة</summary>
        Task<int> MarkAllAsReadAsync(int userId);

        /// <summary>امسح إشعار واحد</summary>
        Task<bool> DeleteAlertAsync(int alertId, int userId);

        /// <summary>امسح كل الإشعارات المقروءة الأقدم من X يوم (cleanup)</summary>
        Task<int> DeleteOldReadAlertsAsync(int daysOld = 30);
    }
}
