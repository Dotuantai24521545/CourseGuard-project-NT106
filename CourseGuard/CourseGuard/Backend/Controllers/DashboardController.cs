using System;
using CourseGuard.Backend.Data;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;

namespace CourseGuard.Backend.Controllers
{
    public class DashboardController
    {
        private readonly CourseGuardDbContext _dbContext;

        public DashboardController(CourseGuardDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public List<LoginFrequencyModel> GetLoginFrequencyStatistics(int days = 7)
        {
            if (ServerApiClient.Enabled) return ServerApiClient.GetAsync<List<LoginFrequencyModel>>($"api/admin/login-frequency?days={days}").GetAwaiter().GetResult();
            return _dbContext.GetLoginFrequencyStats(days);
        }

        public Task<List<LoginFrequencyModel>> GetLoginFrequencyStatisticsAsync(int days = 7, CancellationToken cancellationToken = default)
        {
            if (ServerApiClient.Enabled) return ServerApiClient.GetAsync<List<LoginFrequencyModel>>($"api/admin/login-frequency?days={days}", cancellationToken);
            return _dbContext.GetLoginFrequencyStatsAsync(days, cancellationToken);
        }

        public List<CourseListItemModel> GetCourseListStatistics(int limit = 100)
        {
            if (ServerApiClient.Enabled) return ServerApiClient.GetAsync<List<CourseListItemModel>>($"api/admin/course-statistics?limit={limit}").GetAwaiter().GetResult();
            return _dbContext.GetCourseListItems(limit);
        }

        public Task<List<CourseListItemModel>> GetCourseListStatisticsAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            if (ServerApiClient.Enabled) return ServerApiClient.GetAsync<List<CourseListItemModel>>($"api/admin/course-statistics?limit={limit}", cancellationToken);
            return _dbContext.GetCourseListItemsAsync(limit, cancellationToken);
        }

        public AccountSummaryModel GetAccountSummaryStatistics()
        {
            if (ServerApiClient.Enabled) return ServerApiClient.GetAsync<AccountSummaryModel>("api/admin/account-summary").GetAwaiter().GetResult();
            return _dbContext.GetAccountSummary();
        }

        public Task<AccountSummaryModel> GetAccountSummaryStatisticsAsync(CancellationToken cancellationToken = default)
        {
            if (ServerApiClient.Enabled) return ServerApiClient.GetAsync<AccountSummaryModel>("api/admin/account-summary", cancellationToken);
            return _dbContext.GetAccountSummaryAsync(cancellationToken);
        }
    }
}
