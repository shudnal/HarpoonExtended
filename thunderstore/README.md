# Harpoon Extended
![](https://staticdelivery.nexusmods.com/mods/3667/images/headers/2528_1695185106.jpg)

There is no spoon. There is Harpoon.

And you need to get fun of it. You didn't know you needed this.

You're practically spiderman.

The mod needs further multiplayer testing but it should be pretty good.

## Basic features
* harpoon everything!
* movable objects will be moved towards you and you will be pulled to immovable objects
* retrieve the line to pull harpooned target to you or yourself to target
* cast the line to increase target distance between you and target
* server synced config

### Detailed features
* disable durability usage
* disable damage
* disable stamina usage
* increase max quality and durability per lvl
* apply Feather Fall while harpooning to prevent fall damage
* control projectile gravity and velocity
* control max, min and break distance
* control what objects is targetable
* by default terrain harpooning is disabled. It could be especially handy in mountains and mistlands.
* pulling Leviathans and Bosses was considered cheating and also disabled by default.

You can pull to every target if you hold "Pull to target mode" button when harpoon hits the target. Pls keep in mind it's NOT Shift + attack but Shift must be hold at the moment harpoon hits.

That way you can pull yourself to moving objects. Attaching yourself to objects while pulling (like sitting) will break the line.


For any object that can not be moved you will be pulled to it.


### Besides obvios cases like terrain, rocks and trees there are special cases:
 * birds (seagulls and so). They are scripted to fly.
 * intentional pulling (special hotkey)
 * you can't pull a player closer. For players targeting there is only vanilla behaviour.
 * ship or cart controlled by other player can not be pulled
 * if someone harpooned the target you are harpooning you will start to pull to that target
 * in other words only one player can be pulling targeted object

## Hotkeys
### Default ones (you can't change it) are:
 * pull line - "Use" bind, default is E for keyboard
 * release line - "Crouch" bind + pull line bind, default is Ctrl + E for keyboard
 * stop harpooning - "Block" bind, default is right mouse button
 * Pull To Target mode - "Alternative placing" bind, default is Left Shift for keyboard
### Configurable ones:
 * pull line - T
 * release line - LeftControl + T
 * stop harpoon - LeftShift + LeftControl + T
 * Pull To Target mode - LeftShift

## Known issues
 * you can't pull a player closer. For players targeting there is only vanilla behaviour.

## Potential issues
 * multiplayer may grant weird interactions. It won't break your game but may throw you away. It won't be random though but caused by your actions.

## Configurating
The best way to handle configs is configuration manager. Choose one that works for you:
* https://www.nexusmods.com/site/mods/529
* https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/

To get proper tooltips on config options first open the game menu (Esc) and then Configuration manager window.

## Mirrors
[Nexus](https://www.nexusmods.com/valheim/mods/2528)

## Changelog

v 1.1.6
* slow fall fix

v 1.1.5
* fixed Munchausen pulling-your-ship bug
* added option to not use stamina while attached to a ship
* easier pulling another ship while attached to a ship (it becomes possible to drag another ship)

v 1.1.4
* patch 0.217.24

v 1.1.3
* patch 0.217.22

v 1.1.2
* invisible wall fix

v 1.1.1
* killing harpooned fix

v 1.1.0
* Major physics and config overhaul

v 1.0.0
* Initial release