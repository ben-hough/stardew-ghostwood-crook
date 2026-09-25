using StardewModdingAPI;

namespace MrGlim.GhostwoodCrook
{
    /// <summary>Strongly-typed access to the mod's translations.</summary>
    internal static class I18n
    {
        private static ITranslationHelper? Translations;

        public static void Init(ITranslationHelper translations) => Translations = translations;

        private static string Get(string key) => Translations?.Get(key).Default(key).ToString() ?? key;

        public static string ItemName() => Get("item.name");
        public static string ItemDescription() => Get("item.description");
        public static string RecipeName() => Get("recipe.name");

        public static string MessageTooFar() => Get("message.too-far");
        public static string MessageNoHostMod() => Get("message.no-host-mod");
        public static string MessageLimit() => Get("message.limit");
        public static string MessageOverlayOn() => Get("message.overlay-on");
        public static string MessageOverlayOff() => Get("message.overlay-off");

        public static string ConfigSectionShop() => Get("config.section.shop");
        public static string ConfigPriceName() => Get("config.price.name");
        public static string ConfigPriceDesc() => Get("config.price.desc");
        public static string ConfigRecipeEnabledName() => Get("config.recipe-enabled.name");
        public static string ConfigRecipeEnabledDesc() => Get("config.recipe-enabled.desc");
        public static string ConfigRecipeIngredientsName() => Get("config.recipe-ingredients.name");
        public static string ConfigRecipeIngredientsDesc() => Get("config.recipe-ingredients.desc");
        public static string ConfigSectionUse() => Get("config.section.use");
        public static string ConfigRangeName() => Get("config.range.name");
        public static string ConfigRangeDesc() => Get("config.range.desc");
        public static string ConfigDragName() => Get("config.drag.name");
        public static string ConfigDragDesc() => Get("config.drag.desc");
        public static string ConfigSectionDisplay() => Get("config.section.display");
        public static string ConfigOpacityName() => Get("config.opacity.name");
        public static string ConfigOpacityDesc() => Get("config.opacity.desc");
        public static string ConfigToggleName() => Get("config.toggle.name");
        public static string ConfigToggleDesc() => Get("config.toggle.desc");
        public static string ConfigSectionBehavior() => Get("config.section.behavior");
        public static string ConfigPetsName() => Get("config.pets.name");
        public static string ConfigPetsDesc() => Get("config.pets.desc");
    }
}
