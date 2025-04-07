using AsyncTasksQueue.Data;
using AsyncTasksQueue.Repositories;
using AsyncTasksQueue.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure database 
builder.Services.AddDbContextFactory<ApplicationDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConnectionString")),
    ServiceLifetime.Scoped);

// Register repositories
builder.Services.AddScoped<IJobRepository, JobRepository>();
// Register services
builder.Services.AddScoped<IJobService, JobService>();

builder.Services.AddHttpClient();

// Configure the HTTP request pipeline.

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
