using AsyncTasksQueue.Data;
using AsyncTasksQueue.Models;
using AsyncTasksQueue.Repositories;

namespace AsyncTasksQueue.Services
{
    public class JobService : IJobService
    {
        private readonly IJobRepository _jobRepository;
        private readonly ApplicationDBContext _context;
        private readonly int _maxConcurrentJobs;
        private readonly SemaphoreSlim _semaphore;
        private readonly PriorityQueue<Job, JobPriority> _priorityQueue;
        private readonly object _queueLock = new object();
        private readonly int _maxJobsPerWindow = 10;                // عدد المهام المسموح بها في النافذة
        private readonly TimeSpan _windowDuration = TimeSpan.FromMinutes(1); // مدة النافذة الزمنية


        public JobService(IJobRepository jobRepository, ApplicationDBContext context, int maxConCurrentJobs = 3)
        {
            _jobRepository = jobRepository;
            _context = context;
            _maxConcurrentJobs = maxConCurrentJobs;
            _semaphore = new SemaphoreSlim(maxConCurrentJobs);
            _priorityQueue = new PriorityQueue<Job, JobPriority>();
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

        public async Task ProcessJobs()
        {

            var pendingJobs = await _jobRepository.GetPendingJobs();


            foreach (var job in pendingJobs)
            {

                _priorityQueue.Enqueue(job, job.Priority);
            }

            await ProcessJobsFromQueue();
        }

        //private async Task ProcessJobsFromQueue()
        //{
        //    var processingTasks = new List<Task>();
        //    var delayBetweenJobs = TimeSpan.FromSeconds(6);

        //    while (true)
        //    {
        //        Job nextJob;


        //            if (!_priorityQueue.TryDequeue(out nextJob, out _))
        //                break;


        //        processingTasks.Add(ProcessSingleJobAsync(nextJob));


        //        if (processingTasks.Count >= _maxConcurrentJobs)
        //        {
        //            await Task.Delay(delayBetweenJobs);
        //            await Task.WhenAny(processingTasks);
        //            processingTasks.RemoveAll(t => t.IsCompleted);
        //        }
        //    }
        //    await Task.WhenAll(processingTasks);
        //}


        //private async Task ProcessJobsFromQueue()
        //{
        //    var processingTasks = new List<Task>();
        //    var delayBetweenJobs = TimeSpan.FromSeconds(6);

        //    int tasksProcessedThisMinute = 0;
        //    DateTime minuteWindowStart = DateTime.UtcNow;

        //    while (true)
        //    {
        //        Job nextJob;

        //        if (!_priorityQueue.TryDequeue(out nextJob, out _))
        //            break;

        //        // تحقق من عدد المهام التي تمت خلال الدقيقة الحالية
        //        if (tasksProcessedThisMinute >= 10)
        //        {
        //            var timeSinceWindowStart = DateTime.UtcNow - minuteWindowStart;

        //            if (timeSinceWindowStart < TimeSpan.FromMinutes(1))
        //            {
        //                var delay = TimeSpan.FromMinutes(1) - timeSinceWindowStart;

        //                await Task.Delay(delay);
        //            }

        //            // إعادة ضبط العداد بعد مرور دقيقة
        //            tasksProcessedThisMinute = 0;
        //            minuteWindowStart = DateTime.UtcNow;
        //        }

        //        processingTasks.Add(ProcessSingleJobAsync(nextJob));
        //        tasksProcessedThisMinute++;


        //            await Task.Delay(delayBetweenJobs);
        //            await Task.WhenAny(processingTasks);
        //            processingTasks.RemoveAll(t => t.IsCompleted);

        //    }

        //    await Task.WhenAll(processingTasks);
        //}

        public async Task ProcessFailedsJobs()
        {

            var FailedsJobs = await _jobRepository.GetPendingJobs();


            foreach (var job in FailedsJobs)
            {

                _priorityQueue.Enqueue(job, job.Priority);
            }

            await ProcessJobsFromQueue();
        }


        private async Task ProcessJobsFromQueue()
        {
            var processingTasks = new List<Task>();

            int tasksProcessedInWindow = 0;
            DateTime windowStart = DateTime.UtcNow;

            while (true)
            {
                if (!_priorityQueue.TryDequeue(out Job nextJob, out _))
                    break;

                var timeSinceWindowStart = DateTime.UtcNow - windowStart;

                if (tasksProcessedInWindow >= _maxJobsPerWindow)
                {
                    var remainingTime = _windowDuration - timeSinceWindowStart;

                    if (remainingTime > TimeSpan.Zero)
                    {
                        await Task.Delay(remainingTime);
                    }

                    // إعادة ضبط العداد وبداية نافذة جديدة
                    tasksProcessedInWindow = 0;
                    windowStart = DateTime.UtcNow;
                }

                // بدء المعالجة
                var task = ProcessSingleJobAsync(nextJob);
                processingTasks.Add(task);
                tasksProcessedInWindow++;

                // التحكم في التزامن
                if (processingTasks.Count >= _maxConcurrentJobs)
                {
                    var completed = await Task.WhenAny(processingTasks);
                    processingTasks.Remove(completed);
                }
            }

            await Task.WhenAll(processingTasks);
        }

        private async Task ProcessSingleJobAsync(Job job)
        {
            await _semaphore.WaitAsync();
            try
            {
                job.Status = JobStatus.InProgress;
                await _jobRepository.Update(job);

                await Task.Delay(1000);

                bool isSuccess = new Random().Next(0, 2) == 0;

                if (isSuccess)
                {
                    job.Status = JobStatus.Completed;
                    await _jobRepository.Update(job);
                    return;
                }

                job.Status = JobStatus.Failed;
                await _jobRepository.Update(job);
                //await HandleFailedJobAsync(job);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task RetryFailedJobAsync(Job job)
        {
            //job.Status = JobStatus.Failed;
            job.RetryCount++;

            if (job.RetryCount >= job.MaxRetries)
            {
                job.Status = JobStatus.DeadLetter;
                await _jobRepository.Update(job);
                return;
            }


            int delay = (int)Math.Pow(2, job.RetryCount);
            job.NextRetryTime = DateTime.UtcNow.AddSeconds(delay);
            await _jobRepository.Update(job);

            _priorityQueue.Enqueue(job, job.Priority);

        }



    }
}
