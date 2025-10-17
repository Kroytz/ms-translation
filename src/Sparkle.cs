using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sharp.Shared;

namespace Kxnrl.Sparkle;

public sealed class Sparkle : IModSharpModule
{
    public string DisplayName   => "Sparkle from StarRail";
    public string DisplayAuthor => "Kxnrl";

    private readonly ILogger<Sparkle> _logger;
    private readonly InterfaceBridge  _bridge;
    private readonly ServiceProvider  _serviceProvider;

    public Sparkle(ISharedSystem sharedSystem,
        string                   dllPath,
        string                   sharpPath,
        Version                  version,
        IConfiguration           coreConfiguration,
        bool                     hotReload)
    {
        ArgumentNullException.ThrowIfNull(dllPath);
        ArgumentNullException.ThrowIfNull(sharpPath);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(coreConfiguration);

        var configuration = new ConfigurationBuilder()
                            .AddJsonFile(Path.Combine(dllPath, "appsettings.json"), false, false)
                            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(sharedSystem.GetLoggerFactory());
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

        _bridge          = new InterfaceBridge(dllPath, sharpPath, version, sharedSystem);
        _logger          = sharedSystem.GetLoggerFactory().CreateLogger<Sparkle>();
        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init()
    {
        _logger.LogInformation(
            "Oh wow, we seem to be crossing paths a lot lately... Where could I have seen you before? Can you figure it out?");

        return true;
    }

    public void Shutdown()
    {
        _logger.LogInformation("See you around, Nameless~ Try to stay out of trouble, especially... the next time we meet!");
    }

    public void PostInit()
    {
        _logger.LogInformation("Why don't you stay and play for a while?");
    }

    public void OnAllModulesLoaded()
    {
        _logger.LogInformation("A foolish sage or a wise fool... Who will I become next?");
    }

    public void OnLibraryConnected(string name)
    {
        _logger.LogInformation("The~ Game~ Is~ On~");
    }

    public void OnLibraryDisconnect(string name)
    {
        _logger.LogInformation("Done playing for today...");
    }
}
