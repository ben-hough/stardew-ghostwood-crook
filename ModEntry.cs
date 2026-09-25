using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;

namespace MrGlim.GhostwoodCrook
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        /*********
        ** Constants
        *********/
        /// <summary>The unqualified item ID.</summary>
        public const string ItemId = "MrGlim.GhostwoodCrook";

        /// <summary>The qualified item ID.</summary>
        public const string QualifiedItemId = "(O)" + ItemId;

        /// <summary>The asset name for the item texture.</summary>
        private const string TextureAssetName = "Mods/MrGlim.GhostwoodCrook/Crook";

        /// <summary>How long (in ticks) a farmhand's unconfirmed edit is shown optimistically.</summary>
        private const int PendingTicks = 120;


        /*********
        ** Fields
        *********/
        private ModConfig Config = new();
        private Texture2D Pixel = null!;
        private Texture2D PostTexture = null!;

        /// <summary>The active paint (click/drag) operation for each local screen.</summary>
        private readonly PerScreen<PaintState?> Paint = new();

        /// <summary>Whether the overlay is always shown for each local screen.</summary>
        private readonly PerScreen<bool> AlwaysShow = new();

        /// <summary>Unconfirmed farmhand edits shown optimistically until the host's mod data sync arrives.</summary>
        private readonly PerScreen<Dictionary<(string Location, Point Tile), PendingEdit>> Pending = new(() => new());

        /// <summary>The last tick a HUD warning was shown, to avoid spamming messages while dragging.</summary>
        private readonly PerScreen<int> LastWarningTick = new(() => -1000);


        /*********
        ** Public methods
        *********/
        public override void Entry(IModHelper helper)
        {
            I18n.Init(helper.Translation);
            this.Config = helper.ReadConfig<ModConfig>();
            this.NormalizeConfig();

            CollisionPatch.Apply(new Harmony(this.ModManifest.UniqueID), this.Monitor, () => this.Config);

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.ResetScreenState();
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.Content.LocaleChanged += (_, _) => this.InvalidateData();
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
            helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
            helper.Events.Player.Warped += this.OnWarped;

            helper.ConsoleCommands.Add("ghostwood_count", "Shows how many phantom fence tiles are in your current location.", this.OnCountCommand);
            helper.ConsoleCommands.Add("ghostwood_clear", "Removes every phantom fence tile in your current location (host only).", this.OnClearCommand);
        }


        /*********
        ** Private methods: events
        *********/
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.Pixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            this.Pixel.SetData(new[] { Color.White });
            this.PostTexture = this.Helper.ModContent.Load<Texture2D>("assets/phantom_post.png");

            this.RegisterConfigMenu();
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(TextureAssetName))
            {
                e.LoadFromModFile<Texture2D>("assets/crook.png", AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, ObjectData>().Data;
                    data[ItemId] = new ObjectData
                    {
                        Name = ItemId,
                        DisplayName = I18n.ItemName(),
                        Description = I18n.ItemDescription(),
                        Type = "Basic",
                        Category = 0,
                        Price = 0,
                        Texture = TextureAssetName,
                        SpriteIndex = 0,
                        Edibility = -300,
                        CanBeGivenAsGift = false,
                        CanBeTrashed = true,
                        ExcludeFromFishingCollection = true,
                        ExcludeFromShippingCollection = true,
                        ExcludeFromRandomSale = true,
                        ContextTags = new List<string> { "not_placeable", "ghostwood_crook_item" }
                    };
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            {
                if (this.Config.Price <= 0)
                    return;

                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, ShopData>().Data;
                    if (!data.TryGetValue("AnimalShop", out ShopData? shop))
                    {
                        this.Monitor.Log("Couldn't find Marnie's shop (AnimalShop) in Data/Shops; the Ghostwood Crook won't be sold there.", LogLevel.Warn);
                        return;
                    }

                    shop.Items ??= new List<ShopItemData>();
                    shop.Items.RemoveAll(p => p?.Id == ItemId);
                    shop.Items.Add(new ShopItemData
                    {
                        Id = ItemId,
                        ItemId = QualifiedItemId,
                        Price = this.Config.Price,
                        IgnoreShopPriceModifiers = true
                    });
                }, AssetEditPriority.Late);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                if (!this.Config.EnableCraftingRecipe || string.IsNullOrWhiteSpace(this.Config.CraftingRecipeIngredients))
                    return;

                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    // ingredients / unused / yield / big craftable / unlock condition / display name
                    data[ItemId] = $"{this.Config.CraftingRecipeIngredients.Trim()}/Home/{ItemId}/false/default/{I18n.RecipeName()}";
                });
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // toggle always-show overlay
            if (Context.IsPlayerFree && this.Config.ToggleAlwaysShowKey.JustPressed())
            {
                this.AlwaysShow.Value = !this.AlwaysShow.Value;
                Game1.addHUDMessage(new HUDMessage(this.AlwaysShow.Value ? I18n.MessageOverlayOn() : I18n.MessageOverlayOff(), HUDMessage.newQuest_type));
                this.Helper.Input.SuppressActiveKeybinds(this.Config.ToggleAlwaysShowKey);
                return;
            }

            // use the crook
            if (!e.Button.IsUseToolButton() || !IsHoldingCrook() || !Context.IsPlayerFree || Game1.player.UsingTool)
                return;

            // suppress the normal use-tool handling (stowing, placing, left-click interactions) until the button is released
            this.Helper.Input.Suppress(e.Button);

            GameLocation location = Game1.currentLocation;
            Point tile = this.GetTargetTile();
            if (!this.IsInRange(tile))
            {
                Game1.playSound("cancel");
                this.Warn(I18n.MessageTooFar());
                return;
            }

            bool add = !this.EffectiveHas(location, tile);
            PaintState paint = new(e.Button, location, add);
            paint.Visited.Add(tile);
            this.ApplyEdit(location, tile, add, playSound: true);

            this.Paint.Value = this.Config.DragToPaint ? paint : null;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // continue a drag
            PaintState? paint = this.Paint.Value;
            if (paint != null)
            {
                bool stillHeld = this.Helper.Input.IsSuppressed(paint.Button) || this.Helper.Input.IsDown(paint.Button);
                if (!stillHeld || !IsHoldingCrook() || !Context.IsPlayerFree || !object.ReferenceEquals(Game1.currentLocation, paint.Location))
                {
                    this.Paint.Value = null;
                }
                else
                {
                    Point tile = this.GetTargetTile();
                    if (this.IsInRange(tile) && paint.Visited.Add(tile) && this.EffectiveHas(paint.Location, tile) != paint.Add)
                        this.ApplyEdit(paint.Location, tile, paint.Add, playSound: true);
                }
            }

            // expire optimistic edits
            if (e.IsMultipleOf(30))
            {
                var pending = this.Pending.Value;
                if (pending.Count > 0)
                {
                    foreach (var key in pending.Where(p => p.Value.ExpiresAt <= Game1.ticks).Select(p => p.Key).ToArray())
                        pending.Remove(key);
                }
            }
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (e.IsLocalPlayer)
                this.Paint.Value = null;
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != this.ModManifest.UniqueID || e.Type != EditMessage.MessageType || !Context.IsMainPlayer)
                return;

            EditMessage? message;
            try
            {
                message = e.ReadAs<EditMessage>();
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Ignored malformed phantom fence message from player {e.FromPlayerID}: {ex.Message}", LogLevel.Trace);
                return;
            }
            if (message == null || string.IsNullOrWhiteSpace(message.LocationName))
                return;

            GameLocation? location = Game1.getLocationFromName(message.LocationName, message.IsStructure);
            Farmer? who = Game1.GetPlayer(e.FromPlayerID, onlyOnline: true);
            if (location == null || who == null)
            {
                this.Monitor.Log($"Ignored phantom fence edit from player {e.FromPlayerID}: unknown location '{message.LocationName}' or player.", LogLevel.Trace);
                return;
            }

            // validate the farmhand is actually there and close enough (with a little slack for network lag)
            Point tile = new(message.X, message.Y);
            Point farmerTile = who.TilePoint;
            int reach = this.Config.Range + 3;
            if (who.currentLocation?.NameOrUniqueName != location.NameOrUniqueName
                || Math.Abs(farmerTile.X - tile.X) > reach
                || Math.Abs(farmerTile.Y - tile.Y) > reach)
            {
                this.Monitor.Log($"Rejected phantom fence edit from {who.Name} at {location.NameOrUniqueName} ({tile.X}, {tile.Y}): not in range.", LogLevel.Trace);
                return;
            }

            EditResult result = PhantomFences.Edit(location, tile, message.Add, this.Config.MaxTilesPerLocation);
            if (result == EditResult.LimitReached)
                this.Monitor.Log($"Rejected phantom fence edit from {who.Name}: {location.NameOrUniqueName} reached the limit of {this.Config.MaxTilesPerLocation} tiles.", LogLevel.Debug);
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null || this.Pixel == null)
                return;

            bool holding = IsHoldingCrook();
            if (!holding && !this.AlwaysShow.Value)
                return;

            GameLocation location = Game1.currentLocation;
            SpriteBatch b = e.SpriteBatch;
            HashSet<Point> tiles = this.GetEffectiveTiles(location);

            double time = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(time / 450.0);
            float alpha = MathHelper.Clamp(this.Config.OverlayOpacity, 0.05f, 1f) * pulse;
            Color ghost = new(190, 245, 245);

            // draw phantom fence tiles in view
            if (tiles.Count > 0)
            {
                int minX = Game1.viewport.X / Game1.tileSize - 1;
                int minY = Game1.viewport.Y / Game1.tileSize - 1;
                int maxX = (Game1.viewport.X + Game1.viewport.Width) / Game1.tileSize + 1;
                int maxY = (Game1.viewport.Y + Game1.viewport.Height) / Game1.tileSize + 1;

                foreach (Point tile in tiles)
                {
                    if (tile.X < minX || tile.X > maxX || tile.Y < minY || tile.Y > maxY)
                        continue;

                    Vector2 pos = Game1.GlobalToLocal(Game1.viewport, new Vector2(tile.X * Game1.tileSize, tile.Y * Game1.tileSize));
                    int x = (int)pos.X;
                    int y = (int)pos.Y;

                    // faint tile wash
                    b.Draw(this.Pixel, new Rectangle(x, y, Game1.tileSize, Game1.tileSize), ghost * (alpha * 0.18f));

                    // rails to neighbours (right and down, so each connection is drawn once)
                    if (tiles.Contains(new Point(tile.X + 1, tile.Y)))
                    {
                        b.Draw(this.Pixel, new Rectangle(x + 32, y + 22, Game1.tileSize, 5), ghost * alpha);
                        b.Draw(this.Pixel, new Rectangle(x + 32, y + 42, Game1.tileSize, 5), ghost * alpha);
                    }
                    if (tiles.Contains(new Point(tile.X, tile.Y + 1)))
                    {
                        b.Draw(this.Pixel, new Rectangle(x + 24, y + 40, 5, Game1.tileSize), ghost * alpha);
                        b.Draw(this.Pixel, new Rectangle(x + 36, y + 40, 5, Game1.tileSize), ghost * alpha);
                    }

                    // ghostly post
                    b.Draw(this.PostTexture, new Vector2(x, y), null, Color.White * alpha, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 1f);
                }
            }

            // placement preview under the cursor
            if (holding && Context.IsPlayerFree && Game1.activeClickableMenu == null)
            {
                Point target = this.GetTargetTile();
                Color color;
                if (!this.IsInRange(target))
                    color = Color.Gray;
                else
                {
                    PaintState? paint = this.Paint.Value;
                    bool willAdd = paint?.Add ?? !tiles.Contains(target);
                    color = willAdd ? Color.LimeGreen : Color.Red;
                }

                Vector2 pos = Game1.GlobalToLocal(Game1.viewport, new Vector2(target.X * Game1.tileSize, target.Y * Game1.tileSize));
                Rectangle area = new((int)pos.X, (int)pos.Y, Game1.tileSize, Game1.tileSize);
                b.Draw(this.Pixel, area, color * 0.25f);
                this.DrawBorder(b, area, 3, color * 0.8f);
            }
        }


        /*********
        ** Private methods: console commands
        *********/
        private void OnCountCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Info);
                return;
            }
            GameLocation location = Game1.currentLocation;
            this.Monitor.Log($"{location.NameOrUniqueName} has {PhantomFences.GetTiles(location).Count} phantom fence tile(s).", LogLevel.Info);
        }

        private void OnClearCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady || !Context.IsMainPlayer)
            {
                this.Monitor.Log("This command can only be used by the host in a loaded save.", LogLevel.Info);
                return;
            }
            GameLocation location = Game1.currentLocation;
            int count = PhantomFences.Clear(location);
            this.Monitor.Log($"Removed {count} phantom fence tile(s) from {location.NameOrUniqueName}.", LogLevel.Info);
        }


        /*********
        ** Private methods: logic
        *********/
        /// <summary>Get whether the local player is holding the Ghostwood Crook.</summary>
        private static bool IsHoldingCrook()
        {
            return Game1.player?.ActiveObject?.QualifiedItemId == QualifiedItemId;
        }

        /// <summary>Get the tile the local player is targeting with the crook.</summary>
        private Point GetTargetTile()
        {
            ICursorPosition cursor = this.Helper.Input.GetCursorPosition();
            Vector2 tile = Game1.wasMouseVisibleThisFrame && Game1.mouseCursorTransparency > 0f
                ? cursor.Tile
                : cursor.GrabTile;
            return new Point((int)tile.X, (int)tile.Y);
        }

        /// <summary>Get whether a tile is within the crook's reach of the local player.</summary>
        private bool IsInRange(Point tile)
        {
            Point player = Game1.player.TilePoint;
            int range = Math.Max(0, this.Config.Range);
            return Math.Abs(player.X - tile.X) <= range
                && Math.Abs(player.Y - tile.Y) <= range
                && Game1.currentLocation.isTileOnMap(tile.X, tile.Y);
        }

        /// <summary>Add or remove a phantom fence tile, either directly (host) or by asking the host (farmhand).</summary>
        private void ApplyEdit(GameLocation location, Point tile, bool add, bool playSound)
        {
            if (Context.IsMainPlayer)
            {
                EditResult result = PhantomFences.Edit(location, tile, add, this.Config.MaxTilesPerLocation);
                switch (result)
                {
                    case EditResult.Changed:
                        if (playSound)
                            Game1.playSound(add ? "woodyStep" : "shwip");
                        break;

                    case EditResult.LimitReached:
                        Game1.playSound("cancel");
                        this.Warn(I18n.MessageLimit());
                        break;
                }
                return;
            }

            // farmhand: the host owns the data
            long hostId = Game1.MasterPlayer.UniqueMultiplayerID;
            IMultiplayerPeer? host = this.Helper.Multiplayer.GetConnectedPlayer(hostId);
            if (host != null && host.GetMod(this.ModManifest.UniqueID) == null)
            {
                Game1.playSound("cancel");
                this.Warn(I18n.MessageNoHostMod());
                return;
            }

            this.Helper.Multiplayer.SendMessage(
                new EditMessage
                {
                    LocationName = location.NameOrUniqueName,
                    IsStructure = location.isStructure.Value,
                    X = tile.X,
                    Y = tile.Y,
                    Add = add
                },
                EditMessage.MessageType,
                modIDs: new[] { this.ModManifest.UniqueID },
                playerIDs: new[] { hostId }
            );
            this.Pending.Value[(location.NameOrUniqueName, tile)] = new PendingEdit(add, Game1.ticks + PendingTicks);
            if (playSound)
                Game1.playSound(add ? "woodyStep" : "shwip");
        }

        /// <summary>Get whether a tile has a phantom fence, including this screen's unconfirmed edits.</summary>
        private bool EffectiveHas(GameLocation location, Point tile)
        {
            if (!Context.IsMainPlayer && this.Pending.Value.TryGetValue((location.NameOrUniqueName, tile), out PendingEdit pending) && pending.ExpiresAt > Game1.ticks)
                return pending.Add;
            return PhantomFences.Has(location, tile);
        }

        /// <summary>Get the phantom fence tiles for a location, including this screen's unconfirmed edits.</summary>
        private HashSet<Point> GetEffectiveTiles(GameLocation location)
        {
            HashSet<Point> tiles = PhantomFences.GetTiles(location);
            var pending = this.Pending.Value;
            if (Context.IsMainPlayer || pending.Count == 0)
                return tiles;

            string name = location.NameOrUniqueName;
            HashSet<Point>? merged = null;
            foreach ((var key, PendingEdit edit) in pending)
            {
                if (key.Location != name || edit.ExpiresAt <= Game1.ticks || tiles.Contains(key.Tile) == edit.Add)
                    continue;
                merged ??= new HashSet<Point>(tiles);
                if (edit.Add)
                    merged.Add(key.Tile);
                else
                    merged.Remove(key.Tile);
            }
            return merged ?? tiles;
        }

        /// <summary>Show a HUD warning, rate-limited.</summary>
        private void Warn(string message)
        {
            if (Game1.ticks - this.LastWarningTick.Value < 90)
                return;
            this.LastWarningTick.Value = Game1.ticks;
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
        }

        /// <summary>Draw a rectangle border.</summary>
        private void DrawBorder(SpriteBatch b, Rectangle area, int thickness, Color color)
        {
            b.Draw(this.Pixel, new Rectangle(area.X, area.Y, area.Width, thickness), color);
            b.Draw(this.Pixel, new Rectangle(area.X, area.Bottom - thickness, area.Width, thickness), color);
            b.Draw(this.Pixel, new Rectangle(area.X, area.Y + thickness, thickness, area.Height - thickness * 2), color);
            b.Draw(this.Pixel, new Rectangle(area.Right - thickness, area.Y + thickness, thickness, area.Height - thickness * 2), color);
        }

        /// <summary>Clear per-screen state.</summary>
        private void ResetScreenState()
        {
            this.Paint.ResetAllScreens();
            this.Pending.ResetAllScreens();
            this.AlwaysShow.ResetAllScreens();
        }

        /// <summary>Clamp config values to sane ranges.</summary>
        private void NormalizeConfig()
        {
            this.Config.Range = Math.Clamp(this.Config.Range, 0, 10);
            this.Config.OverlayOpacity = Math.Clamp(this.Config.OverlayOpacity, 0.05f, 1f);
            this.Config.MaxTilesPerLocation = Math.Clamp(this.Config.MaxTilesPerLocation, 1, 100_000);
            this.Config.CraftingRecipeIngredients ??= "";
            this.Config.ToggleAlwaysShowKey ??= new KeybindList();
        }

        /// <summary>Invalidate the data assets this mod edits.</summary>
        private void InvalidateData()
        {
            this.Helper.GameContent.InvalidateCache("Data/Objects");
            this.Helper.GameContent.InvalidateCache("Data/Shops");
            this.Helper.GameContent.InvalidateCache("Data/CraftingRecipes");
        }

        /// <summary>Register the Generic Mod Config Menu options, if it's installed.</summary>
        private void RegisterConfigMenu()
        {
            IGenericModConfigMenuApi? gmcm;
            try
            {
                gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Couldn't connect to Generic Mod Config Menu: {ex.Message}", LogLevel.Trace);
                return;
            }
            if (gmcm == null)
                return;

            IManifest mod = this.ModManifest;
            gmcm.Register(
                mod,
                reset: () => this.Config = new ModConfig(),
                save: () =>
                {
                    this.NormalizeConfig();
                    this.Helper.WriteConfig(this.Config);
                    this.InvalidateData();
                }
            );

            gmcm.AddSectionTitle(mod, I18n.ConfigSectionShop);
            gmcm.AddNumberOption(mod, () => this.Config.Price, v => this.Config.Price = v, I18n.ConfigPriceName, I18n.ConfigPriceDesc, min: 0, max: 100_000, interval: 50);
            gmcm.AddBoolOption(mod, () => this.Config.EnableCraftingRecipe, v => this.Config.EnableCraftingRecipe = v, I18n.ConfigRecipeEnabledName, I18n.ConfigRecipeEnabledDesc);
            gmcm.AddTextOption(mod, () => this.Config.CraftingRecipeIngredients, v => this.Config.CraftingRecipeIngredients = v, I18n.ConfigRecipeIngredientsName, I18n.ConfigRecipeIngredientsDesc);

            gmcm.AddSectionTitle(mod, I18n.ConfigSectionUse);
            gmcm.AddNumberOption(mod, () => this.Config.Range, v => this.Config.Range = v, I18n.ConfigRangeName, I18n.ConfigRangeDesc, min: 0, max: 10, interval: 1);
            gmcm.AddBoolOption(mod, () => this.Config.DragToPaint, v => this.Config.DragToPaint = v, I18n.ConfigDragName, I18n.ConfigDragDesc);

            gmcm.AddSectionTitle(mod, I18n.ConfigSectionDisplay);
            gmcm.AddNumberOption(mod, () => this.Config.OverlayOpacity, v => this.Config.OverlayOpacity = v, I18n.ConfigOpacityName, I18n.ConfigOpacityDesc, min: 0.05f, max: 1f, interval: 0.05f);
            gmcm.AddKeybindList(mod, () => this.Config.ToggleAlwaysShowKey, v => this.Config.ToggleAlwaysShowKey = v, I18n.ConfigToggleName, I18n.ConfigToggleDesc);

            gmcm.AddSectionTitle(mod, I18n.ConfigSectionBehavior);
            gmcm.AddBoolOption(mod, () => this.Config.BlocksPets, v => this.Config.BlocksPets = v, I18n.ConfigPetsName, I18n.ConfigPetsDesc);
        }


        /*********
        ** Private types
        *********/
        /// <summary>An in-progress click/drag with the crook.</summary>
        private sealed class PaintState
        {
            public SButton Button { get; }
            public GameLocation Location { get; }
            public bool Add { get; }
            public HashSet<Point> Visited { get; } = new();

            public PaintState(SButton button, GameLocation location, bool add)
            {
                this.Button = button;
                this.Location = location;
                this.Add = add;
            }
        }

        /// <summary>An unconfirmed farmhand edit.</summary>
        private readonly record struct PendingEdit(bool Add, int ExpiresAt);
    }
}
