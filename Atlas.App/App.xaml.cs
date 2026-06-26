using System.Diagnostics.CodeAnalysis;
using Uno.Resizetizer;

namespace Atlas.App;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }

    /// <summary>UI dispatcher for services that must hop to the UI thread (e.g. file pickers).</summary>
    internal static Microsoft.UI.Dispatching.DispatcherQueue? MainDispatcher { get; private set; }

    /// <summary>Model path passed on the command line / via file association (desktop). Set by
    /// Program.Main before the host runs; consumed once at bridge start.</summary>
    internal static string? StartupModelPath { get; set; }

    /// <summary>App.xaml.cs path to extract-and-boot from (desktop); set by Program.Main.</summary>
    internal static string? StartupSourcePath { get; set; }
    protected IHost? Host { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            // Add navigation support for toolkit controls such as TabBar and NavigationView
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);

                    // Uno Platform namespace filter groups
                    // Uncomment individual methods to see more detailed logging
                    //// Generic Xaml events
                    //logBuilder.XamlLogLevel(LogLevel.Debug);
                    //// Layout specific messages
                    //logBuilder.XamlLayoutLogLevel(LogLevel.Debug);
                    //// Storage messages
                    //logBuilder.StorageLogLevel(LogLevel.Debug);
                    //// Binding related messages
                    //logBuilder.XamlBindingLogLevel(LogLevel.Debug);
                    //// Binder memory references tracking
                    //logBuilder.BinderMemoryReferenceLogLevel(LogLevel.Debug);
                    //// DevServer and HotReload related
                    //logBuilder.HotReloadCoreLogLevel(LogLevel.Information);
                    //// Debug JS interop
                    //logBuilder.WebAssemblyLogLevel(LogLevel.Debug);

                }, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IAppModelSource, JsonAppModelSource>();
                    services.AddSingleton<ILayoutStore, JsonLayoutStore>();
                    services.AddSingleton<IRecentModels>(_ => new JsonRecentModels());
                    services.AddSingleton(new StartupOptions(StartupModelPath, StartupSourcePath));
                    services.AddSingleton<IModelFilePicker, ModelFilePicker>();
                    services.AddSingleton<IRuntimeBridge, RuntimeBridge>();
                    RegisterAgentQuery(services);
                })
                .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes)
            );
        MainWindow = builder.Window;
        MainDispatcher = MainWindow.DispatcherQueue;

        #if DEBUG
        MainWindow.UseStudio();
#endif
                MainWindow.SetWindowIcon();

        Host = await builder.NavigateAsync<Shell>();
    }

    // Claude answers when an API key is present (desktop build only); otherwise the local interpreter does.
    private static void RegisterAgentQuery(IServiceCollection services)
    {
#if ATLAS_CLAUDE
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            services.AddSingleton<IAgentQuery, ClaudeAgentQuery>();
            return;
        }
#endif
        services.AddSingleton<IAgentQuery, LocalAgentQuery>();
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellModel)),
            new ViewMap<MapPage, MapModel>()
        );

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                Nested:
                [
                    new ("Map", View: views.FindByViewModel<MapModel>(), IsDefault:true),
                ]
            )
        );
    }
}
