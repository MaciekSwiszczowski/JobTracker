var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// The one endpoint that exists before the real ones: the UI and the skill use it to tell "the service is not
// running" apart from "the service has no applications yet".
app.MapGet("/health", static () => TypedResults.Ok(new { status = "ok" }));

app.Run();
