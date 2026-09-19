using System;
using CourseGuard.Backend.Data;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Models;

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
            return _dbContext.GetLoginFrequencyStats(days);
        }

        public Task<List<LoginFrequencyModel>> GetLoginFrequencyStatisticsAsync(int days = 7, CancellationToken cancellationToken = default)
        {
            return _dbContext.GetLoginFrequencyStatsAsync(days, cancellationToken);
        }

        public List<CourseListItemModel> GetCourseListStatistics(int limit = 100)
        {
            return _dbContext.GetCourseListItems(limit);
        }

        public Task<List<CourseListItemModel>> GetCourseListStatisticsAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            return _dbContext.GetCourseListItemsAsync(limit, cancellationToken);
        }

        public AccountSummaryModel GetAccountSummaryStatistics()
        {
            return _dbContext.GetAccountSummary();
        }

        public Task<AccountSummaryModel> GetAccountSummaryStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return _dbContext.GetAccountSummaryAsync(cancellationToken);
        }
    }
}
