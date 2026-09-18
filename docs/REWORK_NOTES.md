# Deferred harpoon and rope rework

These are design notes, not features of the maintenance release. Do not merge the experimental `bogwitch` implementation as a shortcut to a release.

## Shared connection model

A link should be independent of its firing projectile, player, and rendered rope. Represent two identified endpoints with explicit local anchors, length, mode, permissions and lifetime. Persistent world links need save/load behavior, sector unload/reload handling, late-join state and stable link identifiers.

Physical state must not be read back from LineRenderer. Preserve tangential velocity for swinging and treat passive restraint, reeling and burst pulling as separate behaviors. Use point-relative velocity, including angular motion, and process several links acting on one body coherently.

The owner of each physical body should apply the force. Control and validated movement requests may be sent to that owner by RPC; a link should not need to steal ownership. Ownership changes must preserve the selected link meaning. Dynamic-to-dynamic links require an explicit coordination policy before implementation.

## Planned applications

* Harpoon arrows for bows and harpoon bolts for crossbows, using the weapon/ammunition profile captured at firing time.
* Rope arrows that leave an interactable world rope, with attachment, climbing/reeling and swinging. Define collision/obstruction behavior rather than drawing through obstacles silently.
* Two-step attachment: hit an NPC, then a tree or another anchor, and transfer the player's endpoint to it.
* Ballista harpoon bolts with target tracking and a bounded escape reaction. A restrained NPC should try to escape rather than attack its restraining ballista; avoid a global AI flee override or global ballista immunity.
* Fast self-pulling and fast object retrieval as distinct controlled modes, with bounded acceleration and collision-aware stopping rather than arbitrary large impulses.
* Quality-dependent attachment materials, unlocked capabilities, link statistics and per-level recipes. Material policy must distinguish attachment surfaces from movable targets; account for new wood material types such as Timberwood.
* Surface-dependent impact sounds reusing vanilla assets without inflicting a second artificial hit. Keep this separate from projectile hit completion.

## Low-priority application

Tombstone towing: keep inventory and grave ownership intact. TombStone.PositionCheck resets graves moved more than four meters in XZ from the stored spawn point and also prevents underground positions. Any towing mode must retain the latter protection, define permission and disconnect behavior, and record a safe final location.

## Useful historical work

`bogwitch` contains exploratory Harpooned/HarpoonTarget/TargetState classes, turret ammunition, multiple links per target, tracking and interaction controls. It is useful as requirements history, not as a finished network or physics implementation. The old and experimental paths coexist there, and its Pull function still follows the legacy approach.
