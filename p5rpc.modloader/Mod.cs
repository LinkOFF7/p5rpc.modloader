﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using FileEmulationFramework.Lib.Utilities;
using p5rpc.modloader.Configuration;
using p5rpc.modloader.Patches;
using p5rpc.modloader.Patches.Common;
using p5rpc.modloader.Template;
using p5rpc.modloader.Utilities;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

// Free perf gains, but you'll need to remember that any stackalloc isn't 0 initialized.
[module: SkipLocalsInit]

namespace p5rpc.modloader;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public unsafe class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    public static Config Configuration = null!;

    /// <summary>
    /// Current process.
    /// </summary>
    public static Process CurrentProcess = null!;
    
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private readonly Logger _logger;
    private CpkBindBuilder? _cpkBuilder;
    private CpkBinder? _binder;
    private SigScanHelper _scanHelper;

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _owner = context.Owner;
        Configuration = context.Configuration;
        _logger = new Logger(context.Logger, Configuration.LogLevel);
        _modConfig = context.ModConfig;

        // For more information about this template, please see
        // https://reloaded-project.github.io/Reloaded-II/ModTemplate/

        // If you want to implement e.g. unload support in your mod,
        // and some other neat features, override the methods in ModBase.
        _modLoader.GetController<IStartupScanner>().TryGetTarget(out var startupScanner);
        _scanHelper = new SigScanHelper(_logger, startupScanner);
        CurrentProcess = Process.GetCurrentProcess();
        var baseAddr = CurrentProcess.MainModule!.BaseAddress;
        
        var patchContext = new PatchContext()
        {
            BaseAddress = baseAddr,
            Config = Configuration,
            Logger = _logger,
            Hooks = _hooks!,
            ScanHelper = _scanHelper
        };
        
        // Patches
        CpkBinderPointers.Init(_scanHelper, baseAddr);
        NoPauseOnFocusLoss.Activate(patchContext);
        DontLogCriDirectoryBinds.Activate(patchContext);
        SkipIntro.Activate(patchContext);
        
        // CPK Builder & Redirector
        _cpkBuilder = new CpkBindBuilder(_modLoader, _logger, _modConfig);
        _modLoader.ModLoading += OnModLoading;
        _modLoader.OnModLoaderInitialized += OnLoaderInitialized;
    }

    private void OnModLoading(IModV1 arg1, IModConfigV1 arg2) => _cpkBuilder?.Add((IModConfig)arg2);

    private void OnLoaderInitialized()
    {
        _modLoader.ModLoading -= OnModLoading;
        _binder = _cpkBuilder?.Build(_hooks); // Return something that stores only the data we need here.
        _cpkBuilder = null;  // We don't need this anymore :), free up the mem!
    }

    #region Standard Overrides

    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        Configuration = configuration;
        _logger.LogLevel = Configuration.LogLevel;
        _logger.Info($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    #endregion Standard Overrides

    #region For Exports, Serialization etc.

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public Mod()
    { }

#pragma warning restore CS8618

    #endregion For Exports, Serialization etc.
}