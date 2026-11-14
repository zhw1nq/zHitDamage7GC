# zHitDamage7GC

Fortnite-style hit damage numbers display plugin for Counter-Strike 2 on **SwiftlyS2**.

## Features

- Displays floating damage numbers on hit (normal vs headshot).
- Camera billboard math to align numbers with the attacker's viewpoint.
- Distance offset to prevent numbers from clipping inside player models.
- Only visible to the attacker and their spectators (`SetTransmitState`).
- Shotgun damage accumulation to group pellets into a single number.
- In-game player toggle with state stored in RAM.
- Multi-language support (English, Vietnamese, Russian, German).

## Particles

Default asset path: `particles/thuyskieu/hdg/`
- **Normal:** `7mau_dmg_{digit}_n.vpcf` (`0` - `9`)
- **Headshot:** `7mau_dmg_{digit}_h.vpcf` (`0` - `9`)

## Commands

- `!hit`, `!hits`, `!damage`, `!dmg`, `!damages`: Toggle damage numbers on/off.
- `sw_hit`: Console command equivalent.

## Build

```bash
dotnet publish -c Release
```
Output: `build/zHitDamage7GC.zip`
