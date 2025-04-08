using AsyncTasksQueue.Data;
using AsyncTasksQueue.Models;
using Microsoft.EntityFrameworkCore;

namespace AsyncTasksQueue.Repositories
{
    public class JobRepository : IJobRepository
    {
        private readonly IDbContextFactory<ApplicationDBContext> _contextFactory;

        public JobRepository(IDbContextFactory<ApplicationDBContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<Job> Add(Job job)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Jobs.Add(job);
            await context.SaveChangesAsync();
            return job;
        }

        public async Task<IEnumerable<Job>> GetAll()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Jobs
                .AsNoTracking()
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();
        }

        public async Task<Job> GetById(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Jobs.FindAsync(id);
        }

        public async Task<IEnumerable<Job>> GetFailedsJobs()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var now = DateTime.UtcNow;

            return await context.Jobs
                .Where(F => F.Status == JobStatus.Failed
                            && F.NextRetryTime <= now
                            && F.RetryCount <= F.MaxRetries)
                .OrderBy(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Job>> GetPendingJobs()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var now = DateTime.UtcNow;

            return await context.Jobs
                .Where(P => P.Status == JobStatus.Pending  )
                .OrderBy(P => P.Priority)
                .ThenBy(P => P.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task Update(Job job)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Jobs.Update(job);
            await context.SaveChangesAsync();
        }
    }

}
