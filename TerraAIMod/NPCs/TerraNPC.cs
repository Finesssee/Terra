using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TerraAIMod.Memory;
using TerraAIMod.Action;
using TerraAIMod.Pathfinding;
using TerraAIMod.Systems;

namespace TerraAIMod.NPCs
{
    /// <summary>
    /// Terra - The main AI companion ModNPC entity.
    /// An immortal, friendly NPC that follows players and executes AI-driven commands.
    /// </summary>
    public class TerraNPC : ModNPC
    {
        #region Constants

        /// <summary>
        /// Default gravity acceleration applied each tick.
        /// </summary>
        private const float Gravity = 0.3f;

        /// <summary>
        /// Maximum fall speed.
        /// </summary>
        private const float MaxFallSpeed = 10f;

        /// <summary>
        /// Default name for Terra if none is set.
        /// </summary>
        private const string DefaultName = "Terra";

        #endregion

        #region Properties

        /// <summary>
        /// The custom name for this Terra instance.
        /// </summary>
        public string TerraName { get; private set; } = DefaultName;

        /// <summary>
        /// Terra's memory system for storing context, learned behaviors, and conversation history.
        /// </summary>
        public TerraMemory Memory { get; private set; }

        /// <summary>
        /// The action executor that processes and runs AI-driven commands.
        /// </summary>
        public ActionExecutor ActionExecutor { get; private set; }

        /// <summary>
        /// Index of the player that Terra is currently following/targeting.
        /// </summary>
        public int TargetPlayerIndex { get; set; } = -1;

        /// <summary>
        /// Whether Terra has access to a grappling hook for movement.
        /// </summary>
        public bool HasGrapplingHook { get; set; } = false;

        /// <summary>
        /// Reference to the target player, or null if no valid target.
        /// </summary>
        public Player TargetPlayer => TargetPlayerIndex >= 0 && TargetPlayerIndex < Main.maxPlayers
            ? Main.player[TargetPlayerIndex]
            : null;

        /// <summary>
        /// Terra's inventory of items (40 slots like a player).
        /// </summary>
        public Item[] Inventory { get; private set; }

        /// <summary>
        /// The currently equipped weapon slot index.
        /// </summary>
        public int EquippedWeaponSlot { get; set; } = -1;

        /// <summary>
        /// Maximum inventory size.
        /// </summary>
        public const int InventorySize = 40;

        #endregion

        #region ModNPC Overrides

        /// <summary>
        /// Sets static defaults for the NPC type.
        /// </summary>
        public override void SetStaticDefaults()
        {
            // Set the number of animation frames
            Main.npcFrameCount[NPC.type] = 25;

            // Make Terra act like a town NPC (can be talked to, etc.)
            NPCID.Sets.ActsLikeTownNPC[NPC.type] = true;

            // Terra spawns with a custom name
            NPCID.Sets.SpawnsWithCustomName[NPC.type] = true;

            // Prevent Terra from being counted as a normal enemy
            NPCID.Sets.CountsAsCritter[NPC.type] = false;
        }

        /// <summary>
        /// Sets default NPC properties.
        /// </summary>
        public override void SetDefaults()
        {
            NPC.width = 18;
            NPC.height = 40;
            NPC.friendly = true;
            NPC.aiStyle = -1; // Custom AI
            NPC.immortal = true;
            NPC.dontTakeDamage = true;

            // Additional useful defaults
            NPC.lifeMax = 250;
            NPC.life = 250;
            NPC.defense = 15;
            NPC.knockBackResist = 0f; // Don't get knocked back
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;

            // Town NPC behavior
            NPC.townNPC = false; // Not a true town NPC, but acts like one
            NPC.homeless = true;
        }

        /// <summary>
        /// Called when the NPC spawns. Initializes Memory and ActionExecutor.
        /// </summary>
        /// <param name="source">The spawn source.</param>
        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            base.OnSpawn(source);

            // Initialize inventory
            InitializeInventory();

            // Initialize memory system with reference to this NPC
            Memory = new TerraMemory(this);

            // Initialize action executor with reference to this NPC
            ActionExecutor = new ActionExecutor(this);

            // Find the nearest player to set as initial target
            FindNearestPlayer();

            // Log spawn
            TerraAIMod.Instance?.Logger.Info($"Terra '{TerraName}' has spawned at ({NPC.position.X:F0}, {NPC.position.Y:F0})");
        }

        /// <summary>
        /// Custom AI logic for Terra.
        /// </summary>
        public override void AI()
        {
            // Update action executor
            ActionExecutor?.Tick();

            // Apply gravity if not climbing or swimming
            if (!IsClimbing() && !IsSwimming())
            {
                ApplyGravity();
            }

            // Update facing direction based on velocity or target
            UpdateDirection();

            // Update animation
            UpdateAnimation();

            // Keep Terra within world bounds
            ClampToWorldBounds();
        }

        /// <summary>
        /// Saves Terra's data to a TagCompound for world persistence.
        /// </summary>
        /// <param name="tag">The tag compound to save to.</param>
        public override void SaveData(TagCompound tag)
        {
            tag["TerraName"] = TerraName;
            tag["HasGrapplingHook"] = HasGrapplingHook;
            tag["TargetPlayerIndex"] = TargetPlayerIndex;

            // Save memory if it exists
            if (Memory != null)
            {
                tag["Memory"] = Memory.Save();
            }
        }

        /// <summary>
        /// Loads Terra's data from a TagCompound.
        /// </summary>
        /// <param name="tag">The tag compound to load from.</param>
        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("TerraName"))
            {
                TerraName = tag.GetString("TerraName");
            }

            if (tag.ContainsKey("HasGrapplingHook"))
            {
                HasGrapplingHook = tag.GetBool("HasGrapplingHook");
            }

            if (tag.ContainsKey("TargetPlayerIndex"))
            {
                TargetPlayerIndex = tag.GetInt("TargetPlayerIndex");
            }

            // Load memory with reference to this NPC
            Memory = new TerraMemory(this);
            if (tag.ContainsKey("Memory"))
            {
                Memory.Load(tag.GetCompound("Memory"));
            }

            // Ensure action executor is initialized
            if (ActionExecutor == null)
            {
                ActionExecutor = new ActionExecutor(this);
            }

            // Register with TerraManager so the loaded Terra is tracked
            TerraManager.Instance?.RegisterTerra(this);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets a string describing Terra's current status/action.
        /// </summary>
        /// <returns>A description of the current status.</returns>
        public string GetCurrentStatus()
        {
            if (ActionExecutor == null)
                return "Idle";

            if (ActionExecutor.IsExecuting)
            {
                return ActionExecutor.CurrentGoal ?? "Executing action";
            }

            return "Idle";
        }

        /// <summary>
        /// Stops the current action and clears any pending tasks.
        /// </summary>
        public void StopCurrentAction()
        {
            ActionExecutor?.StopCurrentAction();
        }

        /// <summary>
        /// Sets Terra's custom name.
        /// </summary>
        /// <param name="name">The new name for Terra.</param>
        public void SetTerraName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                TerraName = name;
                NPC.GivenName = name;
            }
        }

        /// <summary>
        /// Processes a command asynchronously through the AI system.
        /// </summary>
        /// <param name="command">The command to process.</param>
        /// <returns>A task that completes when the command has been processed.</returns>
        public async System.Threading.Tasks.Task ProcessCommandAsync(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            // Add command to memory
            Memory?.AddMessage("user", command);

            try
            {
                // Process through action executor
                if (ActionExecutor != null)
                {
                    await ActionExecutor.ProcessCommandAsync(command);
                }
            }
            catch (Exception ex)
            {
                TerraAIMod.Instance?.Logger.Error($"Error processing command: {ex.Message}");
                // Use QueueChatMessage to safely queue the message for main thread processing
                ActionExecutor?.QueueChatMessage($"Sorry, I encountered an error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a chat message from Terra to nearby players.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void SendChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Add to memory
            Memory?.AddMessage("assistant", message);

            // Add to the UI chat panel (client-side)
            if (Main.netMode != NetmodeID.Server)
            {
                TerraSystem.AddTerraMessage(TerraName, message);
            }

            // Display as NPC chat in game
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                Main.NewText($"<{TerraName}> {message}", Color.LightGreen);
            }
            else if (Main.netMode == NetmodeID.Server)
            {
                Terraria.Chat.ChatHelper.BroadcastChatMessage(
                    Terraria.Localization.NetworkText.FromLiteral($"<{TerraName}> {message}"),
                    Color.LightGreen
                );
            }
        }

        /// <summary>
        /// Checks if Terra is currently climbing a rope or vine.
        /// </summary>
        /// <returns>True if Terra is on a climbable tile.</returns>
        public bool IsClimbing()
        {
            // Get the tile at Terra's center position
            int tileX = (int)(NPC.Center.X / 16f);
            int tileY = (int)(NPC.Center.Y / 16f);

            // Check bounds
            if (tileX < 0 || tileX >= Main.maxTilesX || tileY < 0 || tileY >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[tileX, tileY];

            if (!tile.HasTile)
                return false;

            // Check for rope tiles
            ushort tileType = tile.TileType;
            return tileType == TileID.Rope ||
                   tileType == TileID.SilkRope ||
                   tileType == TileID.VineRope ||
                   tileType == TileID.WebRope ||
                   tileType == TileID.Vines ||
                   tileType == TileID.VineFlowers ||
                   tileType == TileID.JungleVines ||
                   tileType == TileID.CrimsonVines ||
                   tileType == TileID.HallowedVines;
        }

        /// <summary>
        /// Checks if Terra is currently in liquid.
        /// </summary>
        /// <returns>True if Terra is in water, lava, or honey.</returns>
        public bool IsSwimming()
        {
            int tileX = (int)(NPC.Center.X / 16f);
            int tileY = (int)(NPC.Center.Y / 16f);

            if (tileX < 0 || tileX >= Main.maxTilesX || tileY < 0 || tileY >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[tileX, tileY];
            return tile.LiquidAmount > 0;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Applies gravity to Terra's velocity.
        /// </summary>
        private void ApplyGravity()
        {
            // Only apply gravity if not on ground
            if (!IsOnGround())
            {
                NPC.velocity.Y += Gravity;

                // Clamp to max fall speed
                if (NPC.velocity.Y > MaxFallSpeed)
                {
                    NPC.velocity.Y = MaxFallSpeed;
                }
            }
        }

        /// <summary>
        /// Checks if Terra is standing on solid ground.
        /// </summary>
        /// <returns>True if there is solid ground beneath Terra.</returns>
        private bool IsOnGround()
        {
            // Check the tiles beneath Terra's feet
            int leftTileX = (int)(NPC.position.X / 16f);
            int rightTileX = (int)((NPC.position.X + NPC.width) / 16f);
            int bottomTileY = (int)((NPC.position.Y + NPC.height + 1) / 16f);

            for (int x = leftTileX; x <= rightTileX; x++)
            {
                if (x < 0 || x >= Main.maxTilesX || bottomTileY < 0 || bottomTileY >= Main.maxTilesY)
                    continue;

                Tile tile = Main.tile[x, bottomTileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Updates Terra's facing direction based on velocity or target player.
        /// </summary>
        private void UpdateDirection()
        {
            if (Math.Abs(NPC.velocity.X) > 0.1f)
            {
                // Face direction of movement
                NPC.direction = NPC.velocity.X > 0 ? 1 : -1;
            }
            else if (TargetPlayer != null)
            {
                // Face towards target player
                NPC.direction = TargetPlayer.Center.X > NPC.Center.X ? 1 : -1;
            }

            NPC.spriteDirection = NPC.direction;
        }

        /// <summary>
        /// Updates Terra's animation frame.
        /// </summary>
        private void UpdateAnimation()
        {
            NPC.frameCounter++;

            if (Math.Abs(NPC.velocity.X) > 0.5f)
            {
                // Walking animation
                if (NPC.frameCounter >= 4)
                {
                    NPC.frameCounter = 0;
                    NPC.frame.Y += NPC.frame.Height;

                    if (NPC.frame.Y >= NPC.frame.Height * 14)
                    {
                        NPC.frame.Y = 0;
                    }
                }
            }
            else
            {
                // Idle animation
                NPC.frame.Y = 0;
            }
        }

        /// <summary>
        /// Keeps Terra within world boundaries.
        /// </summary>
        private void ClampToWorldBounds()
        {
            // Left boundary
            if (NPC.position.X < 16)
            {
                NPC.position.X = 16;
                NPC.velocity.X = 0;
            }

            // Right boundary
            float rightBound = (Main.maxTilesX - 2) * 16;
            if (NPC.position.X + NPC.width > rightBound)
            {
                NPC.position.X = rightBound - NPC.width;
                NPC.velocity.X = 0;
            }

            // Top boundary
            if (NPC.position.Y < 16)
            {
                NPC.position.Y = 16;
                NPC.velocity.Y = 0;
            }

            // Bottom boundary
            float bottomBound = (Main.maxTilesY - 2) * 16;
            if (NPC.position.Y + NPC.height > bottomBound)
            {
                NPC.position.Y = bottomBound - NPC.height;
                NPC.velocity.Y = 0;
            }
        }

        /// <summary>
        /// Finds and sets the nearest player as the target.
        /// </summary>
        private void FindNearestPlayer()
        {
            float nearestDistance = float.MaxValue;
            int nearestIndex = -1;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead)
                {
                    float distance = Vector2.Distance(NPC.Center, player.Center);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestIndex = i;
                    }
                }
            }

            TargetPlayerIndex = nearestIndex;
        }

        /// <summary>
        /// Initializes Terra's inventory with empty slots.
        /// </summary>
        private void InitializeInventory()
        {
            Inventory = new Item[InventorySize];
            for (int i = 0; i < InventorySize; i++)
            {
                Inventory[i] = new Item();
                Inventory[i].TurnToAir();
            }

            // Give Terra a default starter weapon (wooden sword)
            Inventory[0].SetDefaults(ItemID.WoodenSword);
            Inventory[0].stack = 1;

            // Give some wooden arrows as default ammo
            Inventory[1].SetDefaults(ItemID.WoodenArrow);
            Inventory[1].stack = 100;
        }

        #endregion

        #region Inventory Methods

        /// <summary>
        /// Adds an item to Terra's inventory.
        /// </summary>
        /// <param name="itemType">The item type ID to add.</param>
        /// <param name="stack">The stack count.</param>
        /// <returns>True if the item was added successfully.</returns>
        public bool AddItemToInventory(int itemType, int stack = 1)
        {
            if (Inventory == null)
                InitializeInventory();

            // First try to stack with existing items
            for (int i = 0; i < InventorySize; i++)
            {
                if (Inventory[i].type == itemType && Inventory[i].stack < Inventory[i].maxStack)
                {
                    int spaceAvailable = Inventory[i].maxStack - Inventory[i].stack;
                    int toAdd = Math.Min(stack, spaceAvailable);
                    Inventory[i].stack += toAdd;
                    stack -= toAdd;

                    if (stack <= 0)
                        return true;
                }
            }

            // Then try to find an empty slot
            for (int i = 0; i < InventorySize; i++)
            {
                if (Inventory[i].IsAir)
                {
                    Inventory[i].SetDefaults(itemType);
                    Inventory[i].stack = Math.Min(stack, Inventory[i].maxStack);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes an item from Terra's inventory.
        /// </summary>
        /// <param name="itemType">The item type ID to remove.</param>
        /// <param name="count">The amount to remove.</param>
        /// <returns>True if the item was removed successfully.</returns>
        public bool RemoveItemFromInventory(int itemType, int count = 1)
        {
            if (Inventory == null)
                return false;

            for (int i = 0; i < InventorySize; i++)
            {
                if (Inventory[i].type == itemType)
                {
                    if (Inventory[i].stack >= count)
                    {
                        Inventory[i].stack -= count;
                        if (Inventory[i].stack <= 0)
                        {
                            Inventory[i].TurnToAir();
                        }
                        return true;
                    }
                    else
                    {
                        count -= Inventory[i].stack;
                        Inventory[i].TurnToAir();
                    }
                }
            }

            return count <= 0;
        }

        /// <summary>
        /// Checks if Terra has an item in inventory.
        /// </summary>
        /// <param name="itemType">The item type ID to check.</param>
        /// <param name="count">The minimum count required.</param>
        /// <returns>True if Terra has at least the specified count of the item.</returns>
        public bool HasItem(int itemType, int count = 1)
        {
            if (Inventory == null)
                return false;

            int totalCount = 0;
            for (int i = 0; i < InventorySize; i++)
            {
                if (Inventory[i].type == itemType)
                {
                    totalCount += Inventory[i].stack;
                    if (totalCount >= count)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the count of a specific item in Terra's inventory.
        /// </summary>
        /// <param name="itemType">The item type ID to count.</param>
        /// <returns>The total count of the item.</returns>
        public int GetItemCount(int itemType)
        {
            if (Inventory == null)
                return 0;

            int totalCount = 0;
            for (int i = 0; i < InventorySize; i++)
            {
                if (Inventory[i].type == itemType)
                {
                    totalCount += Inventory[i].stack;
                }
            }

            return totalCount;
        }

        /// <summary>
        /// Finds the best weapon in inventory based on damage.
        /// </summary>
        /// <param name="preferRanged">Whether to prefer ranged weapons.</param>
        /// <returns>The inventory slot index of the best weapon, or -1 if none found.</returns>
        public int FindBestWeaponSlot(bool preferRanged = false)
        {
            if (Inventory == null)
                return -1;

            int bestSlot = -1;
            int bestDamage = 0;

            for (int i = 0; i < InventorySize; i++)
            {
                Item item = Inventory[i];
                if (item.IsAir || item.damage <= 0)
                    continue;

                bool isRanged = item.CountsAsClass(DamageClass.Ranged);
                bool isMelee = item.CountsAsClass(DamageClass.Melee);

                // Skip if doesn't match preference and we already have a weapon
                if (preferRanged && !isRanged && bestSlot >= 0)
                    continue;
                if (!preferRanged && !isMelee && bestSlot >= 0)
                    continue;

                // Check if this weapon is better
                if (preferRanged && isRanged)
                {
                    // For ranged, also check if we have ammo
                    if (item.useAmmo > 0 && !HasAmmoForWeapon(item))
                        continue;

                    if (item.damage > bestDamage)
                    {
                        bestDamage = item.damage;
                        bestSlot = i;
                    }
                }
                else if (!preferRanged && isMelee)
                {
                    if (item.damage > bestDamage)
                    {
                        bestDamage = item.damage;
                        bestSlot = i;
                    }
                }
                else if (bestSlot < 0)
                {
                    // Accept any weapon if we have none
                    if (item.damage > bestDamage)
                    {
                        bestDamage = item.damage;
                        bestSlot = i;
                    }
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Checks if Terra has ammo for the specified weapon.
        /// </summary>
        /// <param name="weapon">The weapon item to check ammo for.</param>
        /// <returns>True if ammo is available.</returns>
        public bool HasAmmoForWeapon(Item weapon)
        {
            if (Inventory == null || weapon.useAmmo <= 0)
                return true;

            int ammoType = weapon.useAmmo;

            for (int i = 0; i < InventorySize; i++)
            {
                Item item = Inventory[i];
                if (!item.IsAir && item.ammo == ammoType && item.stack > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the first available ammo slot for the specified weapon.
        /// </summary>
        /// <param name="weapon">The weapon item to find ammo for.</param>
        /// <returns>The inventory slot index of ammo, or -1 if none found.</returns>
        public int FindAmmoSlot(Item weapon)
        {
            if (Inventory == null || weapon.useAmmo <= 0)
                return -1;

            int ammoType = weapon.useAmmo;

            for (int i = 0; i < InventorySize; i++)
            {
                Item item = Inventory[i];
                if (!item.IsAir && item.ammo == ammoType && item.stack > 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the currently equipped weapon item.
        /// </summary>
        /// <returns>The equipped weapon Item, or null if none equipped.</returns>
        public Item GetEquippedWeapon()
        {
            if (Inventory == null || EquippedWeaponSlot < 0 || EquippedWeaponSlot >= InventorySize)
                return null;

            return Inventory[EquippedWeaponSlot].IsAir ? null : Inventory[EquippedWeaponSlot];
        }

        #endregion
    }
}
