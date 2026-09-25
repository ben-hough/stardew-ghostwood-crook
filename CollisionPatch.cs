using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;

namespace MrGlim.GhostwoodCrook
{
    /// <summary>
    /// Harmony patch which makes phantom fence tiles solid for farm animals (and optionally pets).
    ///
    /// Target: <c>GameLocation.isCollidingPosition(Rectangle position, xTile.Dimensions.Rectangle viewport, bool isFarmer,
    /// int damagesFarmer, bool glider, Character character, bool pathfinding, bool projectile, bool ignoreCharacterRequirement,
    /// bool skipCollisionEffects)</c>. Every other overload (3-arg and 6-arg) and every location subclass override
    /// (Forest, FarmHouse, IslandLocation, ...) ends up calling this base implementation, and both FarmAnimal/Pet movement
    /// (<c>MovePosition</c>, swimming, hopping, pushes) and <c>PathFindController.findPath</c> go through it.
    /// </summary>
    internal static class CollisionPatch
    {
        /*********
        ** Fields
        *********/
        private static IMonitor Monitor = null!;
        private static Func<ModConfig> GetConfig = null!;
        private static bool LoggedError;


        /*********
        ** Public methods
        *********/
        /// <summary>Apply the patch.</summary>
        public static void Apply(Harmony harmony, IMonitor monitor, Func<ModConfig> getConfig)
        {
            Monitor = monitor;
            GetConfig = getConfig;

            var target = AccessTools.Method(
                typeof(GameLocation),
                nameof(GameLocation.isCollidingPosition),
                new[]
                {
                    typeof(Rectangle), typeof(xTile.Dimensions.Rectangle), typeof(bool), typeof(int), typeof(bool),
                    typeof(Character), typeof(bool), typeof(bool), typeof(bool), typeof(bool)
                }
            );
            if (target == null)
            {
                monitor.Log("Couldn't find GameLocation.isCollidingPosition (10-parameter overload); phantom fences won't block animals.", LogLevel.Error);
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(CollisionPatch), nameof(After_IsCollidingPosition)));
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Postfix: report a collision when a farm animal or pet would move into a phantom fence tile.</summary>
        private static void After_IsCollidingPosition(GameLocation __instance, Rectangle position, Character character, bool pathfinding, ref bool __result)
        {
            // already colliding, or not a character we block (farmers, villagers, monsters, horses, etc. pass freely)
            if (__result || character is null)
                return;
            if (character is not FarmAnimal)
            {
                if (character is not Pet || !GetConfig().BlocksPets)
                    return;
            }

            try
            {
                if (!PhantomFences.HasAny(__instance))
                    return;

                Rectangle area = position;
                Rectangle? ignore = null;

                // For movement checks in the character's own location, ignore phantom tiles the character already overlaps
                // so an animal/pet standing on a newly-placed fence can walk off it instead of getting stuck. We also
                // sweep from the current bounding box to the target area, so a duck can't hop over a phantom tile.
                // Pathfinding checks (arbitrary tiles) and spawn-position checks (area == current box) use the plain area.
                if (!pathfinding && object.ReferenceEquals(character.currentLocation, __instance))
                {
                    Rectangle current = character.GetBoundingBox();
                    if (current != position)
                    {
                        ignore = current;
                        if (Math.Abs(current.Center.X - position.Center.X) <= Game1.tileSize * 3 && Math.Abs(current.Center.Y - position.Center.Y) <= Game1.tileSize * 3)
                            area = Rectangle.Union(current, position);
                    }
                }

                if (PhantomFences.Intersects(__instance, area, ignore))
                    __result = true;
            }
            catch (Exception ex)
            {
                if (!LoggedError)
                {
                    LoggedError = true;
                    Monitor.Log($"Failed checking phantom fence collision (further errors suppressed):\n{ex}", LogLevel.Error);
                }
            }
        }
    }
}
