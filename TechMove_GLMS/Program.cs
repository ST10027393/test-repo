// FILE: TechMove_GLMS/Program.cs
using Microsoft.EntityFrameworkCore;
using TechMove_GLMS.Models;
using TechMove_GLMS.Data;
using TechMove_GLMS.Services;
using TechMove_GLMS.Patterns.Factory;

var builder = WebApplication.CreateBuilder(args);

//Firbase App sessions
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<GlmsDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("GLMS_DB")));

builder.Services.AddScoped<IClientService, ClientService>();

builder.Services.AddScoped<IContractFactory, StandardContractFactory>();

builder.Services.AddScoped<IContractService, ContractService>();

builder.Services.AddHttpClient<ICurrencyService, CurrencyService>();

builder.Services.AddScoped<IServiceRequestFactory, ServiceRequestFactory>();

// 3. Register the Data Service
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseSession();
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");


app.Run();
