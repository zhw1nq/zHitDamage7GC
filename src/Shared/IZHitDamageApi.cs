using SwiftlyS2.Shared.Players;

namespace zHitDamage7GC.Shared;

/// <summary>
/// Public interface exposed to other SwiftlyS2 plugins to query and control hit damage settings.
/// </summary>
public interface IZHitDamageApi
{
    /// <summary>
    /// Grants access to a player when FreeAccess is false.
    /// </summary>
    void GiveClientAccess(IPlayer player);

    /// <summary>
    /// Revokes access from a player when FreeAccess is false.
    /// </summary>
    void TakeClientAccess(IPlayer player);

    /// <summary>
    /// Checks whether the player currently has permission to use hit damage numbers.
    /// </summary>
    bool HasClientAccess(IPlayer player);

    /// <summary>
    /// Checks whether the player currently has hit damage numbers enabled.
    /// </summary>
    bool IsClientEnabled(IPlayer player);

    /// <summary>
    /// Sets whether the player has hit damage numbers enabled.
    /// </summary>
    void SetClientEnabled(IPlayer player, bool enabled);
}

