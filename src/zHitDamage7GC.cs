using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using zHitDamage7GC.Config;
using zHitDamage7GC.Managers;
using zHitDamage7GC.Shared;

namespace zHitDamage7GC;

[PluginMetadata(
    Id = "zHitDamage7GC",
    Version = "1.0.0",
    Name = "zHitDamage7GC",
    Author = "vhming",
    Description = "Fortnite-style hit damage numbers display for SwiftlyS2")]
public partial class zHitDamage7GC : BasePlugin, IZHitDamageApi
{
    private IOptionsMonitor<PluginConfig> _configMonitor = null!;
    private PlayerManager _playerManager = null!;
    private DamageManager _damageManager = null!;

    private readonly List<Guid> _registeredCommandGuids = [];
    private EventDelegates.OnClientPutInServer? _clientPutInServerDelegate;
    private EventDelegates.OnClientDisconnected? _clientDisconnectedDelegate;
    private EventDelegates.OnMapUnload? _mapUnloadDelegate;

    public zHitDamage7GC(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IZHitDamageApi, zHitDamage7GC>("zHitDamage7GC.Api.v1", this);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void Load(bool hotReload)
    {
        InitializeConfiguration();

        _playerManager = new PlayerManager(Core, _configMonitor, Core.Logger);
        _damageManager = new DamageManager(Core, _configMonitor, _playerManager, Core.Logger);

        // Core events
        _clientPutInServerDelegate = OnClientPutInServer;
        Core.Event.OnClientPutInServer += _clientPutInServerDelegate;

        _clientDisconnectedDelegate = OnClientDisconnected;
        Core.Event.OnClientDisconnected += _clientDisconnectedDelegate;

        _mapUnloadDelegate = OnMapUnload;
        Core.Event.OnMapUnload += _mapUnloadDelegate;

        // Register commands from config
        RegisterCommands();

        // Listen for config reload
        _configMonitor.OnChange(_ =>
        {
            Core.Scheduler.NextTick(() =>
            {
                RegisterCommands();
                Core.Logger.LogInformation("[zHitDamage7GC] Configuration reloaded successfully.");
            });
        });

        Core.Logger.LogInformation("[zHitDamage7GC] Plugin loaded successfully (HotReload: {HotReload}).", hotReload);
    }

    public override void Unload()
    {
        if (_clientPutInServerDelegate != null)
        {
            Core.Event.OnClientPutInServer -= _clientPutInServerDelegate;
        }

        if (_clientDisconnectedDelegate != null)
        {
            Core.Event.OnClientDisconnected -= _clientDisconnectedDelegate;
        }

        if (_mapUnloadDelegate != null)
        {
            Core.Event.OnMapUnload -= _mapUnloadDelegate;
        }

        UnregisterCommands();

        _damageManager?.CleanupAll();

        Core.Logger.LogInformation("[zHitDamage7GC] Plugin unloaded.");
    }

    private void InitializeConfiguration()
    {
        const string configFileName = "config.jsonc";
        const string configTemplateName = "config.template.jsonc";

        Core.Configuration
            .InitializeWithTemplate(configFileName, configTemplateName)
            .Configure(builder =>
            {
                builder.AddJsonFile(
                    Core.Configuration.GetConfigPath(configFileName),
                    optional: false,
                    reloadOnChange: true);
            });

        var services = new ServiceCollection();
        services.AddSwiftly(Core);
        services.AddOptionsWithValidateOnStart<PluginConfig>().BindConfiguration("HitDamage");

        var provider = services.BuildServiceProvider();
        _configMonitor = provider.GetRequiredService<IOptionsMonitor<PluginConfig>>();
    }

    private void RegisterCommands()
    {
        UnregisterCommands();

        var config = _configMonitor.CurrentValue;
        if (!config.CommandsEnabled || config.Commands.Count == 0)
        {
            return;
        }

        var primary = config.Commands.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
        if (string.IsNullOrWhiteSpace(primary))
        {
            return;
        }

        // Register primary command with sw_ prefix
        var guid = Core.Command.RegisterCommand(primary, OnToggleCommand, registerRaw: false);
        _registeredCommandGuids.Add(guid);

        // Register aliases
        foreach (var alias in config.Commands.Skip(1))
        {
            if (!string.IsNullOrWhiteSpace(alias) && !string.Equals(alias, primary, StringComparison.OrdinalIgnoreCase))
            {
                Core.Command.RegisterCommandAlias(primary, alias, registerRaw: false);
            }
        }
    }

    private void UnregisterCommands()
    {
        foreach (var guid in _registeredCommandGuids)
        {
            Core.Command.UnregisterCommand(guid);
        }
        _registeredCommandGuids.Clear();
    }

    private void OnToggleCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender == null)
        {
            context.Reply(Core.Localizer["command.only_players"]);
            return;
        }

        var player = context.Sender;
        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var prefix = localizer["prefix"];

        if (!_playerManager.HasAccess(player))
        {
            player.SendChat(prefix + localizer["command.no_access"]);
            return;
        }

        var isEnabled = _playerManager.ToggleEnabled(player);
        player.SendChat(prefix + (isEnabled ? localizer["command.enabled"] : localizer["command.disabled"]));
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerHurt(EventPlayerHurt @event)
    {
        var attacker = @event.AttackerPlayer;
        var victim = @event.UserIdPlayer;

        if (attacker == null || victim == null || !attacker.IsValid || !victim.IsValid)
        {
            return HookResult.Continue;
        }

        if (attacker.IsFakeClient || attacker.PlayerID == victim.PlayerID)
        {
            return HookResult.Continue;
        }

        if (!_playerManager.HasAccess(attacker) || !_playerManager.IsEnabled(attacker))
        {
            return HookResult.Continue;
        }

        var damage = @event.ActualDmgHealth;
        var isCrit = @event.ActualHitGroup == HitGroup_t.HITGROUP_HEAD;
        var weapon = @event.Weapon;

        _damageManager.ProcessDamage(attacker, victim, damage, isCrit, weapon);

        return HookResult.Continue;
    }

    private void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player != null)
        {
            _playerManager.OnPlayerConnected(player);
        }
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player != null)
        {
            _playerManager.OnPlayerDisconnected(player);
        }
        _damageManager.CleanupPlayer(@event.PlayerId);
    }

    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        _damageManager.CleanupAll();
    }

    #region IZHitDamageApi Implementation

    public void GiveClientAccess(IPlayer player) => _playerManager.GiveAccess(player);

    public void TakeClientAccess(IPlayer player) => _playerManager.TakeAccess(player);

    public bool HasClientAccess(IPlayer player) => _playerManager.HasAccess(player);

    public bool IsClientEnabled(IPlayer player) => _playerManager.IsEnabled(player);

    public void SetClientEnabled(IPlayer player, bool enabled) => _playerManager.SetEnabled(player, enabled);

    #endregion
}
