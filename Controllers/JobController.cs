using AsyncTasksQueue.Models;
using AsyncTasksQueue.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AsyncTasksQueue.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobController(IJobService jobService)
        {
            _jobService = jobService;
        }
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Job>> CreateJobs([FromBody] JobDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                for (int i = 0; i < 10; i++)
                {
                    await _jobService.EnqueueJob(
                       request.TaskName,
                       request.TaskData,
                       request.Priority,
                       request.MaxRetries);
                }
                //return CreatedAtAction(nameof(GetJob), new { id = jobId }, null);

                return Ok("Added  Ten Jobs Successfuly");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        [HttpPut("process")]
        public async Task<ActionResult> ProcessJobs()
        {

            await _jobService.ProcessJobs();
            return Ok("Job processing started .");
        }

        [HttpPut("RetryProcessFailedJob")]
        public async Task<ActionResult> RetryProcessFailedJobs()
        {

            await _jobService.ProcessFailedsJobs();
            return Ok("Job processing started .");
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<Job>>> GetJobs()
        {
            var jobs = await _jobService.GetAllJobs();
            return Ok(jobs);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Job>> GetJob(int id)
        {
            if (id <= 0) return BadRequest("Invailed Number");
            var job = await _jobService.GetJobById(id);
            return job == null ? NotFound() : Ok(job);
        }

        [HttpGet("stats")]
        public async Task<ActionResult<JobStats>> GetStats()
        {
            var stats = await _jobService.GetJobStats();
            return Ok(stats);
        }

        public class JobDTO
        {
            public string TaskName { get; set; }
            public string TaskData { get; set; }
            public JobPriority Priority { get; set; } = JobPriority.Medium;
            public int MaxRetries { get; set; } = 3;
        }
    }
}
