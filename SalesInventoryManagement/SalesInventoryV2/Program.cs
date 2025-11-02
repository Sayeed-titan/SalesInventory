// Program.cs
// FIXED VERSION - Correct service registration order

using Microsoft . EntityFrameworkCore;
using SalesInventoryV2 . Data;
using SalesInventoryV2 . Services;
using DinkToPdf;
using DinkToPdf . Contracts;
using System . Runtime . Loader;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder . Services . AddControllersWithViews ( );

//   Register DinkToPdf FIRST (before DbContext)
// Load native library
var context = new CustomAssemblyLoadContext();
var wkHtmlToPdfPath = System.IO.Path.Combine(
    Directory.GetCurrentDirectory(),
    "wwwroot",
    "lib",
    "wkhtmltox",
    "libwkhtmltox.dll"
);

// Only load if file exists
if ( System . IO . File . Exists ( wkHtmlToPdfPath ) )
{
      context . LoadUnmanagedLibrary ( wkHtmlToPdfPath );
      Console . WriteLine ( "✓ DinkToPdf library loaded successfully" );
}
else
{
      Console . WriteLine ( $"⚠ Warning: libwkhtmltox.dll not found at {wkHtmlToPdfPath}" );
      Console . WriteLine ( "PDF export will not work. Please download the library." );
}

// Register DinkToPdf converter as Singleton
builder . Services . AddSingleton ( typeof ( IConverter ) , new SynchronizedConverter ( new PdfTools ( ) ) );

//   Register Report Export Service
builder . Services . AddScoped<ReportExportService> ( );

//   Register DbContext (AFTER other services)
builder . Services . AddDbContext<ApplicationDbContext> ( options =>
    options . UseSqlServer (
        builder . Configuration . GetConnectionString ( "DefaultConnection" ) ,
        sqlServerOptions => sqlServerOptions
            . CommandTimeout ( 60 )
            . EnableRetryOnFailure ( maxRetryCount: 3 )
    ) );

// Optional: Add response compression
builder . Services . AddResponseCompression ( options =>
{
      options . EnableForHttps = true;
} );

// Optional: Add memory cache
builder . Services . AddMemoryCache ( );

var app = builder.Build();

// Configure the HTTP request pipeline
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

// Enable response compression
app . UseResponseCompression ( );

app . UseRouting ( );
app . UseAuthorization ( );

app . MapControllerRoute (
    name: "default" ,
    pattern: "{controller=Home}/{action=Index}/{id?}" );

// Optional: Test database connection on startup
using ( var scope = app . Services . CreateScope ( ) )
{
      try
      {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync();
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

//   Custom Assembly Load Context for DinkToPdf
internal class CustomAssemblyLoadContext : AssemblyLoadContext
{
      public IntPtr LoadUnmanagedLibrary ( string absolutePath )
      {
            return LoadUnmanagedDll ( absolutePath );
      }

      protected override IntPtr LoadUnmanagedDll ( string unmanagedDllName )
      {
            return LoadUnmanagedDllFromPath ( unmanagedDllName );
      }

      protected override System . Reflection . Assembly? Load ( System . Reflection . AssemblyName assemblyName )
      {
            return null;
      }
}