namespace MrGlim.GhostwoodCrook
{
    /// <summary>A multiplayer message sent by a farmhand asking the host to add or remove a phantom fence tile.</summary>
    internal sealed class EditMessage
    {
        /// <summary>The message type sent through SMAPI's multiplayer API.</summary>
        public const string MessageType = "EditPhantomFence";

        /// <summary>The <see cref="StardewValley.GameLocation.NameOrUniqueName"/> of the target location.</summary>
        public string LocationName { get; set; } = "";

        /// <summary>Whether the location is a structure (e.g. a barn or coop interior).</summary>
        public bool IsStructure { get; set; }

        /// <summary>The tile X coordinate.</summary>
        public int X { get; set; }

        /// <summary>The tile Y coordinate.</summary>
        public int Y { get; set; }

        /// <summary>True to add a phantom fence, false to remove it.</summary>
        public bool Add { get; set; }
    }
}
