# Scrapfire gameplay code

## Inventory and equipment

Each CharacterPlayer owns an Inventory component and its independent InventoryContainer.
The default layout is two weapon slots, one armor slot and twelve shared backpack slots.
Ammo, healing items, supplies, consumables and attachments compete for the same general slots.
Explicit backpack upgrades stop at 24 slots. Pickups never auto-expand storage.

Item and WeaponData are shared definitions. ItemStack owns instance quantity, durability and
magazine state. Inventory operations validate ownership and preserve state on moving, dropping
and transferring items. CharacterPlayer owns selected weapon state and held models;
ItemUser dispatches shared use strategies and consumes items only after a successful effect.
The inventory's Changed event is ready for future UI. No inventory UI is generated.

SampleScene starts with one rifle, 60 reserve rounds and two medical kits. Reloading consumes
that player's finite backpack ammo; switching weapons does not refill a magazine.

See [Inventory.md](Inventory.md) for layout, APIs, transaction rules and configuration.

## Other gameplay modules

- [Weapons.md](Weapons.md): firing, ammunition, spread, reload and presentation.
- [EnemyAI.md](EnemyAI.md): perception, state machine, alerts, seek/pursue and dynamic navigation.
- [Survival.md](Survival.md): balances, regions, waves, spawning, attributes and building cutaways.
- [Controls.md](Controls.md): current controls and reserved actions.
- [TODO.md](TODO.md): later work.

Presentation pools remain scene-owned. Enemy behaviour AI is implemented; multiplayer transport is deferred.
