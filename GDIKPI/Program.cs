using GDIKPI.Data;
using GDIKPI.Hubs;
using GDIKPI.Services;
using GDIKPI.Services.ReportServices;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddDbContext<KpisContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("AttendanceApi")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Login";
        options.AccessDeniedPath = "/Home/Privacy";
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<OEEService>();
builder.Services.AddScoped<OEEReportService>();
builder.Services.AddScoped<QualityDashboardService>();
builder.Services.AddScoped<QualityDashboardReportService>();
builder.Services.AddScoped<ModuleCardService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AttendanceLineSyncService>();
builder.Services.AddHostedService<DailyOEESchedulerService>();
builder.Services.AddHostedService<DailyEfficiencySchedulerService>();
builder.Services.AddHostedService<DailyEfficiencySchedulerService>();
var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");
app.MapControllers();
app.MapHub<DashboardHub>("/dashboardHub");

app.Run();
