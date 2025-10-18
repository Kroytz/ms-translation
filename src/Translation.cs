using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Kroytz.Translation.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace Kroytz.Translation;

public sealed class Translation : IModSharpModule, ITranslation, IClientListener
{
    public int ListenerVersion => 1;
    public int ListenerPriority => 0;

    public string DisplayName => "Translation";
    public string DisplayAuthor => "Kroytz";

    private readonly ILogger<Translation> _logger;
    private readonly InterfaceBridge _bridge;
    private readonly ServiceProvider _serviceProvider;

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<PlayerSlot, string> _clientLanguages = new();

    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arabic"] = "ar",
        ["Bulgarian"] = "bg",
        ["SChinese"] = "chi",
        ["Czech"] = "cze",
        ["Danish"] = "da",
        ["German"] = "de",
        ["Greek"] = "el",
        ["English"] = "en",
        ["Spanish"] = "es",
        ["Finnish"] = "fi",
        ["French"] = "fr",
        ["Hebrew"] = "he",
        ["Hungarian"] = "hu",
        ["Italian"] = "it",
        ["Japanese"] = "jp",
        ["Korean"] = "ko",
        ["LatAm"] = "las",
        ["Lithuanian"] = "lt",
        ["Latvian"] = "lv",
        ["Dutch"] = "nl",
        ["Norwegian"] = "no",
        ["Polish"] = "pl",
        ["Brazilian"] = "pt",
        ["Portuguese"] = "pt_p",
        ["Romanian"] = "ro",
        ["Russian"] = "ru",
        ["Slovak"] = "sk",
        ["Swedish"] = "sv",
        ["Thai"] = "th",
        ["Turkish"] = "tr",
        ["Ukrainian"] = "ua",
        ["Vietnamese"] = "vi",
        ["TChinese"] = "zho",
    };

    private static string GetLanguageCode(string languageName)
    {
        if (Languages.TryGetValue(languageName, out var code))
        {
            return code;
        }

        return string.Empty;
    }

    public Translation(ISharedSystem sharedSystem,
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

        _bridge = new InterfaceBridge(dllPath, sharpPath, version, this, sharedSystem);
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<Translation>();
        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init()
    {
        return true;
    }

    public void Shutdown()
    {
        _bridge.ClientManager.RemoveClientListener(this);
    }

    public void PostInit()
    {
        _bridge.ClientManager.InstallClientListener(this);

        _bridge.SharpModuleManager.RegisterSharpModuleInterface(_bridge.Module, ITranslation.Identity, this);
    }

    public void OnAllModulesLoaded()
    {
    }

    public void OnLibraryConnected(string name)
    {
    }

    public void OnLibraryDisconnect(string name)
    {
    }

    public void OnConVarQueryValueFinished(IGameClient client, QueryConVarValueStatus status, string name, string value)
    {
        if (status == QueryConVarValueStatus.ValueIntact && name == "cl_language")
        {
            var languageCode = GetLanguageCode(value);
            _clientLanguages[client.Slot] = languageCode;
            _logger.LogInformation($"Client {client.Slot} language {languageCode}");
        }
    }

    public void OnClientPostAdminCheck(IGameClient client)
    {
        if (client.IsValid && client is { IsHltv: false, IsFakeClient: false })
        {
            _bridge.ClientManager.QueryConVar(client, "cl_language", OnConVarQueryValueFinished);
        }
    }

    public bool LoadTranslation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning("LoadTranslation called with empty path");
            return false;
        }

        var callerAssemblyName = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";

        try
        {
            var translationDir = Path.Combine(_bridge.SharpPath, "translation");
            var translationFile = Path.Combine(translationDir, path + ".json");

            if (!File.Exists(translationFile))
            {
                _logger.LogWarning($"Translation file not found: {translationFile}");
                return false;
            }

            var rawText = File.ReadAllText(translationFile, Encoding.UTF8);

            using var document = JsonDocument.Parse(rawText);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning($"Translation file root is not an object: {translationFile}");
                return false;
            }

            // 保存格式: Caller.Key
            // e.g. MatchZy.NotReady
            int added = 0;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                /**
                {
                    "NotReady": {
                        "en": "Not Ready",
                        "cn": "未准备好"
                    }
                }
                */
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var key = $"{callerAssemblyName}.{property.Name}";
                if (!_translations.TryGetValue(key, out var groupDict) || groupDict is null)
                {
                    groupDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _translations[key] = groupDict;
                }

                var set = property.Value.EnumerateObject();
                foreach (var setProperty in set)
                {
                    if (setProperty.Value.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    groupDict[setProperty.Name] = setProperty.Value.GetString() ?? string.Empty;
                    added++;
                }
            }

            _logger.LogInformation($"[Assembly: {callerAssemblyName}] Loaded {added} translations from {translationFile}");
            return added > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Assembly: {callerAssemblyName}] Failed to load translation {path}");
            return false;
        }
    }

    public string GetTranslated(IGameClient client, string key, params object[] args)
    {
        if (client is null)
        {
            return key;
        }

        if (!_clientLanguages.TryGetValue(client.Slot, out var lang) || lang is null)
        {
            return key;
        }

        return GetTranslated(key, lang, args);
    }

    public string GetTranslated(string key, string lang, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return string.Empty;
        }

        var callerAssemblyName = Assembly.GetCallingAssembly().GetName().Name ?? "Unknown";

        try
        {
            // Build full key as CallerAssemblyName.lang => e.g., MatchZy.MatchZy.NotReady
            var fullKey = $"{callerAssemblyName}.{lang}";

            if (!_translations.TryGetValue(fullKey, out var group) || group is null)
            {
                return fullKey;
            }

            if (!group.TryGetValue(lang, out var template) || string.IsNullOrEmpty(template))
            {
                if (!group.TryGetValue("en", out template) || string.IsNullOrEmpty(template))
                {
                    return fullKey;
                }
            }

            return string.Format(template, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Assembly: {callerAssemblyName}] GetTranslated failed for key {key}");
            return lang;
        }
    }
}
