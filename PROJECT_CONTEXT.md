# Harpoon Extended maintenance context

## Baselines and scope

Maintenance branch: `fix/valheim-1.0-maintenance`, based on `master` at `53b917c0a28be08c914ecad9933a57dc8df99689`.

Selected maintenance ideas from `zdo-fix` (`42d5f020f4d7a9436eda56daf54ea3bece15c873`) are incorporated: framework/reference modernization, assembly metadata, parent-component resolution, and creating a rope only after participant validation. The `bogwitch` experiment is not merged.

Game reference: `shudnal/assemblies_combined`, commit `d1374bfd9175ac8f733ae483b0a06e5c8b75906e`, whose Version.cs declares 1.0.15. Relevant sources are `Projectile.cs`, `LineConnect.cs`, `SEMan.cs`, `StatusEffect.cs`, `SE_Stats.cs`, `ZNetScene.cs`, `GrapplingPoint.cs` and `Piece.cs`.

## Deliberately unchanged behavior

* The legacy Pull formula, impulse/velocity-change selection, mass factors, force-point interpolation, rotation handling and whole-body velocity clamp remain unchanged. Rigidbody.velocity access is updated to Rigidbody.linearVelocity.
* Initial line length still uses the hit point. Break/close-distance checks still use the existing root-body endpoints. The midpoint used by Pull is not changed.
* No new upper limit on line payout is introduced.
* Losing ownership of an otherwise valid target retains the connection and switches to self-pulling. Initial ownership acquisition and the existing Ship.UpdateOwner policy remain in place.
* Cumulative ground time for fall-effect removal is intentional and is not reset by short airborne intervals.
* No GrapplingHook movement, jump activation, FOV, animation, sound or balance changes are introduced.

## Implementation boundaries

`HarpoonExtended.cs` keeps configuration, the single local connection, legacy pulling and existing item behavior. Partial-class files isolate projectile/filter handling and connection validation; they do not introduce the experimental multi-link system.

`HarpoonProjectilePatches.cs` classifies attachment in the hit prefix. It suppresses vanilla Harpooned when attachment is forbidden or replaced, while retaining the original hit handling. The postfix creates a connection only after accepted, non-bouncing impact and only with the originally captured target. A finalizer restores the status hash. Item-layer mask changes wrap the individual harpoon FixedUpdate collision-query call and are restored by a finalizer; they are never held from Awake until impact.

`HarpoonValidation.cs` validates the actual captured Unity objects, required components and finite state before physics and before rendering. A missing target ZNetView is allowed if the target was non-networked at attachment; losing a previously present view is not treated as an ownership change. No replacement participants or live rigidbody fallback are selected. Invalid state clears the connection and removes its visual/network object when locally owned. The separate active flag ensures cleanup still runs when Unity has already destroyed the rope GameObject. The cached rope ZDOID permits retirement of owned orphan network data without retaining a pooled ZDO reference. Remote replicas are removed locally without claiming a participant's ownership.

`HarpoonLine.cs` only tracks presentation/lifetime. Tagged rope ZDOs select the custom renderer on observers. It resolves the exact peer once, follows its VisEquipment left hand, and uses LineConnect's existing local-space geometry, slack and thickness. Failure does not attempt to rebind the rope. Remote clients do not run the pulling formula.

`HarpoonSlowFall.cs` clones the SlowFall ScriptableObject with a distinct internal name/hash. Icon, display text, TTL and other effect fields are preserved. Only the live new instance's maximum downward speed and fall-damage modifier change. Config value 0 maps to modifier -1 because SE_Stats applies an additive fraction of base damage. Existing vanilla effects are neither mutated nor removed. An already active custom effect is not reconfigured. Breaking a rope leaves both pending protection and existing protection to the original removal policy.

CCS is a standalone hard dependency. ModRequired is true; the fixed requirement policy is the CCS default. Gameplay entries use AlwaysServerControlled; presentation and controls retain local ownership. Minimum compatible mod version is 1.1.13 for this new mandatory installation contract. CCS is not bundled or embedded.

## Validation status

No mod build, automated runtime tests, game launch or multiplayer test was performed. Static review covers referenced API signatures, connection exits, source structure, metadata consistency, dependency removal and accidental Cyrillic outside localization. User-side scenarios are recorded in `docs/MANUAL_VERIFICATION.md`.

## Deferred work

See `docs/REWORK_NOTES.md`. The notes preserve decisions and ideas only; they are not implemented by this maintenance branch.
