Dead Signal - 3 Slot Inventory / Hotbar Update

Controls
- E: pick up / interact
- 1, 2, 3: equip item in inventory slot 1/2/3
- Press the same equipped number again: bare hands
- 0: bare hands
- F: throw/drop the currently equipped item forward

Rules
- Batteries and the key occupy one slot each, up to 3 slots total.
- Picking an item puts it in the first empty slot; it is not equipped automatically.
- A battery must be equipped before it can be inserted into an escape device.
- The key must be equipped before the safe can be opened or closed.
- Inserting a battery consumes that battery and frees its slot.
- The key is not consumed by the safe.
- Dropped items can be picked up again with E.
- The selected slot is highlighted in the bottom-left inventory UI.

Run Assets/Scenes/Integrated_Week6.unity.

Flashlight item (v34)
- A flashlight is given at game start and occupies one slot (shown as "LIGHT").
- Equip its slot (1/2/3), then hold Right Mouse to blind the monster. Bare hands / other items: no effect.
- After a successful stun the flashlight has a 3s cooldown, shown on its slot (dark fill + seconds).
- F drops it, E picks it up again. The cooldown is stored on the flashlight object, so drop/pick-up does not reset it.

v35 changes
- E interaction now has a small aim assist (SphereCast radius 0.18m, distance 3.2m). Objects behind walls are never selected.
- Eye penalty (F6) no longer blacks out half the screen: it darkens the damaged-side edge, adds a soft vignette and mild distance blur.
  While the eye is damaged the flashlight screen area shrinks from 80% to 68%.
