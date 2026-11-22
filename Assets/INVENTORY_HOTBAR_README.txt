Dead Signal - Inventory / Hotbar

Controls
- E: pick up / interact
- 1, 2, 3: equip item in the diamond slots (9 o'clock, 12 o'clock, 3 o'clock)
- Press the same equipped number again: bare hands
- 0: bare hands
- F: pick up

Rules
- Slots are diamonds in a tight cross. The bottom (6 o'clock) diamond is about 70% size and shows the flashlight AA count (xN, reloaded with R). It is not an equip slot.
- Beacon batteries, the key, the flashlight, and medkits use the three full-size diamonds.
- Picking an item puts it in the first empty general slot; it is not equipped automatically.
- A beacon battery must be equipped (1/2/3) before it can be inserted into an escape device.
- The key must be equipped before the safe can be opened or closed.
- Inserting a battery consumes that battery and frees its slot.
- The key is not consumed by the safe.
- Dropped items can be picked up again with F.
- The selected slot's frame turns gold.

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
