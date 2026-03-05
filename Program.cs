var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS - Allow your website domains
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFCTools", policy =>
    {
        policy.WithOrigins(
                "https://fctools.dev",
                "https://www.fctools.dev",
                "https://fctools.vercel.app",
                "https://fc-tools.netlify.app",
                "https://fc-ymt-api.onrender.com",
                "http://localhost:3000"
              )
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Enable Swagger in all environments
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFCTools");
app.MapControllers();

Console.WriteLine("FC YMT API starting...");
app.Run();