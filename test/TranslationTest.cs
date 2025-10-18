using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Kroytz.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace Kroytz.TranslationTest;

public sealed class TranslationTest : IModSharpModule, IClientListener
{
    public int ListenerVersion => 1;
    public int ListenerPriority => 0;

    public string DisplayName => "Translation Testsuite";
    public string DisplayAuthor => "Kroytz";

    private readonly ISharedSystem _shared;
    private readonly ILogger<TranslationTest> _logger;
    private readonly ServiceProvider _serviceProvider;

    public TranslationTest(ISharedSystem sharedSystem,
        string dllPath,
        string sharpPath,
        Version version,
        IConfiguration coreConfiguration,
        bool hotReload)
    {
        ArgumentNullException.ThrowIfNull(dllPath);
        ArgumentNullException.ThrowIfNull(sharpPath);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(coreConfiguration);

        var services = new ServiceCollection();

        services.AddSingleton(sharedSystem.GetLoggerFactory());
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

        _shared = sharedSystem;
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<TranslationTest>();
        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init()
    {
        return true;
    }

    public void Shutdown()
    {
    }

    public void PostInit()
    {
    }

    public void OnAllModulesLoaded()
    {
        var manager = _shared.GetSharpModuleManager();

        if (manager.GetOptionalSharpModuleInterface<ITranslation>(ITranslation.Identity) is not { } translation)
        {
            _logger.LogError("Couldn't found shared interface {s}", ITranslation.Identity);
            return;
        }

        if (translation.Instance is not { } instance)
        {
            _logger.LogError("Shared interface {s} is not longer available", ITranslation.Identity);
            return;
        }

        instance.LoadTranslation("MatchZy");
        var translated = instance.GetTranslated("matchzy.ready.markedready", "en");
        _logger.LogInformation("Translated matchzy.ready.markedready: {s}", translated);
        var translatedWithFormat = instance.GetTranslated("matchzy.pracc.blind", "en", "Kroytz", 1.0f);
        _logger.LogInformation("Translated matchzy.pracc.blind: {s}", translatedWithFormat);
    }

    public void OnLibraryConnected(string name)
    {
    }

    public void OnLibraryDisconnect(string name)
    {
    }
}
