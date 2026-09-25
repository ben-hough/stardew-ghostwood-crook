using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xna.Framework;
using StardewValley;

namespace MrGlim.GhostwoodCrook
{
    /// <summary>The result of trying to change a phantom fence tile.</summary>
    internal enum EditResult
    {
        Changed,
        NoChange,
        LimitReached,
        Invalid
    }

    /// <summary>
    /// Reads and writes phantom fence tiles stored in <see cref="GameLocation.modData"/>.
    /// The mod data string is the single source of truth: it's saved with the location and synced by the
    /// game from the host to farmhands. Only the host ever writes it.
    /// </summary>
    internal static class PhantomFences
    {
        /*********
        ** Fields
        *********/
        /// <summary>The mod data key which stores the serialized tiles ("x,y;x,y;...").</summary>
        public const string ModDataKey = "MrGlim.GhostwoodCrook/Tiles";

        /// <summary>A parsed tile set cached for a location, keyed to the raw string it was parsed from.</summary>
        private sealed class CacheEntry
        {
            public string? Raw;
            public HashSet<Point> Tiles = new();
        }

        /// <summary>Parsed tile sets by location. Weak keys so unloaded locations (e.g. mine levels) are collected.</summary>
        private static readonly ConditionalWeakTable<GameLocation, CacheEntry> Cache = new();

        /// <summary>An empty tile set.</summary>
        private static readonly HashSet<Point> Empty = new();


        /*********
        ** Public methods
        *********/
        /// <summary>Get whether a location has any phantom fence tiles (fast path for collision checks).</summary>
        public static bool HasAny(GameLocation? location)
        {
            return location != null
                && location.modData.TryGetValue(ModDataKey, out string? raw)
                && !string.IsNullOrEmpty(raw);
        }

        /// <summary>Get the phantom fence tiles in a location. The returned set must not be modified.</summary>
        public static HashSet<Point> GetTiles(GameLocation? location)
        {
            if (location == null || !location.modData.TryGetValue(ModDataKey, out string? raw) || string.IsNullOrEmpty(raw))
                return Empty;

            CacheEntry entry = Cache.GetValue(location, _ => new CacheEntry());
            if (!object.ReferenceEquals(entry.Raw, raw))
            {
                if (entry.Raw != raw)
                    entry.Tiles = Parse(raw);
                entry.Raw = raw;
            }
            return entry.Tiles;
        }

        /// <summary>Get whether a tile has a phantom fence.</summary>
        public static bool Has(GameLocation? location, Point tile)
        {
            return GetTiles(location).Contains(tile);
        }

        /// <summary>Get whether a pixel rectangle overlaps any phantom fence tile, ignoring tiles overlapped by <paramref name="ignoreArea"/>.</summary>
        /// <param name="location">The location to check.</param>
        /// <param name="area">The pixel area to check.</param>
        /// <param name="ignoreArea">If set, phantom tiles which this pixel area already overlaps are ignored (so a character standing on one can walk off it).</param>
        public static bool Intersects(GameLocation location, Rectangle area, Rectangle? ignoreArea)
        {
            HashSet<Point> tiles = GetTiles(location);
            if (tiles.Count == 0 || area.Width <= 0 || area.Height <= 0)
                return false;

            int left = FloorDiv(area.Left, Game1.tileSize);
            int right = FloorDiv(area.Right - 1, Game1.tileSize);
            int top = FloorDiv(area.Top, Game1.tileSize);
            int bottom = FloorDiv(area.Bottom - 1, Game1.tileSize);

            // sanity cap in case a caller passes a huge rectangle
            if (right - left > 16 || bottom - top > 16)
                return false;

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    if (!tiles.Contains(new Point(x, y)))
                        continue;
                    if (ignoreArea.HasValue && TileOverlaps(ignoreArea.Value, x, y))
                        continue;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Add or remove a phantom fence tile. This should only be called on the host.</summary>
        /// <param name="location">The location to edit.</param>
        /// <param name="tile">The tile to edit.</param>
        /// <param name="add">Whether to add (true) or remove (false) the tile.</param>
        /// <param name="maxTiles">The maximum number of tiles allowed in the location.</param>
        public static EditResult Edit(GameLocation location, Point tile, bool add, int maxTiles)
        {
            if (!StardewModdingAPI.Context.IsWorldReady || !StardewModdingAPI.Context.IsMainPlayer)
                return EditResult.Invalid;
            if (!location.isTileOnMap(tile.X, tile.Y))
                return EditResult.Invalid;

            HashSet<Point> current = GetTiles(location);
            if (current.Contains(tile) == add)
                return EditResult.NoChange;
            if (add && current.Count >= Math.Max(1, maxTiles))
                return EditResult.LimitReached;

            HashSet<Point> updated = new(current);
            if (add)
                updated.Add(tile);
            else
                updated.Remove(tile);

            if (updated.Count == 0)
                location.modData.Remove(ModDataKey);
            else
                location.modData[ModDataKey] = Serialize(updated);
            return EditResult.Changed;
        }

        /// <summary>Remove all phantom fence tiles from a location (host only).</summary>
        public static int Clear(GameLocation location)
        {
            int count = GetTiles(location).Count;
            location.modData.Remove(ModDataKey);
            return count;
        }

        /// <summary>Serialize a tile set to the compact mod data format.</summary>
        public static string Serialize(IEnumerable<Point> tiles)
        {
            StringBuilder str = new();
            foreach (Point tile in tiles.OrderBy(p => p.Y).ThenBy(p => p.X))
            {
                if (str.Length > 0)
                    str.Append(';');
                str.Append(tile.X.ToString(CultureInfo.InvariantCulture)).Append(',').Append(tile.Y.ToString(CultureInfo.InvariantCulture));
            }
            return str.ToString();
        }

        /// <summary>Parse the compact mod data format into a tile set, skipping invalid entries.</summary>
        public static HashSet<Point> Parse(string? raw)
        {
            HashSet<Point> tiles = new();
            if (string.IsNullOrWhiteSpace(raw))
                return tiles;

            foreach (string entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int comma = entry.IndexOf(',');
                if (comma <= 0)
                    continue;
                if (int.TryParse(entry.AsSpan(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    && int.TryParse(entry.AsSpan(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                    tiles.Add(new Point(x, y));
            }
            return tiles;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get whether a pixel area overlaps the given tile.</summary>
        private static bool TileOverlaps(Rectangle area, int tileX, int tileY)
        {
            return area.Right > tileX * Game1.tileSize
                && area.Left < (tileX + 1) * Game1.tileSize
                && area.Bottom > tileY * Game1.tileSize
                && area.Top < (tileY + 1) * Game1.tileSize;
        }

        /// <summary>Integer division which rounds toward negative infinity.</summary>
        private static int FloorDiv(int value, int divisor)
        {
            int result = value / divisor;
            if ((value % divisor != 0) && ((value < 0) != (divisor < 0)))
                result--;
            return result;
        }
    }
}
