using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace MrGlim.GhostwoodCrook
{
    /// <summary>The mod configuration (config.json).</summary>
    internal sealed class ModConfig
    {
        /// <summary>Price at Marnie's shop (AnimalShop). 0 or less removes it from the shop.</summary>
        public int Price { get; set; } = 2500;

        /// <summary>Whether to add a crafting recipe for the crook.</summary>
        public bool EnableCraftingRecipe { get; set; } = false;

        /// <summary>Recipe ingredients in vanilla format ("id count id count ...").</summary>
        public string CraftingRecipeIngredients { get; set; } = "709 10 769 5";

        /// <summary>How many tiles from the farmer's tile you can edit (Chebyshev distance).</summary>
        public int Range { get; set; } = 1;

        /// <summary>Whether holding the use-tool button paints/erases multiple tiles.</summary>
        public bool DragToPaint { get; set; } = true;

        /// <summary>Overlay opacity (0-1).</summary>
        public float OverlayOpacity { get; set; } = 0.45f;

        /// <summary>Whether phantom fences also block pets.</summary>
        public bool BlocksPets { get; set; } = true;

        /// <summary>Keybind which toggles always showing the overlay.</summary>
        public KeybindList ToggleAlwaysShowKey { get; set; } = KeybindList.Parse("LeftShift + G");

        /// <summary>Maximum phantom fence tiles per location (host-enforced).</summary>
        public int MaxTilesPerLocation { get; set; } = 5000;
    }
}
