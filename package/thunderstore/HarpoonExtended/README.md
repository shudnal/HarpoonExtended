# Harpoon Extended
![](https://staticdelivery.nexusmods.com/mods/3667/images/headers/2528_1695185106.jpg)

There is no spoon. There is Harpoon.

Harpoon movable objects, retrieve or release the line, and pull yourself to immovable targets.

## Installation

Install the same compatible Harpoon Extended version on the server and every client. Version 1.2.0 requires:

* BepInExPack Valheim 5.4.2350 or later;
* Conditional Config Sync 1.0.5 or later, including both of its standalone DLLs.

Conditional Config Sync is a hard dependency. The mod checks remote installation and version compatibility. It no longer has an `Enabled` switch and does not embed a synchronization library. Existing local controls remain local; shared gameplay settings are always server-controlled.

## Basic features

* Harpoon movable objects and pull them towards you, or pull yourself to immovable objects.
* Retrieve the line to reduce the distance to the target, or cast the line to increase it.
* Configure damage, durability, stamina, target categories, projectile gravity and projectile velocity.
* Configure maximum, minimum and break distances.
* Increase maximum item quality and durability per level.
* Apply configurable Feather Fall while harpooning.

Terrain, Leviathan and boss targeting remain disabled by default. The legacy pulling physics, force coefficients, force application point, distance endpoints and line payout behavior are unchanged in this maintenance release.

Hold the "Pull To Target mode" button when the projectile hits to pull yourself to the target. This is not a separate attack combination: the button must be held at impact.

You can pull yourself to moving objects. Attaching yourself to something while pulling, for example sitting down, breaks the line. If another peer takes ownership of your target, the connection is retained and you start pulling yourself towards it. The existing ship-specific ownership policy is retained.

### Target restrictions

`Prefab whitelist` and `Prefab blacklist` are in `2 - Targets`. Enter exact prefab names separated by commas. Whitespace is ignored and matching is case-insensitive; wildcards are not supported.

An empty whitelist adds no restriction. A nonempty whitelist restricts attachment to its entries without overriding the existing target categories. The blacklist always wins. `GrapplingBlocker` also prevents rope attachment.

A rejected attachment is still an ordinary projectile hit: damage follows the existing damage setting and normal hit effects still run. Neither the custom rope nor the vanilla harpoon status is applied to a forbidden target. Changing these lists affects subsequent hits.

### Feather Fall

The harpoon uses its own copy of the vanilla SlowFall effect. It retains the original icon, display name and other effect properties without changing or removing SlowFall supplied by equipment or other mods.

Settings in `6 - Misc`:

* `Feather Fall maximum fall speed`: default **7 m/s**. Zero disables this effect's speed limit.
* `Feather Fall damage multiplier`: default **0**, preventing base fall damage. `0.5` halves base damage and `1` leaves it unchanged. Other effects still contribute normally.

The two values are captured only when a new harpoon Feather Fall effect is added. Editing the configuration does not modify an already active effect. Reusing an active effect also keeps its existing values.

The original removal rules remain in place, including cumulative time spent on the ground. A broken or destroyed rope does not immediately remove fall protection.

### Item upgrades

Upgrades to qualities 2, 3 and 4 require **15, 30 and 60 Chitin**, respectively. The initial crafting ingredients are unchanged.

## Hotkeys

Game bindings:

* Pull line: `Use` (normally E).
* Release line: `Crouch` + `Use` (normally LeftControl + E).
* Stop harpooning: `Block` (normally the right mouse button).
* Pull To Target mode: `Alternative placing` (normally LeftShift).

Additional configurable shortcuts:

* Pull line: T.
* Release line: LeftControl + T.
* Stop harpooning: LeftShift + LeftControl + T.
* Pull To Target mode: LeftShift.

## Multiplayer and lifetime

Player targets retain vanilla harpoon behavior unless explicitly forbidden by the attachment filters. The custom rope is for the existing object and creature pulling modes, not a replacement for the vanilla GrapplingHook.

Destroyed or unloaded participants, missing required rope components and invalid positions terminate the connection. The mod does not replace missing participants, choose another rigidbody during an active connection, or resurrect a broken connection. Observers only render the rope; they do not apply an extra pulling force. The displayed player endpoint follows the left hand without changing the physics endpoints.

## Configuration

Use a compatible BepInEx configuration manager or edit `BepInEx/config/shudnal.HarpoonExtended.cfg`. Server-controlled values are synchronized by Conditional Config Sync. Client-local shortcuts and presentation settings stay local.

## Links

[Source and build instructions](https://github.com/shudnal/HarpoonExtended/)

[Nexus](https://www.nexusmods.com/valheim/mods/2528)
