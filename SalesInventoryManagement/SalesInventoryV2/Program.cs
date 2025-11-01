using Microsoft . EntityFrameworkCore;
using Microsoft . Extensions . Options;
using Microsoft . Win32;

using SalesInventoryV2 . Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder . Services . AddControllersWithViews ( );

// ===== Register DbContext with Connection String =====
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions => sqlServerOptions
            .CommandTimeout(60) // 60 second timeout
            .EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan . FromSeconds ( 5 ),
                errorNumbersToAdd: null
            )
    );

// V2: Enable sensitive data logging in development (shows parameter values)
if ( builder . Environment . IsDevelopment ( ) )
{
      options . EnableSensitiveDataLogging ( );
      options . EnableDetailedErrors ( );
}
});

// ===== Optional: Add Response Compression =====
builder . Services . AddResponseCompression ( options =>
{
      options . EnableForHttps = true;
} );

// ===== Optional: Add Response Caching =====
builder . Services . AddResponseCaching ( );

// ===== Optional: Add Memory Cache for frequently accessed data =====
builder . Services . AddMemoryCache ( );

var app = builder.Build();

// Configure the HTTP request pipeline.
if ( !app . Environment . IsDevelopment ( ) )
{
      app . UseExceptionHandler ( "/Home/Error" );
      app . UseHsts ( );
}
else
{
      app . UseDeveloperExceptionPage ( );
}

app . UseHttpsRedirection ( );
app . UseStaticFiles ( );

app.UseResponseCompression();

app . UseResponseCaching ( );

app . UseRouting ( );

app . UseAuthorization ( );

app . MapControllerRoute (
    name: "default" ,
    pattern: "{controller=Home}/{action=Index}/{id?}" );

// ===== Optional: Database initialization check =====
using ( var scope = app . Services . CreateScope ( ) )
{
      var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
      try
      {
            // Test database connection
            var canConnect = await context.Database.CanConnectAsync();
            if ( canConnect )
            {
                  Console . WriteLine ( "✓ Database connection successful!" );
            }
            else
            {
                  Console . WriteLine ( "✗ Cannot connect to database!" );
            }
      }
      catch ( Exception ex )
      {
            Console . WriteLine ( $"✗ Database error: {ex . Message}" );
      }
}

app . Run ( );
