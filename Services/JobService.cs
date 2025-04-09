using AsyncTasksQueue.Data;
using AsyncTasksQueue.Models;
using AsyncTasksQueue.Repositories;
using Polly;
using Polly.RateLimit;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AsyncTasksQueue.Services
{
    public class JobService : IJobService
    {
        private readonly AsyncRateLimitPolicy _rateLimitPolicy;
        private readonly IJobRepository _jobRepository;
        private readonly ApplicationDBContext _context;
        private readonly PriorityQueue<Job, JobPriority> _priorityQueue;
        


        public JobService(IJobRepository jobRepository, ApplicationDBContext context)
        {
            _jobRepository = jobRepository;
            _context = context;
            _priorityQueue = new PriorityQueue<Job, JobPriority>();

            _rateLimitPolicy = Policy.RateLimitAsync(
           numberOfExecutions: 20,
           perTimeSpan: TimeSpan.FromSeconds(100),
           maxBurst: 2);

        }

        public async Task EnqueueJob(string taskName, string data, JobPriority priority = JobPriority.Medium, int maxRetries = 3)
        {
            if (!await _context.Database.CanConnectAsync())
            {
                throw new Exception("Database unavailable");
            }

            priority = (JobPriority)new Random().Next(1, 4);
            var job = new Job
            {
                TaskName = taskName,
                TaskData = data,
                Priority = priority,
                MaxRetries = maxRetries
            };
            job = await _jobRepository.Add(job);

            _priorityQueue.Enqueue(job, job.Priority);

        }
        public async Task<IEnumerable<Job>> GetAllJobs()
        {
            return await _jobRepository.GetAll();
        }

        public async Task<Job> GetJobById(int id)
        {
            return await _jobRepository.GetById(id);
        }

        public async Task<JobStats> GetJobStats()
        {
            var jobs = await _jobRepository.GetAll();


            return new JobStats
            {
                TotalJobs = jobs.Count(),
                PendingJobs = jobs.Count(j => j.Status == JobStatus.Pending),
                InProgressJobs = jobs.Count(j => j.Status == JobStatus.InProgress),
                CompletedJobs = jobs.Count(j => j.Status == JobStatus.Completed),
                FailedJobs = jobs.Count(j => j.Status == JobStatus.Failed),
                DeadLetterJobs = jobs.Count(j => j.Status == JobStatus.DeadLetter),
            };
        }

        public async Task ProcessPendingJobs()
        {

            var pendingJobs = await _jobRepository.GetPendingJobs();


            foreach (var job in pendingJobs)
            {

                _priorityQueue.Enqueue(job, job.Priority);
            }

             await ProcessJobsFromQueue(maxJobsPerInterval: 5, interval: TimeSpan.FromMinutes(2));

        }


        public async Task ProcessJobsFromQueue(int maxJobsPerInterval, TimeSpan interval)
        {
            while (true)
            {
                int jobsProcessed = 0;
                var intervalStart = DateTime.UtcNow;

                while (jobsProcessed < maxJobsPerInterval)
                {
                    if (!_priorityQueue.TryDequeue(out var nextJob, out _))
                        break;

                    try
                    {
                        await _rateLimitPolicy.ExecuteAsync(async () =>
                        {
                            await ProcessSingleJobAsync(nextJob);
                            await SendJobStatusToExternalApi(nextJob);
                        });

                        jobsProcessed++;
                    }
                    catch (RateLimitRejectedException ex)
                    {
                       
                        _priorityQueue.Enqueue(nextJob, nextJob.Priority);
                        await Task.Delay(ex.RetryAfter);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing job: {ex.Message}");
                    }
                }

               
                var elapsed = DateTime.UtcNow - intervalStart;
                var remainingTime = interval - elapsed;
                if (remainingTime > TimeSpan.Zero)
                {
                    await Task.Delay(remainingTime);
                }
            }
        }


        private async Task ProcessSingleJobAsync(Job job)
       {
            
                job.Status = JobStatus.InProgress;
             
                bool isSuccess = new Random().Next(0, 2) == 0;

                if (isSuccess)
                {
                    job.Status = JobStatus.Completed;
                    await _jobRepository.Update(job);
                    return;
                }
                else
                {
                    if (job.RetryCount >= job.MaxRetries)
                    {
                        job.Status = JobStatus.DeadLetter;
                        await _jobRepository.Update(job);
                        return;
                    }
                }

                 job.Status = JobStatus.Failed;
                job.RetryCount++;
                int delay = (int)Math.Pow(2, job.RetryCount);
                job.NextRetryTime = DateTime.UtcNow.AddSeconds(delay);
                await _jobRepository.Update(job);  
    
        }

        public async Task ProcessFailedsJobs()
        {
            var FailedsJobs = await _jobRepository.GetFailedsJobs();

            foreach (var job in FailedsJobs)
            {
                _priorityQueue.Enqueue(job, job.Priority);
            }
            await ProcessJobsFromQueue(maxJobsPerInterval: 5, interval: TimeSpan.FromMinutes(2));


        }

        private async Task SendJobStatusToExternalApi(Job job)
        {
            try
            {
                var requestData = new
                {
                    jobId = job.Id,
                    status = job.Status.ToString()
                };

                using (var httpClient = new HttpClient())
                {
                    var json = JsonSerializer.Serialize(requestData);
                    Console.WriteLine(json);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync("https://localhost:7192/api/SaveStatusResult", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"API Error Response:");
                        throw new HttpRequestException($"API Error: {response.StatusCode} - {responseBody}");
                    }
                    Console.WriteLine("Status successfully updated!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update job status: {ex.Message}");
                throw; 
            }
        }

        public async Task ProcessSingleJobAsync(int id)
        {
            var FailedJob = await _jobRepository.GetFaliedJob0ById(id);


            await ProcessSingleJobAsync(FailedJob);
        }
    }
}
