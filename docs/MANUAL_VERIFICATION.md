# User-side verification before release

These scenarios have not been executed by the author of this maintenance change. Build and run the mod locally against the selected Valheim installation.

## Installation and configuration

Verify compatible server/client connections with HarpoonExtended 1.1.13 and CCS installed on both sides. Verify rejection of a missing or incompatible remote mod. Check that controls remain client-local while gameplay/filter/fall-protection values follow the server. Confirm that no Enabled setting is registered and that no dependency DLL is bundled into the generated package.

## Ordinary pulling

Compare creatures, logs, items, free ships, self-pulling to static surfaces, deliberate self-pulling, controlled ships and carts with the previous version. Check line reeling, payout, stop inputs, stamina and automatic ownership-loss self-pulling. Ship ownership handling intentionally retains the previous policy.

Confirm that moving the displayed endpoint to the left hand does not change physical stopping or break distances. Verify hand rendering on another client. Check that a server/observer does not apply a second force.

## Invalid connections

Destroy a networked target, destroy a target without a ZNetView, unload a target, remove a hit collider and destroy/disable a rope component. Verify complete local state cleanup with no replacement target or repeated exceptions. Teleport or move an endpoint beyond the existing break threshold: no further pull should be applied before the connection ends. No extra displacement heuristic or new maximum payout length is intended.

Leave the world with an active connection, reconnect and create another one. Verify that no old references or visuals survive. Repeat with pending or active fall protection. A rope disappearing in the air must not immediately strip the protection.

## Hits and filters

With empty filters, compare normal target categories and the native player-target path. Then use exact mixed-case names with whitespace in the whitelist and blacklist. Check blacklist priority, whitelist restriction and GrapplingBlocker on the hit object or its parent.

A forbidden target should still receive the normal configured damage and hit effects, stop the projectile as normal, and receive neither a custom rope nor the vanilla Harpooned status. A rejected/dodged/non-accepted hit must not create a rope. Verify ordinary arrows while several harpoons are in flight, including a harpoon expiring without hitting anything.

## Distance configuration

Set Max distance before the first shot in a world, then edit it during the session. Subsequent hits must use the current value rather than the last connection's cached value. Existing active lengths and payout rules remain unchanged. Verify target names without parentheses with diagnostic target-name messages enabled.

## Fall protection

With no other fall modifier, verify default maximum downward speed 5 m/s and zero fall damage. Repeat with a damage multiplier of 0.5 and 1. Change settings while the custom effect is active: the current instance must keep its original values, and a newly applied instance must use the new values.

Equip an item supplying vanilla SlowFall before, during and after harpoon use. Neither its effect parameters nor its lifetime may be overwritten by the custom effect. Overlapping status modifiers still compose according to the game's normal rules.

Verify all original removal conditions, especially cumulative ground time: short hops must not reset accumulated grounded time. A broken rope must not immediately remove its active effect.

## Recipes and packaging

Verify identical displayed, checked and consumed Chitin costs of 15/30/60 for upgrades to qualities 2/3/4. Initial crafting requirements remain unchanged. Inspect the package for the mod DLL, manifest, README, changelog and icon only, and confirm the mandatory standalone dependency in its manifest.
