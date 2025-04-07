using AsyncTasksQueue.Models;

namespace AsyncTasksQueue.Services
{
    public interface IJobService
    {

        Task EnqueueJob(string taskName, string data, JobPriority priority = JobPriority.Medium, int maxRetries = 3);
        Task ProcessJobs();
        Task ProcessFailedsJobs();
        Task<JobStats> GetJobStats();
        Task<IEnumerable<Job>> GetAllJobs();
        Task<Job> GetJobById(int id);
    }
    public class JobStats
    {
        public int TotalJobs { get; set; }
        public int PendingJobs { get; set; }
        public int InProgressJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int FailedJobs { get; set; }
        public int DeadLetterJobs { get; set; }
    }
}
