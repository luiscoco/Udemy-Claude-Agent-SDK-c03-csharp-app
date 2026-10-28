using Anthropic;
using LessonC03.Components;
using LessonC03.Services;

DotEnv.Load(".env");

// Console commands mirror the Node scripts: setup, session, history. No argument starts the web app.
switch (args.FirstOrDefault())
{
    case "setup": await Cli.Setup(); return;
    case "session": await Cli.Session(string.Join(' ', args.Skip(1))); return;
    case "history": await Cli.History(args.ElementAtOrDefault(1)); return;
}

var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var p) ? p : 3003;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The key stays on the server; the placeholder lets the page render an unconfigured state.
builder.Services.AddSingleton(_ => new AnthropicClient
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is { Length: > 0 } key ? key : "not-configured",
});
builder.Services.AddSingleton<SessionLab>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
