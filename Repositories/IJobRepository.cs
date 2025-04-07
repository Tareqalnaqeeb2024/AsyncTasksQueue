using AsyncTasksQueue.Models;

namespace AsyncTasksQueue.Repositories
{
    public interface IJobRepository
    {
       
         Task<Job> GetById(int id);
         Task<IEnumerable<Job>> GetAll();
         Task<IEnumerable<Job>> GetPendingJobs();
        Task<IEnumerable<Job>> GetFailedsJobs();
        Task<Job> Add(Job job);
         Task Update(Job job);

        
    }
}
