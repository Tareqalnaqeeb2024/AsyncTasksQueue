using AsyncTasksQueue.Models;
using Microsoft.EntityFrameworkCore;

namespace AsyncTasksQueue.Data
{
    public class ApplicationDBContext : DbContext
    {
        public ApplicationDBContext(DbContextOptions options):base(options) 
        
        {

        }
        DbSet<Job> Jobs {  get; set; }
    }
}
