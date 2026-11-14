using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;
using zHitDamage7GC.Config;

namespace zHitDamage7GC.Managers;

public sealed class DamageManager
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<PluginConfig> _configMonitor;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, ShotgunData>> _shotgunData = new();
    private readonly ConcurrentDictionary<int, List<CHandle<CParticleSystem>>> _activeParticles = new();
    private readonly Random _random = new();

    private string[] _normalPaths = new string[10];
    private string[] _headshotPaths = new string[10];

    private sealed class ShotgunData
    {
        public int TotalDamage { get; set; }
        public bool IsCrit { get; set; }
        public Vector? Position { get; set; }
        public bool IsDucking { get; set; }
        public bool IsProcessing { get; set; }
        public string Weapon { get; set; } = string.Empty;
    }

    public DamageManager(
        ISwiftlyCore core,
        IOptionsMonitor<PluginConfig> configMonitor,
        PlayerManager _,
        ILogger logger)
    {
        _core = core;
        _configMonitor = configMonitor;
        _logger = logger;

        UpdateCachedPaths(_configMonitor.CurrentValue);
        _configMonitor.OnChange(UpdateCachedPaths);
    }

    private void UpdateCachedPaths(PluginConfig config)
    {
        var normal = new string[10];
        var headshot = new string[10];
        for (int i = 0; i < 10; i++)
        {
            var digitStr = i.ToString();
            normal[i] = config.Particles.NMPathPattern.Replace("{digit}", digitStr);
            headshot[i] = config.Particles.HSPathPattern.Replace("{digit}", digitStr);
        }
        _normalPaths = normal;
        _headshotPaths = headshot;
    }

    public void ProcessDamage(IPlayer attacker, IPlayer victim, int damage, bool isCrit, string weapon)
    {
        var config = _configMonitor.CurrentValue;
        if (!config.Enabled)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        if (IsShotgunWeapon(weapon, config))
        {
            HandleShotgunDamage(attacker, victim, damage, isCrit, weapon, config);
        }
        else
        {
            ShowDamageParticles(attacker, victim, damage, isCrit, weapon, null, null, config);
        }
    }

    public void CleanupPlayer(int playerId)
    {
        _shotgunData.TryRemove(playerId, out _);

        if (_activeParticles.TryRemove(playerId, out var list))
        {
            lock (list)
            {
                foreach (var handle in list)
                {
                    if (handle.IsValid && handle.Value is { } entity)
                    {
                        try
                        {
                            entity.Despawn();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[zHitDamage7GC] Error despawning particle during player cleanup.");
                        }
                    }
                }
                list.Clear();
            }
        }
    }

    public void CleanupAll()
    {
        _shotgunData.Clear();

        foreach (var (_, list) in _activeParticles)
        {
            lock (list)
            {
                foreach (var handle in list)
                {
                    if (handle.IsValid && handle.Value is { } entity)
                    {
                        try
                        {
                            entity.Despawn();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[zHitDamage7GC] Error despawning particle during full cleanup.");
                        }
                    }
                }
                list.Clear();
            }
        }
        _activeParticles.Clear();
    }

    private static bool IsShotgunWeapon(string weapon, PluginConfig config)
    {
        if (string.IsNullOrWhiteSpace(weapon))
        {
            return false;
        }

        var cleanWeapon = weapon.StartsWith("weapon_") ? weapon["weapon_".Length..] : weapon;
        return config.Shotgun.Weapons.Any(w => string.Equals(w, cleanWeapon, StringComparison.OrdinalIgnoreCase));
    }

    private void HandleShotgunDamage(
        IPlayer attacker,
        IPlayer victim,
        int damage,
        bool isCrit,
        string weapon,
        PluginConfig config)
    {
        var attackerMap = _shotgunData.GetOrAdd(attacker.PlayerID, _ => new ConcurrentDictionary<int, ShotgunData>());
        var data = attackerMap.GetOrAdd(victim.PlayerID, _ => new ShotgunData());

        lock (data)
        {
            if (!data.IsProcessing)
            {
                data.IsProcessing = true;
                data.TotalDamage = damage;
                data.IsCrit = isCrit;
                data.Weapon = weapon;

                var victimPawn = victim.PlayerPawn;
                if (victimPawn?.AbsOrigin != null)
                {
                    var origin = victimPawn.AbsOrigin.Value;
                    data.Position = new Vector(origin.X, origin.Y, origin.Z);
                    data.IsDucking = (victimPawn.Flags & 2) != 0;
                }

                var delaySeconds = Math.Max(0.01f, config.Shotgun.AccumulationDelaySeconds);
                _core.Scheduler.DelayBySeconds(delaySeconds, () =>
                {
                    FlushShotgunDamage(attacker.PlayerID, victim.PlayerID);
                });
            }
            else
            {
                data.TotalDamage += damage;
                if (isCrit)
                {
                    data.IsCrit = true;
                }
            }
        }
    }

    private void FlushShotgunDamage(int attackerId, int victimId)
    {
        if (!_shotgunData.TryGetValue(attackerId, out var attackerMap))
        {
            return;
        }

        if (!attackerMap.TryRemove(victimId, out var data))
        {
            return;
        }

        if (attackerMap.IsEmpty)
        {
            _shotgunData.TryRemove(attackerId, out _);
        }

        var attacker = _core.PlayerManager.GetPlayer(attackerId);
        var victim = _core.PlayerManager.GetPlayer(victimId);

        if (attacker == null || !attacker.IsValid || data.TotalDamage <= 0)
        {
            return;
        }

        var config = _configMonitor.CurrentValue;
        ShowDamageParticles(attacker, victim, data.TotalDamage, data.IsCrit, data.Weapon, data.Position, data.IsDucking, config);
    }

    private void ShowDamageParticles(
        IPlayer attacker,
        IPlayer? victim,
        int damage,
        bool isCrit,
        string weapon,
        Vector? storedPosition,
        bool? storedIsDucking,
        PluginConfig config)
    {
        var attackerPawn = attacker.PlayerPawn;
        if (attackerPawn?.AbsOrigin == null)
        {
            return;
        }

        Vector victimPos;
        bool isDucking = false;

        if (storedPosition.HasValue)
        {
            victimPos = storedPosition.Value;
            isDucking = storedIsDucking ?? false;
        }
        else if (victim?.PlayerPawn?.AbsOrigin != null)
        {
            victimPos = victim.PlayerPawn.AbsOrigin.Value;
            isDucking = (victim.PlayerPawn.Flags & 2) != 0;
        }
        else
        {
            return;
        }

        var eyeAngles = attackerPawn.EyeAngles;
        var attackerPos = attackerPawn.AbsOrigin.Value;
        var dist = (attackerPos - victimPos).Length();

        // Calculate view-aligned direction vectors based on yaw (matches Metamod AngleVectors logic)
        var yawAngle = new QAngle(0, eyeAngles.Yaw, 0);
        yawAngle.ToDirectionVectors(out var fwd, out var right, out _);

        // Fallback safety if forward/right degenerates
        if (Math.Abs(fwd.X) < 1e-4f && Math.Abs(fwd.Y) < 1e-4f)
        {
            var dir = victimPos - attackerPos;
            dir.Z = 0;
            var len = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            if (len > 0.001f)
            {
                fwd = new Vector(dir.X / len, dir.Y / len, 0);
                right = new Vector(fwd.Y, -fwd.X, 0);
            }
            else
            {
                fwd = new Vector(1, 0, 0);
                right = new Vector(0, -1, 0);
            }
        }

        // Distance-based spacing scale: increases readability at longer distances
        var d = dist > config.Display.DistanceScaleThreshold
            ? (dist / config.Display.DistanceScaleThreshold * config.Display.SpacingBase)
            : config.Display.SpacingBase;

        // Base position starts at victim origin
        var basePos = victimPos;

        // Digits extraction: lowest to highest digit (dmgnums[0] = units, dmgnums[1] = tens, ...)
        var dmgnums = new List<int>();
        var tempDamage = Math.Max(1, damage);
        while (tempDamage > 0)
        {
            dmgnums.Add(tempDamage % 10);
            tempDamage /= 10;
        }

        var count = dmgnums.Count;
        var l = (int)Math.Ceiling(count / 2.0);

        // Random horizontal jitter along the right basis vector (matches Metamod: right * d * l * random[-0.5, 1.0])
        var horizontalJitter = (float)(_random.NextDouble() * 1.5 - 0.5) * config.Display.RandomSpreadHorizontal;
        basePos.X += right.X * d * l * horizontalJitter;
        basePos.Y += right.Y * d * l * horizontalJitter;

        // Vertical height offset (standing vs ducking, normal vs headshot/crit)
        float baseHeight = isCrit
            ? (isDucking ? config.Display.HeightOffsetDuckingCrit : config.Display.HeightOffsetStandingCrit)
            : (isDucking ? config.Display.HeightOffsetDuckingNormal : config.Display.HeightOffsetStandingNormal);

        var verticalSpread = (float)(_random.NextDouble() * (isCrit ? config.Display.RandomSpreadVertical : config.Display.RandomSpreadVertical * 2.0f));
        basePos.Z += baseHeight + verticalSpread;

        var dif = config.Display.Distance;
        var firstParticlePath = string.Empty;

        // Loop from most significant digit down to 0 (matches Metamod)
        for (int i = count - 1; i >= 0; i--)
        {
            var digit = dmgnums[i];
            var currentL = l--;

            var digitPos = new Vector(
                basePos.X - (fwd.X * dif + right.X * d * currentL),
                basePos.Y - (fwd.Y * dif + right.Y * d * currentL),
                basePos.Z
            );

            var path = SpawnDigitParticle(
                attacker,
                digit,
                digitPos,
                eyeAngles,
                isCrit,
                config
            );

            if (i == count - 1)
            {
                firstParticlePath = path;
            }
        }

        // Optional sound emission
        PlayHitSound(attacker, isCrit, config);

        // In-game debug message for verifying particle appearance and damage
        if (config.Debug)
        {
            var victimName = victim?.Name ?? "Unknown";
            attacker.SendChat($"[gold][DEBUG] [default]Hit [green]{victimName} [default]| Dmg: [gold]{damage} [default]| Headshot: [{(isCrit ? "lightred" : "grey")}]{isCrit} [default]| Wep: [lime]{weapon}");
            attacker.SendChat($"[gold][DEBUG] [default]Particle: [lightblue]{firstParticlePath}");
        }
    }

    private string SpawnDigitParticle(
        IPlayer attacker,
        int digit,
        Vector position,
        QAngle angles,
        bool isCrit,
        PluginConfig config)
    {
        var paths = isCrit ? _headshotPaths : _normalPaths;
        var path = (digit >= 0 && digit < paths.Length) ? paths[digit] : string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var particle = _core.EntitySystem.CreateEntityByDesignerName<CParticleSystem>("info_particle_system");
            if (particle == null)
            {
                _logger.LogWarning("[zHitDamage7GC] Failed to create info_particle_system for path: {Path}", path);
                return path;
            }

            particle.EffectName = path;
            particle.StartActive = true;
            particle.Teleport(position, angles, null);
            particle.DispatchSpawn();

            ApplyTransmitRules(particle, attacker, config);

            var handle = _core.EntitySystem.GetRefEHandle(particle);
            TrackParticle(attacker.PlayerID, handle);

            var duration = Math.Max(0.05f, config.Display.DurationSeconds);
            _core.Scheduler.DelayBySeconds(duration, () =>
            {
                UntrackParticle(attacker.PlayerID, handle);
                if (handle.IsValid && handle.Value is { } entity)
                {
                    try
                    {
                        entity.Despawn();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[zHitDamage7GC] Error despawning particle.");
                    }
                }
            });

            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[zHitDamage7GC] Error creating damage particle {Path}", path);
            return path;
        }
    }

    private void ApplyTransmitRules(CParticleSystem particle, IPlayer attacker, PluginConfig config)
    {
        if (!config.OnlyAttackerVisible)
        {
            return;
        }

        particle.SetTransmitState(false);
        particle.SetTransmitState(true, attacker.PlayerID);

        if (config.SpectatorsCanSee)
        {
            var attackerPawn = attacker.PlayerPawn;
            if (attackerPawn != null)
            {
                foreach (var other in _core.PlayerManager.GetAllPlayers())
                {
                    if (other.IsValid && !other.IsAlive && other.PlayerID != attacker.PlayerID)
                    {
                        var obsTarget = other.PlayerPawn?.ObserverServices?.ObserverTarget.Value;
                        if (obsTarget != null && obsTarget.Index == attackerPawn.Index)
                        {
                            particle.SetTransmitState(true, other.PlayerID);
                        }
                    }
                }
            }
        }
    }

    private void PlayHitSound(IPlayer attacker, bool isCrit, PluginConfig config)
    {
        if (!config.Sounds.Enabled)
        {
            return;
        }

        var soundName = isCrit && !string.IsNullOrWhiteSpace(config.Sounds.CritSoundEvent)
            ? config.Sounds.CritSoundEvent
            : config.Sounds.HitSoundEvent;

        if (string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        try
        {
            using var sound = new SoundEvent
            {
                Name = soundName,
                SourceEntityIndex = -1,
                Volume = config.Sounds.Volume,
                Pitch = config.Sounds.Pitch
            };

            sound.Recipients.AddRecipient(attacker.PlayerID);
            sound.Emit();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[zHitDamage7GC] Error playing sound event {Sound}", soundName);
        }
    }

    public static string FormatPath(string pattern, int digit)
    {
        return pattern.Replace("{digit}", digit.ToString());
    }

    private void TrackParticle(int playerId, CHandle<CParticleSystem> handle)
    {
        var list = _activeParticles.GetOrAdd(playerId, _ => new List<CHandle<CParticleSystem>>());
        lock (list)
        {
            list.Add(handle);
        }
    }

    private void UntrackParticle(int playerId, CHandle<CParticleSystem> handle)
    {
        if (_activeParticles.TryGetValue(playerId, out var list))
        {
            lock (list)
            {
                list.Remove(handle);
            }
        }
    }
}
