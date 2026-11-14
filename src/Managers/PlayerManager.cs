using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using zHitDamage7GC.Config;

namespace zHitDamage7GC.Managers;

public sealed class PlayerManager
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<PluginConfig> _configMonitor;
    private readonly ILogger _logger;

    // In-memory RAM storage (no file or database persistence)
    private readonly ConcurrentDictionary<ulong, bool> _playerEnabled = new();
    private readonly ConcurrentDictionary<ulong, bool> _playerAccess = new();

    public PlayerManager(ISwiftlyCore core, IOptionsMonitor<PluginConfig> configMonitor, ILogger logger)
    {
        _core = core;
        _configMonitor = configMonitor;
        _logger = logger;
    }

    public bool HasAccess(IPlayer player)
    {
        var config = _configMonitor.CurrentValue;
        if (config.FreeAccess)
        {
            return true;
        }

        var steamId = player.SteamID;
        if (steamId == 0)
        {
            return false;
        }

        if (_playerAccess.TryGetValue(steamId, out var access) && access)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(config.RequiredPermission))
        {
            return _core.Permission.PlayerHasPermission(steamId, config.RequiredPermission);
        }

        return false;
    }

    public void GiveAccess(IPlayer player)
    {
        if (player.SteamID != 0)
        {
            _playerAccess[player.SteamID] = true;
        }
    }

    public void TakeAccess(IPlayer player)
    {
        if (player.SteamID != 0)
        {
            _playerAccess[player.SteamID] = false;
        }
    }

    /// <summary>
    /// Checks whether hit damage is enabled for this player. Default is always true (enabled).
    /// </summary>
    public bool IsEnabled(IPlayer player)
    {
        var steamId = player.SteamID;
        if (steamId == 0)
        {
            return true;
        }

        // Default is always true (enabled)
        return _playerEnabled.GetOrAdd(steamId, true);
    }

    public void SetEnabled(IPlayer player, bool enabled)
    {
        var steamId = player.SteamID;
        if (steamId == 0)
        {
            return;
        }

        _playerEnabled[steamId] = enabled;
    }

    public bool ToggleEnabled(IPlayer player)
    {
        var nextState = !IsEnabled(player);
        SetEnabled(player, nextState);
        return nextState;
    }

    public bool IsDebugEnabled => _configMonitor.CurrentValue.Debug;

    public void OnPlayerConnected(IPlayer player)
    {
        if (player.IsFakeClient || player.SteamID == 0)
        {
            return;
        }

        // Default is enabled when a player connects
        _playerEnabled.TryAdd(player.SteamID, true);
    }

    public void OnPlayerDisconnected(IPlayer player)
    {
    }

    public void ResetAll()
    {
        _playerEnabled.Clear();
        _playerAccess.Clear();
    }
}
