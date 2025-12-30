using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TerraAIMod.NPCs;
using TerraAIMod.Pathfinding;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Combat states for the state machine.
    /// </summary>
    public enum CombatState
    {
        /// <summary>Looking for a target.</summary>
        Searching,

        /// <summary>Moving toward the target.</summary>
        Approaching,

        /// <summary>Actively attacking the target.</summary>
        Attacking,

        /// <summary>Running away due to low health or danger.</summary>
        Retreating
    }

    /// <summary>
    /// Action that handles combat with weapons and projectiles.
    /// Supports melee and ranged attacks against hostile NPCs.
    /// </summary>
    public class CombatAction : BaseAction
    {
        #region Constants

        /// <summary>
        /// Range in tiles for melee attacks.
        /// </summary>
        private const float MeleeRange = 3f;

        /// <summary>
        /// Range in tiles for ranged attacks.
        /// </summary>
        private const float RangedRange = 30f;

        /// <summary>
        /// Health percentage threshold for retreating.
        /// </summary>
        private const float RetreatHealth = 0.3f;

        /// <summary>
        /// Maximum search radius for finding targets (in tiles).
        /// </summary>
        private const float MaxSearchRadius = 40f;

        /// <summary>
        /// Tile size in pixels.
        /// </summary>
        private const float TileSize = 16f;

        /// <summary>
        /// Ticks between target searches while searching.
        /// </summary>
        private const int SearchInterval = 30;

        /// <summary>
        /// Default attack cooldown in ticks.
        /// </summary>
        private const int DefaultAttackCooldown = 15;

        /// <summary>
        /// Safe distance from player for retreat (in tiles).
        /// </summary>
        private const float SafeDistanceFromPlayer = 10f;

        #endregion

        #region Fields

        /// <summary>
        /// The type of target to attack (e.g., "hostile", "slime", specific name).
        /// </summary>
        private string targetType;

        /// <summary>
        /// The current target NPC.
        /// </summary>
        private NPC targetNPC;

        /// <summary>
        /// Current combat state.
        /// </summary>
        private CombatState state;

        /// <summary>
        /// Number of ticks the action has been running.
        /// </summary>
        private int ticksRunning;

        /// <summary>
        /// Cooldown remaining before next attack.
        /// </summary>
        private int attackCooldown;

        /// <summary>
        /// The equipped weapon item reference.
        /// </summary>
        private Item equippedWeapon;

        /// <summary>
        /// The inventory slot of the equipped weapon.
        /// </summary>
        private int equippedWeaponSlot;

        /// <summary>
        /// Whether the current weapon uses ammo.
        /// </summary>
        private bool usesAmmo;

        /// <summary>
        /// The ammo type required by the current weapon.
        /// </summary>
        private int ammoType;

        /// <summary>
        /// Pathfinder for navigation.
        /// </summary>
        private TerrariaPathfinder pathfinder;

        /// <summary>
        /// Path executor for following computed paths.
        /// </summary>
        private PathExecutor pathExecutor;

        /// <summary>
        /// Number of kills during this combat action.
        /// </summary>
        private int killCount;

        /// <summary>
        /// Target kill count to complete the action (0 = no limit).
        /// </summary>
        private int targetKillCount;

        /// <summary>
        /// Maximum ticks before timeout (0 = no timeout).
        /// </summary>
        private int maxTicks;

        /// <summary>
        /// Ticks spent in searching state without finding a target.
        /// </summary>
        private int searchingTicks;

        /// <summary>
        /// Maximum ticks to search before giving up.
        /// </summary>
        private const int MaxSearchingTicks = 600; // 10 seconds at 60 fps

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new CombatAction for the specified Terra NPC and task.
        /// </summary>
        /// <param name="terra">The Terra NPC executing this action.</param>
        /// <param name="task">The task containing combat parameters.</param>
        public CombatAction(TerraNPC terra, Task task) : base(terra, task)
        {
        }

        #endregion

        #region BaseAction Implementation

        /// <summary>
        /// Initializes the combat action.
        /// </summary>
        protected override void OnStart()
        {
            // Get target type from parameters
            targetType = task.GetParameter<string>("target", "hostile");

            // Get kill target count (0 = continuous combat)
            targetKillCount = task.GetParameter<int>("killCount", 0);

            // Get timeout (0 = no timeout, in seconds)
            int timeoutSeconds = task.GetParameter<int>("timeout", 0);
            maxTicks = timeoutSeconds * 60; // Convert to ticks

            // Initialize pathfinder and executor
            pathfinder = new TerrariaPathfinder(terra.NPC);
            pathExecutor = new PathExecutor(terra.NPC);

            // Equip the best available weapon
            bool preferRanged = task.GetParameter<bool>("preferRanged", false);
            EquipBestWeapon(preferRanged);

            // Find initial target
            targetNPC = FindTarget();

            // Set initial state based on whether we found a target
            if (targetNPC != null)
            {
                state = CombatState.Approaching;
            }
            else
            {
                state = CombatState.Searching;
            }

            ticksRunning = 0;
            attackCooldown = 0;
            killCount = 0;
            searchingTicks = 0;
        }

        /// <summary>
        /// Updates the combat action each tick.
        /// </summary>
        protected override void OnTick()
        {
            ticksRunning++;

            // Check for timeout
            if (maxTicks > 0 && ticksRunning >= maxTicks)
            {
                result = ActionResult.Succeed($"Combat timed out after {ticksRunning / 60} seconds. Killed {killCount} enemies.");
                return;
            }

            // Check if we've reached kill target
            if (targetKillCount > 0 && killCount >= targetKillCount)
            {
                result = ActionResult.Succeed($"Combat complete! Killed {killCount} enemies.");
                return;
            }

            // Decrement attack cooldown
            if (attackCooldown > 0)
            {
                attackCooldown--;
            }

            // Check health for retreat
            float healthPercent = (float)terra.NPC.life / terra.NPC.lifeMax;
            if (healthPercent <= RetreatHealth && state != CombatState.Retreating)
            {
                state = CombatState.Retreating;
                pathExecutor.ClearPath();
            }

            // Validate current target and check for kills
            if (targetNPC != null && !IsValidTarget(targetNPC))
            {
                // Check if target died (kill confirmation)
                if (!targetNPC.active || targetNPC.life <= 0)
                {
                    killCount++;
                    terra.SendChatMessage($"Defeated {targetNPC.GivenOrTypeName}! ({killCount} kills)");
                }

                targetNPC = null;
                if (state == CombatState.Attacking || state == CombatState.Approaching)
                {
                    state = CombatState.Searching;
                    searchingTicks = 0;
                }
            }

            // Process current state
            switch (state)
            {
                case CombatState.Searching:
                    HandleSearching();
                    break;
                case CombatState.Approaching:
                    HandleApproaching();
                    break;
                case CombatState.Attacking:
                    HandleAttacking();
                    break;
                case CombatState.Retreating:
                    HandleRetreating();
                    break;
            }
        }

        /// <summary>
        /// Called when the action is cancelled.
        /// </summary>
        protected override void OnCancel()
        {
            pathExecutor?.ClearPath();
            targetNPC = null;
        }

        /// <summary>
        /// Gets a description of this action.
        /// </summary>
        public override string Description => $"Combat action targeting {targetType}";

        #endregion

        #region State Handlers

        /// <summary>
        /// Handles the searching state - wanders and looks for targets.
        /// </summary>
        private void HandleSearching()
        {
            searchingTicks++;

            // Check search timeout
            if (searchingTicks >= MaxSearchingTicks)
            {
                // If we have kills, succeed; otherwise fail
                if (killCount > 0)
                {
                    result = ActionResult.Succeed($"No more targets found. Killed {killCount} enemies.");
                }
                else
                {
                    result = ActionResult.Fail($"No {targetType} targets found after searching for {MaxSearchingTicks / 60} seconds.");
                }
                return;
            }

            // Periodically search for targets
            if (ticksRunning % SearchInterval == 0)
            {
                targetNPC = FindTarget();

                if (targetNPC != null)
                {
                    state = CombatState.Approaching;
                    searchingTicks = 0;
                    return;
                }
            }

            // Wander around while searching
            if (pathExecutor.IsComplete || pathExecutor.IsStuck)
            {
                // Pick a random nearby position to wander to
                int currentTileX = (int)(terra.NPC.Center.X / TileSize);
                int currentTileY = (int)(terra.NPC.Center.Y / TileSize);

                int offsetX = Main.rand.Next(-10, 11);
                int targetX = currentTileX + offsetX;
                int targetY = currentTileY;

                var path = pathfinder.FindPath(currentTileX, currentTileY, targetX, targetY);
                if (path != null)
                {
                    pathExecutor.SetPath(path);
                }
            }
            else
            {
                pathExecutor.Tick();
            }
        }

        /// <summary>
        /// Handles the approaching state - moves toward the target.
        /// </summary>
        private void HandleApproaching()
        {
            if (targetNPC == null)
            {
                state = CombatState.Searching;
                return;
            }

            // Calculate distance to target
            float distanceInTiles = Vector2.Distance(terra.NPC.Center, targetNPC.Center) / TileSize;

            // Determine attack range based on weapon type
            float attackRange = IsRangedWeapon() ? RangedRange : MeleeRange;

            // Check if we're in attack range
            if (distanceInTiles <= attackRange)
            {
                state = CombatState.Attacking;
                pathExecutor.ClearPath();
                return;
            }

            // Pathfind to target if far or path is complete
            if (pathExecutor.IsComplete || pathExecutor.IsStuck)
            {
                int currentTileX = (int)(terra.NPC.Center.X / TileSize);
                int currentTileY = (int)(terra.NPC.Center.Y / TileSize);
                int targetTileX = (int)(targetNPC.Center.X / TileSize);
                int targetTileY = (int)(targetNPC.Center.Y / TileSize);

                var path = pathfinder.FindPath(currentTileX, currentTileY, targetTileX, targetTileY);
                if (path != null)
                {
                    pathExecutor.SetPath(path);
                }
            }
            else
            {
                pathExecutor.Tick();
            }
        }

        /// <summary>
        /// Handles the attacking state - faces target and performs attacks.
        /// </summary>
        private void HandleAttacking()
        {
            if (targetNPC == null)
            {
                state = CombatState.Searching;
                return;
            }

            // Face the target
            float direction = targetNPC.Center.X - terra.NPC.Center.X;
            terra.NPC.direction = direction > 0 ? 1 : -1;
            terra.NPC.spriteDirection = terra.NPC.direction;

            // Calculate distance
            float distanceInTiles = Vector2.Distance(terra.NPC.Center, targetNPC.Center) / TileSize;
            float attackRange = IsRangedWeapon() ? RangedRange : MeleeRange;

            // Check if target is out of range
            if (distanceInTiles > attackRange * 1.2f)
            {
                state = CombatState.Approaching;
                return;
            }

            // Check line of sight for ranged weapons
            if (IsRangedWeapon() && !HasLineOfSight(targetNPC))
            {
                state = CombatState.Approaching;
                return;
            }

            // Check ammo for ranged weapons
            if (IsRangedWeapon() && usesAmmo && !terra.HasAmmoForWeapon(equippedWeapon))
            {
                // Try to switch to melee weapon
                EquipBestWeapon(false);
                if (equippedWeapon == null)
                {
                    result = ActionResult.Fail("Out of ammo and no melee weapon available.");
                    return;
                }
            }

            // Perform attack when cooldown is ready
            if (attackCooldown <= 0)
            {
                PerformAttack();
                // Set cooldown based on weapon use time
                attackCooldown = equippedWeapon != null ? equippedWeapon.useTime : DefaultAttackCooldown;
            }
        }

        /// <summary>
        /// Handles the retreating state - moves toward the nearest player for safety.
        /// </summary>
        private void HandleRetreating()
        {
            // Find nearest player
            Player nearestPlayer = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead)
                {
                    float distance = Vector2.Distance(terra.NPC.Center, player.Center);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPlayer = player;
                    }
                }
            }

            if (nearestPlayer == null)
            {
                // No player found, just keep moving
                result = ActionResult.Fail("No player found to retreat to");
                return;
            }

            float distanceInTiles = nearestDistance / TileSize;

            // Check if we're safe (close to player)
            if (distanceInTiles <= SafeDistanceFromPlayer)
            {
                // Check if health has recovered enough
                float healthPercent = (float)terra.NPC.life / terra.NPC.lifeMax;
                if (healthPercent > RetreatHealth + 0.2f)
                {
                    // Resume combat
                    state = CombatState.Searching;
                    result = ActionResult.Succeed("Retreated to safety and recovered");
                    return;
                }
            }

            // Pathfind to player
            if (pathExecutor.IsComplete || pathExecutor.IsStuck)
            {
                int currentTileX = (int)(terra.NPC.Center.X / TileSize);
                int currentTileY = (int)(terra.NPC.Center.Y / TileSize);
                int targetTileX = (int)(nearestPlayer.Center.X / TileSize);
                int targetTileY = (int)(nearestPlayer.Center.Y / TileSize);

                var path = pathfinder.FindPath(currentTileX, currentTileY, targetTileX, targetTileY);
                if (path != null)
                {
                    pathExecutor.SetPath(path);
                }
            }
            else
            {
                pathExecutor.Tick();
            }
        }

        #endregion

        #region Combat Methods

        /// <summary>
        /// Performs an attack on the current target.
        /// </summary>
        private void PerformAttack()
        {
            if (targetNPC == null || equippedWeapon == null)
                return;

            if (IsRangedWeapon())
            {
                PerformRangedAttack();
            }
            else
            {
                PerformMeleeAttack();
            }
        }

        /// <summary>
        /// Performs a ranged attack on the current target.
        /// </summary>
        private void PerformRangedAttack()
        {
            // Get the projectile type and stats from ammo
            int ammoSlot = terra.FindAmmoSlot(equippedWeapon);
            if (ammoSlot < 0 && usesAmmo)
            {
                // No ammo available
                return;
            }

            Item ammoItem = usesAmmo ? terra.Inventory[ammoSlot] : null;

            // Calculate direction with slight accuracy variance
            Vector2 direction = targetNPC.Center - terra.NPC.Center;
            direction.Normalize();

            // Add slight randomness for realism
            float accuracy = 0.05f;
            direction.X += Main.rand.NextFloat(-accuracy, accuracy);
            direction.Y += Main.rand.NextFloat(-accuracy, accuracy);
            direction.Normalize();

            // Get projectile type from ammo or weapon
            int projectileType = GetProjectileType(ammoItem);
            float speed = equippedWeapon.shootSpeed > 0 ? equippedWeapon.shootSpeed : 10f;
            int damage = GetWeaponDamage();
            float knockback = GetWeaponKnockback();

            // Spawn the projectile
            Projectile.NewProjectile(
                terra.NPC.GetSource_FromAI(),
                terra.NPC.Center,
                direction * speed,
                projectileType,
                damage,
                knockback,
                Main.myPlayer
            );

            // Consume ammo if applicable
            if (usesAmmo && ammoItem != null)
            {
                ConsumeAmmo(ammoSlot);
            }
        }

        /// <summary>
        /// Performs a melee attack on the current target.
        /// </summary>
        private void PerformMeleeAttack()
        {
            // Melee attack - check hitbox intersection
            Rectangle meleeHitbox = GetMeleeHitbox();
            Rectangle targetHitbox = targetNPC.Hitbox;

            if (meleeHitbox.Intersects(targetHitbox))
            {
                int damage = GetWeaponDamage();
                float knockback = GetWeaponKnockback();
                int hitDirection = terra.NPC.direction;

                // Calculate crit chance from weapon
                int critChance = equippedWeapon != null ? equippedWeapon.crit + 4 : 10; // Base 4% + item crit

                // Strike the target NPC
                NPC.HitInfo hitInfo = new NPC.HitInfo
                {
                    Damage = damage,
                    Knockback = knockback,
                    HitDirection = hitDirection,
                    Crit = Main.rand.Next(100) < critChance
                };

                targetNPC.StrikeNPC(hitInfo);
            }
        }

        /// <summary>
        /// Finds a valid target within search radius.
        /// </summary>
        /// <returns>The nearest valid target NPC, or null if none found.</returns>
        private NPC FindTarget()
        {
            NPC bestTarget = null;
            float bestDistance = MaxSearchRadius * TileSize;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active)
                    continue;

                if (!IsValidTarget(npc))
                    continue;

                float distance = Vector2.Distance(terra.NPC.Center, npc.Center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = npc;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// Checks if an NPC is a valid target based on the target type.
        /// </summary>
        /// <param name="npc">The NPC to check.</param>
        /// <returns>True if the NPC is a valid target.</returns>
        private bool IsValidTarget(NPC npc)
        {
            // Must be active and alive
            if (npc == null || !npc.active || npc.life <= 0)
                return false;

            // Cannot target friendly NPCs
            if (npc.friendly || npc.townNPC)
                return false;

            // Check target type
            switch (targetType.ToLowerInvariant())
            {
                case "hostile":
                    // Any hostile NPC
                    return !npc.friendly && !npc.townNPC;

                case "slime":
                    // Only slimes
                    return IsSlime(npc);

                default:
                    // Specific NPC name
                    return npc.GivenOrTypeName.Equals(targetType, StringComparison.OrdinalIgnoreCase) ||
                           npc.TypeName.Equals(targetType, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Checks if an NPC is a slime.
        /// </summary>
        /// <param name="npc">The NPC to check.</param>
        /// <returns>True if the NPC is a slime type.</returns>
        private bool IsSlime(NPC npc)
        {
            int type = npc.type;
            return type == NPCID.BlueSlime ||
                   type == NPCID.GreenSlime ||
                   type == NPCID.RedSlime ||
                   type == NPCID.PurpleSlime ||
                   type == NPCID.YellowSlime ||
                   type == NPCID.BlackSlime ||
                   type == NPCID.MotherSlime ||
                   type == NPCID.BabySlime ||
                   type == NPCID.Pinky ||
                   type == NPCID.JungleSlime ||
                   type == NPCID.SpikedJungleSlime ||
                   type == NPCID.IceSlime ||
                   type == NPCID.SpikedIceSlime ||
                   type == NPCID.SandSlime ||
                   type == NPCID.LavaSlime ||
                   type == NPCID.DungeonSlime ||
                   type == NPCID.ToxicSludge ||
                   type == NPCID.CorruptSlime ||
                   type == NPCID.Crimslime ||
                   type == NPCID.IlluminantSlime ||
                   type == NPCID.KingSlime ||
                   type == NPCID.QueenSlimeBoss ||
                   npc.TypeName.Contains("Slime", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if Terra has line of sight to the target.
        /// </summary>
        /// <param name="npc">The target NPC.</param>
        /// <returns>True if there is a clear line of sight.</returns>
        private bool HasLineOfSight(NPC npc)
        {
            return Collision.CanHitLine(
                terra.NPC.Center,
                1, 1,
                npc.Center,
                1, 1
            );
        }

        #endregion

        #region Weapon Methods

        /// <summary>
        /// Checks if the current weapon is a ranged weapon.
        /// </summary>
        /// <returns>True if the weapon is ranged.</returns>
        private bool IsRangedWeapon()
        {
            if (equippedWeapon == null)
                return false;

            // Use the game's damage class system
            return equippedWeapon.CountsAsClass(DamageClass.Ranged);
        }

        /// <summary>
        /// Gets the projectile type for the current weapon.
        /// </summary>
        /// <param name="ammoItem">The ammo item being used (can be null).</param>
        /// <returns>The projectile ID to spawn.</returns>
        private int GetProjectileType(Item ammoItem)
        {
            // If we have ammo, use its shoot property
            if (ammoItem != null && ammoItem.shoot > ProjectileID.None)
            {
                return ammoItem.shoot;
            }

            // If the weapon has a direct shoot property, use that
            if (equippedWeapon != null && equippedWeapon.shoot > ProjectileID.None)
            {
                return equippedWeapon.shoot;
            }

            // Default to wooden arrow for bows
            return ProjectileID.WoodenArrowFriendly;
        }

        /// <summary>
        /// Gets the damage for the current weapon.
        /// </summary>
        /// <returns>The weapon damage value.</returns>
        private int GetWeaponDamage()
        {
            if (equippedWeapon == null)
                return 10;

            return equippedWeapon.damage;
        }

        /// <summary>
        /// Gets the knockback for the current weapon.
        /// </summary>
        /// <returns>The weapon knockback value.</returns>
        private float GetWeaponKnockback()
        {
            if (equippedWeapon == null)
                return 3f;

            return equippedWeapon.knockBack;
        }

        /// <summary>
        /// Gets the melee hitbox for attacks.
        /// </summary>
        /// <returns>Rectangle representing the melee attack area.</returns>
        private Rectangle GetMeleeHitbox()
        {
            int width = (int)(MeleeRange * TileSize);
            int height = (int)(terra.NPC.height * 1.5f);

            int x = terra.NPC.direction > 0
                ? (int)terra.NPC.Center.X
                : (int)terra.NPC.Center.X - width;

            int y = (int)(terra.NPC.Center.Y - height / 2);

            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// Equips the best available weapon from inventory.
        /// </summary>
        /// <param name="preferRanged">Whether to prefer ranged weapons.</param>
        private void EquipBestWeapon(bool preferRanged)
        {
            // Use Terra's inventory system to find the best weapon
            int bestSlot = terra.FindBestWeaponSlot(preferRanged);

            if (bestSlot >= 0)
            {
                equippedWeaponSlot = bestSlot;
                equippedWeapon = terra.Inventory[bestSlot];
                terra.EquippedWeaponSlot = bestSlot;

                // Check if this weapon uses ammo
                usesAmmo = equippedWeapon.useAmmo > 0;
                if (usesAmmo)
                {
                    ammoType = equippedWeapon.useAmmo;
                }
            }
            else
            {
                // No weapon found - try the other type
                if (preferRanged)
                {
                    bestSlot = terra.FindBestWeaponSlot(false);
                }
                else
                {
                    bestSlot = terra.FindBestWeaponSlot(true);
                }

                if (bestSlot >= 0)
                {
                    equippedWeaponSlot = bestSlot;
                    equippedWeapon = terra.Inventory[bestSlot];
                    terra.EquippedWeaponSlot = bestSlot;

                    usesAmmo = equippedWeapon.useAmmo > 0;
                    if (usesAmmo)
                    {
                        ammoType = equippedWeapon.useAmmo;
                    }
                }
                else
                {
                    // No weapons at all - this is a problem
                    equippedWeapon = null;
                    equippedWeaponSlot = -1;
                    terra.EquippedWeaponSlot = -1;
                    usesAmmo = false;
                }
            }
        }

        /// <summary>
        /// Consumes ammo for ranged attacks.
        /// </summary>
        /// <param name="ammoSlot">The inventory slot of the ammo to consume.</param>
        private void ConsumeAmmo(int ammoSlot)
        {
            if (terra.Inventory == null || ammoSlot < 0 || ammoSlot >= TerraNPC.InventorySize)
                return;

            Item ammo = terra.Inventory[ammoSlot];
            if (ammo.IsAir || ammo.stack <= 0)
                return;

            // Reduce ammo stack by 1
            ammo.stack--;

            // Remove item if depleted
            if (ammo.stack <= 0)
            {
                ammo.TurnToAir();
            }
        }

        #endregion
    }
}
