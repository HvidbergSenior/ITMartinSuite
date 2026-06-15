using ITMartin.Ai;
using ITMartin.OCR;
using ITMartin.Receipt.Application;
using ITMartin.Receipt.Infrastructure;
using ITMartin.Receipt.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReceiptApplication();
builder.Services.AddReceiptInfrastructure();
builder.Services.AddAi();
builder.Services.AddOcr();

// =========================
// SIGNALR
// =========================

builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024 * 20;
});

// =========================
// BLAZOR
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// =========================
// BUILD
// =========================

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

// =========================
// DATA FOLDERS
// =========================

Directory.CreateDirectory("data");
Directory.CreateDirectory("data/receipts");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
