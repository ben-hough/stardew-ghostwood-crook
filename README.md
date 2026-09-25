# Ghostwood Crook

*A Stardew Valley 1.6 SMAPI mod by MrGlim* — `MrGlim.GhostwoodCrook` v1.0.0

**Download:** [Ghostwood Crook on Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/52912)

> **Ghostwood Crook** — *A shepherd's crook carved from pale ghostwood. Livestock heed the fences it draws, though no one else can see them.*

The Ghostwood Crook lets you lay down **phantom fences**: invisible fence tiles that keep your farm animals (and, by default, pets) in, while you and other farmers walk straight through them. No gates to open, no fence posts ruining your farm's look.

## Getting the crook
* Buy it from **Marnie's Ranch** for **2,500g** (price configurable; set `Price` to `0` to remove it from the shop).
* Optional crafting recipe (off by default): set `EnableCraftingRecipe` to `true` in `config.json`. Default ingredients are 10 Hardwood + 5 Void Essence (`"709 10 769 5"`). The recipe is learned automatically on the next day/save load.

## Using it
1. Put the crook in your hotbar and **select it** (make it your held item).
2. While you hold it, every phantom fence in the area appears as a faint, pulsing, ghostly fence, and the tile under your cursor is highlighted:
   * **Green**: clicking will *place* a phantom fence.
   * **Red**: clicking will *remove* the phantom fence there.
   * **Gray**: too far away. By default you can reach 1 tile around you, like a tool (`Range` in the config).
3. Press the **use-tool button** (left click / `C` / controller `X`) on a tile to place or remove a fence piece.
4. **Hold and drag** (keep the button held while you move the mouse or walk) to paint a whole line. Whatever the first tile did (place or remove), the drag keeps doing on each tile you pass over.
5. While you're holding the crook, the button's normal behavior (placing, stowing, left-click interactions) is turned off, so it won't do anything else by accident.

Only the player holding the crook sees the fences, and nothing is drawn for anyone else. To see them without the crook, press **`LeftShift + G`** (configurable) to toggle "always show".

### What the fences block
| Who | Blocked? |
|---|---|
| Farm animals (chickens, cows, ducks, pigs, modded animals...) | **Yes**, just like a regular fence, both when they wander and when they pathfind (going out to graze or heading home through the barn door). Ducks can't hop over them either. |
| Pets (cats, dogs, turtles...) | **Yes** by default (`BlocksPets`). |
| Farmers (you and other players) | No, you walk right through them. |
| Villagers, monsters, horses, Junimos | No. |

The fences work anywhere, including inside barns and coops. If you put a fence on a tile an animal is standing on, it can still walk *off* that tile (so it can't get stuck), but it won't walk back in. If you fence off the tile in front of a barn/coop animal door, the animals won't go out (same as a regular fence).

## Config (`config.json`, or Generic Mod Config Menu if installed)
| Setting | Default | Description |
|---|---|---|
| `Price` | `2500` | Price at Marnie's. `0` removes it from the shop. |
| `EnableCraftingRecipe` | `false` | Adds a crafting recipe. |
| `CraftingRecipeIngredients` | `"709 10 769 5"` | Recipe ingredients (`id count id count ...`). |
| `Range` | `1` | How far (in tiles) from your tile you can edit (0-10). |
| `DragToPaint` | `true` | Hold the button to paint or erase multiple tiles. |
| `OverlayOpacity` | `0.45` | Opacity of the ghost overlay (0.05-1). |
| `BlocksPets` | `true` | Phantom fences also block pets (the host's setting is what counts). |
| `ToggleAlwaysShowKey` | `LeftShift + G` | Toggles showing the fences without holding the crook. |
| `MaxTilesPerLocation` | `5000` | Safety cap per location (enforced by the host). |

## Multiplayer
* **The host must have the mod installed.** The host runs the farm animal and pet AI, so the host's copy is what actually blocks animals, and the host is the only one who saves fence data.
* **Every player who wants to see or edit fences should install it too.** Farmhands without the mod can still play normally. Animals are still blocked for everyone (because the host simulates them), but those farmhands can't see or edit fences, and anyone who has the crook in their inventory will see it as an error item.
* How it works: each location stores its fence tiles in its save data (`modData["MrGlim.GhostwoodCrook/Tiles"]`, formatted as `x,y;x,y;...`). The game saves this with the farm and syncs it from the host to farmhands automatically. When a farmhand clicks, their game sends a small SMAPI message to the host. The host checks it (right location, within reach) and applies the change, and it then syncs back to everyone. The farmhand sees their change right away while it waits for the host.
* If a farmhand has the mod but the host doesn't, the farmhand gets a message saying they can't edit fences.

## Console commands (SMAPI console)
* `ghostwood_count`: shows how many phantom fence tiles are in your current location.
* `ghostwood_clear`: removes all phantom fence tiles in your current location (host only).

## Uninstalling
Phantom fence data is just a string in each location's mod data, so removing the mod is harmless: fences stop working and the data is ignored. Any Ghostwood Crooks in inventories or chests will turn into error items, so sell or trash them first if you like.

## Technical notes
* Harmony postfix on `GameLocation.isCollidingPosition(Rectangle, xTile.Dimensions.Rectangle, bool isFarmer, int damagesFarmer, bool glider, Character character, bool pathfinding, bool projectile, bool ignoreCharacterRequirement, bool skipCollisionEffects)`. All other overloads and all location subclass overrides (Forest, FarmHouse, IslandLocation, Desert, etc.) call this base method. It only makes a collision happen when `character` is a `FarmAnimal` (or a `Pet` if enabled). It never removes one.
* The item is a normal `Data/Objects` entry (`(O)MrGlim.GhostwoodCrook`, context tag `not_placeable`, can't be gifted) added to `Data/Shops` → `AnimalShop`.

## Building
Requires the .NET 6 SDK (or newer, targeting `net6.0`). By default the project references the game from `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley`. Override that with `dotnet build -c Release -p:GamePath="D:\path\to\Stardew Valley"`. Output goes to `bin/Release`. Copy `manifest.json`, `GhostwoodCrook.dll`, `assets/` and `i18n/` into `Mods/GhostwoodCrook/`.

The pixel art can be regenerated with `python tools/generate_art.py` (needs Pillow).

## AI disclosure

This mod was made with a lot of help from generative AI. The C# code was written by an AI coding assistant under the author's direction, and the 16x16 sprites are drawn procedurally by a small Python script (`tools/generate_art.py`) that was also AI-written. No image-generation model was used. The Nexus page is tagged **AI-Generated Content** and **AI Media**.

## License

MIT. See [LICENSE](LICENSE).
