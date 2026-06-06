using Microsoft.EntityFrameworkCore;
using HospitalApi.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();


builder.Services.AddDbContext<HospitalContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


var app = builder.Build();


app.MapControllers();

app.Run();
