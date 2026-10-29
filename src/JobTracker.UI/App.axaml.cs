using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JobTracker.Contracts;
using JobTracker.UI.Services;
using JobTracker.UI.ViewModels;
using JobTracker.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.UI;

/// <summary>The Avalonia application, and the UI's composition root.</summary>
public partial class App : Application
{
    /// <summary>The command-line switch that runs the app on built-in sample data instead of the service.</summary>
    public const string SampleSwitch = "--sample";

    /// <summary>The environment variable that points the app at a service elsewhere than the default address.</summary>
    public const string ServiceAddressVariable = "JOBTRACKER_API";

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var useSample = desktop.Args?.Contains(SampleSwitch, StringComparer.Ordinal) == true;
            var services = ConfigureServices(useSample).BuildServiceProvider();
            desktop.Exit += (_, _) => services.Dispose();
            var viewModel = services.GetRequiredService<MainViewModel>();

            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.MainWindow.Title = useSample ? "Job Tracker - sample data" : "Job Tracker";
            viewModel.RefreshCommand.Execute(null);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceCollection ConfigureServices(bool useSample)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        if (useSample)
        {
            services.AddSingleton<IJobTrackerClient>(sp =>
            {
                var today = DateOnly.FromDateTime(sp.GetRequiredService<TimeProvider>().GetLocalNow().DateTime);
                return new InMemoryJobTrackerClient(SampleData.Create(today));
            });
        }
        else
        {
            // The container owns the HttpClient and disposes it with itself when the app exits.
            services.AddSingleton(_ => new HttpClient { BaseAddress = ServiceAddress() });
            services.AddSingleton<IJobTrackerClient, HttpJobTrackerClient>();
        }

        services.AddTransient<MainViewModel>();
        return services;
    }

    private static Uri ServiceAddress() =>
        new(Environment.GetEnvironmentVariable(ServiceAddressVariable) is { Length: > 0 } configured
            ? configured
            : ApiRoutes.DefaultBaseAddress);
}
