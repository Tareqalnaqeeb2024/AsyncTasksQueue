using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AsyncTasksQueue.Models
{
    
    public enum JobStatus
    {
        Pending = 1,
        InProgress = 2,
        Completed = 3,
        Failed = 4,
        DeadLetter = 5
    }

    public enum JobPriority
    {
        High = 1,
        Medium = 2,
        Low = 3

    }
    public class Job
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string TaskName { get; set; }

        public string TaskData { get; set; }

        public JobPriority Priority { get; set; } = JobPriority.Medium;

        public JobStatus Status { get; set; } = JobStatus.Pending;

        public int RetryCount { get; set; } = 0;

        public int MaxRetries { get; set; } = 3;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? NextRetryTime { get; set; }
    }
}
