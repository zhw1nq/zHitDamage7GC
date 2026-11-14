# zHitDamage7GC (SwiftlyS2)

Fortnite-style hit damage numbers display plugin for Counter-Strike 2, built specifically for the modern **SwiftlyS2** framework.

---

## ✨ Features & Performance Optimizations

- ⚡ **Ultra-Smooth (Zero OnTick Loop)**: Uses `Core.Scheduler.DelayBySeconds` instead of heavy `OnTick()` polling loops. Zero garbage collection allocations per frame, eliminating FPS drops and server hitches.
- 🎯 **View-Aligned Camera Billboard Math**: Uses attacker view yaw angle and direction basis vectors (`fwd`, `right`) from Metamod architecture. Prevents digits from clumping into blobs or displaying in reverse regardless of viewing angle.
- 📐 **Anti-Clipping Offset (`Distance`)**: Pulls digits along the attacker's line of sight toward the camera so numbers never get buried inside the player model mesh.
- 🎨 **Fully Configurable Particle Paths**: No hardcoded asset paths. Uses dynamic token patterns `{digit}`, `{side}`, `{crit}`. Easily switch particle packs by updating a single configuration line.
- 👁️ **Client-Scoped Transmit (`SetTransmitState`)**: Transmits damage particles only to the attacker (and spectators viewing the attacker). Eliminates visual clutter for third parties and saves server network bandwidth.
- 💬 **Chat Debug Mode**: Enable debug via configuration (`"Debug": true`) to print damage numbers, hit group, weapon name, and active particle paths directly to the attacker's chat window on every hit.
- 🛡️ **Memory-Safe Entity Tracking**: Uses `CHandle<CParticleSystem>` to track entity lifecycles safely across round resets, player disconnects, and map changes without native crashes.
- 🎮 **Default Enabled & In-Game Toggle**: Enabled by default for all connecting players. Players can toggle it on or off in-game via `!damage`. Preferences are stored purely in RAM (no database or disk writes).
- 🌐 **Clean Localization & Dynamic Prefix**: Adheres strictly to the SwiftlyS2 localizer standard with isolated prefix `[gold]zHitDamage7GC [silver]» `. Bundled with English (`en.jsonc`), Vietnamese (`vn.jsonc`), Russian (`ru.jsonc`), and German (`de.jsonc`).
- 🔌 **Shared API**: Exposes `IZHitDamageApi` for seamless integration and control from other SwiftlyS2 plugins.

---

## 📂 Particle Asset Naming Convention

Based on the 20-file optimized asset layout:
- Directory: `particles/thuyskieu/hdg/`
- Normal Hit: `7mau_dmg_{digit}_n.vpcf` (digits 0 - 9)
- Headshot Hit: `7mau_dmg_{digit}_h.vpcf` (digits 0 - 9)

Token definitions:
- `{digit}`: Digit number (`0` through `9`).

---

## ⚙️ Configuration (`config.jsonc`)

Automatically generated from `resources/templates/config.template.jsonc`:

```jsonc
{
  "HitDamage": {
    // Enable or disable the entire hit damage plugin
    "Enabled": true,

    // Whether all players have access to hit damage display (Default: true)
    "FreeAccess": true,

    // Permission required when FreeAccess is false (e.g., "zhitdamage.vip")
    "RequiredPermission": "",

    // Chat and console command triggers (e.g., !hit, !hits, !damage, !dmg, !damages)
    "Commands": [ "hit", "hits", "damage", "dmg", "damages" ],
    "CommandsEnabled": true,

    // When true, prints detailed damage, weapon, headshot, and particle paths to attacker's chat on every hit
    "Debug": false,

    // Transmit damage particles only to the attacker (significant performance and network optimization)
    "OnlyAttackerVisible": true,

    // Allow players spectating the attacker to also see the damage particles
    "SpectatorsCanSee": true,

    // Particle asset path configuration (Placeholders: {digit} for 0-9)
    "Particles": {
      // Normal hit damage particle pattern
      "NMPathPattern": "particles/thuyskieu/hdg/7mau_dmg_{digit}_n.vpcf",
      // Headshot / Critical hit damage particle pattern
      "HSPathPattern": "particles/thuyskieu/hdg/7mau_dmg_{digit}_h.vpcf"
    },

    // Shotgun damage accumulation settings (pellet grouping)
    "Shotgun": {
      "Weapons": [ "xm1014", "nova", "mag7", "sawedoff" ],
      // Time in seconds to accumulate shotgun pellets before displaying the total damage number
      "AccumulationDelaySeconds": 0.1
    },

    // Display duration and positioning offsets
    "Display": {
      // Distance in game units to pull numbers toward the attacker (prevents clipping inside victim model, Default: 40.0)
      "Distance": 40.0,
      // Lifetime of the damage particle in seconds (Default: 2.0s)
      "DurationSeconds": 2.0,
      // Base spacing between digits
      "SpacingBase": 6.0,
      // Distance threshold where digit spacing scales up for readability
      "DistanceScaleThreshold": 700.0,
      // Vertical height offsets relative to the victim's origin (normal vs headshot)
      "HeightOffsetStandingNormal": 35.0,
      "HeightOffsetStandingCrit": 60.0,
      "HeightOffsetDuckingNormal": 25.0,
      "HeightOffsetDuckingCrit": 45.0,
      // Random position spread for natural appearance
      "RandomSpreadHorizontal": 1.0,
      "RandomSpreadVertical": 10.0
    },

    // Optional audio feedback on hit
    "Sounds": {
      "Enabled": false,
      "HitSoundEvent": "",
      "CritSoundEvent": "",
      "Volume": 0.5,
      "Pitch": 1.0
    }
  }
}
```

---

## 🎮 Commands & Usage

| Command | Channel | Description |
| :--- | :--- | :--- |
| `!hit` / `/hit` | Chat | Toggle hit damage numbers on/off for yourself |
| `!hits` / `!damage` / `!dmg` / `!damages` | Chat | Command aliases |
| `sw_hit` | Console | Client console equivalent |

---

## 🛠️ Build & Packaging

```bash
# Build in Release mode and automatically create the deployment zip
dotnet publish -c Release
```

Output files will be generated at:
- `build/zHitDamage7GC.zip`
- `build/publish/zHitDamage7GC/`
