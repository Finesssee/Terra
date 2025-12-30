using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TerraAIMod.NPCs;
using TerraAIMod.Pathfinding;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace TerraAIMod.Action.Actions
{
    /// <summary>
    /// Action for building various structures including NPC housing.
    /// Supports collaborative building with multiple Terra NPCs.
    /// </summary>
    public class BuildStructureAction : BaseAction
    {
        #region Constants

        /// <summary>
        /// Minimum width for valid NPC housing.
        /// </summary>
        public const int MinHousingWidth = 10;

        /// <summary>
        /// Minimum height for valid NPC housing.
        /// </summary>
        public const int MinHousingHeight = 6;

        /// <summary>
        /// Minimum area (width * height) for valid NPC housing.
        /// </summary>
        public const int MinHousingArea = 60;

        /// <summary>
        /// Maximum ticks before timing out the build action.
        /// </summary>
        private const int MaxBuildTicks = 18000; // 5 minutes at 60 ticks/second

        /// <summary>
        /// Distance in tiles at which Terra needs to navigate closer to place a tile.
        /// </summary>
        private const float MaxPlaceDistance = 5f;

        /// <summary>
        /// Tile size in pixels.
        /// </summary>
        private const float TileSize = 16f;

        #endregion

        #region Fields

        /// <summary>
        /// The type of structure to build (house, tower, bridge, arena, hellevator).
        /// </summary>
        private string structureType;

        /// <summary>
        /// The complete build plan with all tile placements.
        /// </summary>
        private List<TilePlacement> buildPlan;

        /// <summary>
        /// Current index in the build plan for non-collaborative builds.
        /// </summary>
        private int currentIndex;

        /// <summary>
        /// Number of ticks the build has been running.
        /// </summary>
        private int ticksRunning;

        /// <summary>
        /// Reference to the collaborative build if participating in one.
        /// </summary>
        private CollaborativeBuild collaborativeBuild;

        /// <summary>
        /// Whether this build is collaborative with other Terras.
        /// </summary>
        private bool isCollaborative;

        /// <summary>
        /// Pathfinder for navigation to build locations.
        /// </summary>
        private TerrariaPathfinder pathfinder;

        /// <summary>
        /// Path executor for following computed paths.
        /// </summary>
        private PathExecutor pathExecutor;

        /// <summary>
        /// The current tile placement being worked on.
        /// </summary>
        private TilePlacement currentTile;

        /// <summary>
        /// Whether Terra is currently navigating to a tile location.
        /// </summary>
        private bool isNavigating;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new BuildStructureAction for the specified Terra NPC.
        /// </summary>
        /// <param name="terra">The Terra NPC that will execute this action.</param>
        /// <param name="task">The task containing build parameters.</param>
        public BuildStructureAction(TerraNPC terra, Task task) : base(terra, task)
        {
            buildPlan = new List<TilePlacement>();
            currentIndex = 0;
            ticksRunning = 0;
            isCollaborative = false;
            currentTile = null;
            isNavigating = false;
        }

        #endregion

        #region BaseAction Implementation

        /// <summary>
        /// Human-readable description of this action.
        /// </summary>
        public override string Description => $"Building {structureType ?? "structure"}";

        /// <summary>
        /// Called when the action starts. Initializes the build plan and registers with collaborative manager.
        /// </summary>
        protected override void OnStart()
        {
            // Get structure type from task parameters
            structureType = task.GetParameter<string>("type", "house").ToLowerInvariant();

            // Get optional dimensions
            int width = task.GetParameter<int>("width", GetDefaultWidth(structureType));
            int height = task.GetParameter<int>("height", GetDefaultHeight(structureType));

            // Initialize pathfinder and executor
            pathfinder = new TerrariaPathfinder(terra.NPC);
            pathExecutor = new PathExecutor(terra.NPC);

            // Find a suitable build location
            Point? buildLocation = FindBuildLocation(width, height);

            if (!buildLocation.HasValue)
            {
                result = ActionResult.Fail("Could not find a suitable build location");
                return;
            }

            // Generate the build plan
            buildPlan = GenerateBuildPlan(structureType, buildLocation.Value, width, height);

            if (buildPlan == null || buildPlan.Count == 0)
            {
                result = ActionResult.Fail($"Failed to generate build plan for {structureType}");
                return;
            }

            // Check for existing collaborative build of this type
            collaborativeBuild = CollaborativeBuildManager.FindActiveBuild(structureType);

            if (collaborativeBuild != null)
            {
                // Join existing build
                isCollaborative = true;
                collaborativeBuild.AssignToSection(terra.TerraName);
                terra.SendChatMessage($"Joining collaborative build of {structureType}");
            }
            else
            {
                // Register new build for collaboration
                collaborativeBuild = CollaborativeBuildManager.RegisterBuild(structureType, buildPlan, buildLocation.Value);
                isCollaborative = true;
                collaborativeBuild.AssignToSection(terra.TerraName);
                terra.SendChatMessage($"Starting to build a {structureType} at ({buildLocation.Value.X}, {buildLocation.Value.Y})");
            }

            TerraAIMod.Instance?.Logger.Info($"Terra '{terra.TerraName}' starting build: {structureType} with {buildPlan.Count} tiles");
        }

        /// <summary>
        /// Called each tick to progress the build.
        /// </summary>
        protected override void OnTick()
        {
            ticksRunning++;

            // Check for timeout
            if (ticksRunning >= MaxBuildTicks)
            {
                result = ActionResult.Fail("Build timed out");
                return;
            }

            // Check if build is complete
            if (collaborativeBuild != null && collaborativeBuild.IsComplete)
            {
                CollaborativeBuildManager.CompleteBuild(collaborativeBuild.StructureId);
                result = ActionResult.Succeed($"Successfully built {structureType}");
                terra.SendChatMessage($"Finished building {structureType}!");
                return;
            }

            // Handle navigation if in progress
            if (isNavigating && !pathExecutor.IsComplete)
            {
                bool pathComplete = pathExecutor.Tick();

                if (pathExecutor.IsStuck)
                {
                    // Try to get a new path or skip this tile
                    isNavigating = false;
                    currentTile = null;
                }

                if (!pathComplete)
                {
                    return; // Still navigating
                }

                isNavigating = false;
            }

            // Get next tile to place
            if (currentTile == null)
            {
                currentTile = GetNextTile();

                if (currentTile == null)
                {
                    // No more tiles available
                    if (collaborativeBuild != null && collaborativeBuild.IsComplete)
                    {
                        CollaborativeBuildManager.CompleteBuild(collaborativeBuild.StructureId);
                        result = ActionResult.Succeed($"Successfully built {structureType}");
                        terra.SendChatMessage($"Finished building {structureType}!");
                    }
                    else
                    {
                        // Waiting for other Terras or build is done
                        result = ActionResult.Succeed($"Completed my part of building {structureType}");
                    }
                    return;
                }
            }

            // Check distance to tile
            float distanceToTile = GetDistanceToTile(currentTile);

            if (distanceToTile > MaxPlaceDistance)
            {
                // Need to navigate closer
                NavigateToTile(currentTile);
                return;
            }

            // Place the tile or wall
            bool placed = PlaceTileOrWall(currentTile);

            if (placed)
            {
                currentTile = null; // Move to next tile
            }
            else
            {
                // Failed to place, might need to clear the area first or skip
                currentTile = null;
            }
        }

        /// <summary>
        /// Called when the action is cancelled.
        /// </summary>
        protected override void OnCancel()
        {
            pathExecutor?.ClearPath();

            // Don't remove from collaborative build - other Terras might still be building
            TerraAIMod.Instance?.Logger.Info($"Terra '{terra.TerraName}' cancelled build action");
        }

        #endregion

        #region Tile Placement

        /// <summary>
        /// Places a tile or wall at the specified location.
        /// </summary>
        /// <param name="placement">The tile placement information.</param>
        /// <returns>True if successfully placed, false otherwise.</returns>
        private bool PlaceTileOrWall(TilePlacement placement)
        {
            if (placement == null)
                return false;

            int x = placement.X;
            int y = placement.Y;

            // Validate coordinates
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            bool success = false;

            if (placement.IsWall)
            {
                // Place wall
                if (placement.WallType >= 0)
                {
                    WorldGen.PlaceWall(x, y, placement.WallType);
                    success = Main.tile[x, y].WallType == placement.WallType;
                }
            }
            else
            {
                // Place tile
                if (placement.TileType >= 0)
                {
                    // Clear existing tile if present
                    if (Main.tile[x, y].HasTile)
                    {
                        WorldGen.KillTile(x, y, false, false, true);
                    }

                    success = WorldGen.PlaceTile(x, y, placement.TileType, false, true);
                }
            }

            // Sync in multiplayer
            if (success && Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendTileSquare(-1, x, y, 1);
            }

            return success;
        }

        /// <summary>
        /// Gets the next tile to place from the collaborative build manager or local plan.
        /// </summary>
        /// <returns>The next tile placement, or null if none available.</returns>
        private TilePlacement GetNextTile()
        {
            if (isCollaborative && collaborativeBuild != null)
            {
                return CollaborativeBuildManager.GetNextTile(collaborativeBuild, terra.TerraName);
            }

            // Non-collaborative: use local index
            if (currentIndex >= buildPlan.Count)
                return null;

            return buildPlan[currentIndex++];
        }

        #endregion

        #region Build Plan Generation

        /// <summary>
        /// Generates a build plan based on the structure type.
        /// </summary>
        /// <param name="type">The type of structure to build.</param>
        /// <param name="start">The starting position for the build.</param>
        /// <param name="width">The width of the structure.</param>
        /// <param name="height">The height of the structure.</param>
        /// <returns>A list of tile placements for the structure.</returns>
        private List<TilePlacement> GenerateBuildPlan(string type, Point start, int width, int height)
        {
            switch (type.ToLowerInvariant())
            {
                case "house":
                case "npchouse":
                case "housing":
                    return GenerateNPCHouse(start, width, height);

                case "tower":
                    return GenerateTower(start, width, height);

                case "bridge":
                    return GenerateBridge(start, width);

                case "arena":
                case "bossarena":
                    return GenerateBossArena(start, width, height);

                case "hellevator":
                case "shaft":
                    return GenerateHellevator(start);

                default:
                    // Default to NPC house
                    return GenerateNPCHouse(start, width, height);
            }
        }

        /// <summary>
        /// Generates a valid NPC house with all required furniture.
        /// </summary>
        /// <param name="start">Bottom-left corner of the house.</param>
        /// <param name="width">Width of the house (minimum 10).</param>
        /// <param name="height">Height of the house (minimum 6).</param>
        /// <returns>List of tile placements for the house.</returns>
        private List<TilePlacement> GenerateNPCHouse(Point start, int width, int height)
        {
            var plan = new List<TilePlacement>();

            // Ensure minimum dimensions for valid NPC housing
            width = Math.Max(width, MinHousingWidth);
            height = Math.Max(height, MinHousingHeight);

            int x = start.X;
            int y = start.Y;

            // Floor (bottom row) - Wood blocks
            for (int i = 0; i < width; i++)
            {
                plan.Add(new TilePlacement(x + i, y, TileID.WoodBlock));
            }

            // Ceiling (top row) - Wood blocks
            for (int i = 0; i < width; i++)
            {
                plan.Add(new TilePlacement(x + i, y - height + 1, TileID.WoodBlock));
            }

            // Left wall with door space
            int doorY = y - 1; // Door position (one tile above floor)
            for (int j = 1; j < height - 1; j++)
            {
                int wallY = y - j;
                if (wallY == doorY || wallY == doorY - 1 || wallY == doorY - 2)
                {
                    // Skip for door space (door is 3 tiles tall)
                    continue;
                }
                plan.Add(new TilePlacement(x, wallY, TileID.WoodBlock));
            }

            // Right wall (solid)
            for (int j = 1; j < height - 1; j++)
            {
                plan.Add(new TilePlacement(x + width - 1, y - j, TileID.WoodBlock));
            }

            // Background walls (inside the house)
            for (int i = 1; i < width - 1; i++)
            {
                for (int j = 1; j < height - 1; j++)
                {
                    plan.Add(new TilePlacement(x + i, y - j, -1, WallID.Wood, true));
                }
            }

            // Door on left wall (3 tiles tall)
            plan.Add(new TilePlacement(x, doorY, TileID.ClosedDoor));

            // Furniture - placed inside the house
            int interiorX = x + 2;
            int floorY = y - 1;

            // Light source (Torch) - on the wall
            plan.Add(new TilePlacement(x + width - 2, y - height + 2, TileID.Torches));

            // Table (2 tiles wide)
            plan.Add(new TilePlacement(interiorX + 2, floorY, TileID.Tables));

            // Chair (next to table)
            plan.Add(new TilePlacement(interiorX + 4, floorY, TileID.Chairs));

            return plan;
        }

        /// <summary>
        /// Generates a vertical multi-story tower.
        /// </summary>
        /// <param name="start">Bottom-left corner of the tower.</param>
        /// <param name="width">Width of the tower.</param>
        /// <param name="height">Total height of the tower.</param>
        /// <returns>List of tile placements for the tower.</returns>
        private List<TilePlacement> GenerateTower(Point start, int width, int height)
        {
            var plan = new List<TilePlacement>();

            int x = start.X;
            int y = start.Y;
            int floorHeight = 6; // Height of each floor
            int numFloors = height / floorHeight;

            for (int floor = 0; floor < numFloors; floor++)
            {
                int floorY = y - (floor * floorHeight);

                // Floor (platforms except for ground floor which uses solid blocks)
                int floorTile = floor == 0 ? TileID.WoodBlock : TileID.Platforms;
                for (int i = 0; i < width; i++)
                {
                    plan.Add(new TilePlacement(x + i, floorY, floorTile));
                }

                // Left wall
                for (int j = 1; j < floorHeight; j++)
                {
                    plan.Add(new TilePlacement(x, floorY - j, TileID.WoodBlock));
                }

                // Right wall
                for (int j = 1; j < floorHeight; j++)
                {
                    plan.Add(new TilePlacement(x + width - 1, floorY - j, TileID.WoodBlock));
                }

                // Background walls
                for (int i = 1; i < width - 1; i++)
                {
                    for (int j = 1; j < floorHeight; j++)
                    {
                        plan.Add(new TilePlacement(x + i, floorY - j, -1, WallID.Wood, true));
                    }
                }

                // Rope or ladder in center for access between floors
                if (floor < numFloors - 1)
                {
                    int ropeX = x + width / 2;
                    for (int j = 1; j < floorHeight; j++)
                    {
                        plan.Add(new TilePlacement(ropeX, floorY - j, TileID.Rope));
                    }
                }

                // Torch for lighting
                plan.Add(new TilePlacement(x + 1, floorY - 2, TileID.Torches));
            }

            // Top ceiling
            int topY = y - (numFloors * floorHeight);
            for (int i = 0; i < width; i++)
            {
                plan.Add(new TilePlacement(x + i, topY + floorHeight, TileID.WoodBlock));
            }

            return plan;
        }

        /// <summary>
        /// Generates a horizontal bridge using platforms.
        /// </summary>
        /// <param name="start">Starting position of the bridge.</param>
        /// <param name="width">Length of the bridge.</param>
        /// <returns>List of tile placements for the bridge.</returns>
        private List<TilePlacement> GenerateBridge(Point start, int width)
        {
            var plan = new List<TilePlacement>();

            int x = start.X;
            int y = start.Y;

            // Main platform surface
            for (int i = 0; i < width; i++)
            {
                plan.Add(new TilePlacement(x + i, y, TileID.Platforms));
            }

            // Support pillars every 8 tiles
            for (int i = 0; i < width; i += 8)
            {
                // Pillar going down
                for (int j = 1; j <= 5; j++)
                {
                    plan.Add(new TilePlacement(x + i, y + j, TileID.WoodBlock));
                }
            }

            // End supports
            if (width > 1)
            {
                for (int j = 1; j <= 5; j++)
                {
                    plan.Add(new TilePlacement(x + width - 1, y + j, TileID.WoodBlock));
                }
            }

            // Torches for lighting every 10 tiles
            for (int i = 0; i < width; i += 10)
            {
                plan.Add(new TilePlacement(x + i, y - 1, TileID.Torches));
            }

            return plan;
        }

        /// <summary>
        /// Generates a boss arena with platforms, campfires, and heart lanterns.
        /// </summary>
        /// <param name="start">Center-bottom of the arena.</param>
        /// <param name="width">Width of the arena.</param>
        /// <param name="height">Height of the arena.</param>
        /// <returns>List of tile placements for the arena.</returns>
        private List<TilePlacement> GenerateBossArena(Point start, int width, int height)
        {
            var plan = new List<TilePlacement>();

            int x = start.X - width / 2; // Center the arena
            int y = start.Y;
            int platformSpacing = 8; // Vertical spacing between platform rows

            // Ground floor - solid
            for (int i = 0; i < width; i++)
            {
                plan.Add(new TilePlacement(x + i, y, TileID.WoodBlock));
            }

            // Multiple platform rows
            int numRows = height / platformSpacing;
            for (int row = 1; row <= numRows; row++)
            {
                int platformY = y - (row * platformSpacing);

                // Platforms across the arena with gaps
                for (int i = 0; i < width; i++)
                {
                    // Leave gaps every 4 tiles for vertical movement
                    if (i % 12 < 8)
                    {
                        plan.Add(new TilePlacement(x + i, platformY, TileID.Platforms));
                    }
                }
            }

            // Campfires on ground level - every 20 tiles
            for (int i = 5; i < width - 5; i += 20)
            {
                plan.Add(new TilePlacement(x + i, y - 1, TileID.Campfire));
            }

            // Heart lanterns - every 30 tiles
            for (int i = 10; i < width - 10; i += 30)
            {
                 plan.Add(new TilePlacement(x + i, y - 3, TileID.HangingLanterns));
            }

            // Torches along the sides
            for (int j = 0; j < height; j += 4)
            {
                plan.Add(new TilePlacement(x, y - j, TileID.Torches));
                plan.Add(new TilePlacement(x + width - 1, y - j, TileID.Torches));
            }

            return plan;
        }

        /// <summary>
        /// Generates a hellevator (vertical shaft to hell) with rope.
        /// </summary>
        /// <param name="start">Starting position at the surface.</param>
        /// <returns>List of tile placements for the hellevator.</returns>
        private List<TilePlacement> GenerateHellevator(Point start)
        {
            var plan = new List<TilePlacement>();

            int x = start.X;
            int y = start.Y;

            // Calculate depth to underworld (approximately)
            int targetDepth = (int)(Main.maxTilesY * 0.85); // Go to about 85% of world depth
            int shaftHeight = targetDepth - y;

            // Create a 3-tile wide shaft
            for (int j = 0; j < shaftHeight; j++)
            {
                int currentY = y + j;

                // Clear the shaft (represented by removing tiles - we'll handle this differently)
                // For now, we place rope in the center
                plan.Add(new TilePlacement(x, currentY, TileID.Rope));

                // Add torches every 50 tiles for visibility
                if (j % 50 == 0)
                {
                    plan.Add(new TilePlacement(x - 1, currentY, TileID.Torches));
                }

                // Add platforms every 100 tiles as rest stops
                if (j % 100 == 0 && j > 0)
                {
                    plan.Add(new TilePlacement(x - 1, currentY, TileID.Platforms));
                    plan.Add(new TilePlacement(x + 1, currentY, TileID.Platforms));
                }
            }

            return plan;
        }

        #endregion

        #region Location Finding

        /// <summary>
        /// Finds a suitable build location near the player.
        /// </summary>
        /// <param name="width">Required width for the structure.</param>
        /// <param name="height">Required height for the structure.</param>
        /// <returns>A point representing the bottom-left corner of the build location, or null if not found.</returns>
        private Point? FindBuildLocation(int width, int height)
        {
            if (terra.TargetPlayer == null)
                return null;

            int playerTileX = (int)(terra.TargetPlayer.Center.X / TileSize);
            int playerTileY = (int)(terra.TargetPlayer.Center.Y / TileSize);

            // Search in expanding circles around the player
            int searchRadius = 50;

            for (int radius = 5; radius < searchRadius; radius += 3)
            {
                // Check positions around the player
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    int testX = playerTileX + offsetX;

                    // Find ground level at this X position
                    int groundY = FindGroundLevel(testX, playerTileY);

                    if (groundY < 0)
                        continue;

                    // Check if this location is flat enough for building
                    if (IsFlatEnough(testX, groundY, width))
                    {
                        // Check if there's enough vertical space
                        bool hasSpace = true;
                        for (int j = 1; j <= height; j++)
                        {
                            if (Main.tile[testX, groundY - j].HasTile &&
                                Main.tileSolid[Main.tile[testX, groundY - j].TileType])
                            {
                                hasSpace = false;
                                break;
                            }
                        }

                        if (hasSpace)
                        {
                            return new Point(testX, groundY);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the ground level at a given X coordinate.
        /// </summary>
        /// <param name="x">The X tile coordinate.</param>
        /// <param name="startY">The Y coordinate to start searching from.</param>
        /// <returns>The Y coordinate of the ground, or -1 if not found.</returns>
        private int FindGroundLevel(int x, int startY)
        {
            if (x < 0 || x >= Main.maxTilesX)
                return -1;

            // Search downward for solid ground
            for (int y = startY; y < Main.maxTilesY - 50; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    return y - 1; // Return the tile above the ground
                }
            }

            return -1;
        }

        /// <summary>
        /// Checks if the ground is flat enough for building.
        /// </summary>
        /// <param name="x">Starting X coordinate.</param>
        /// <param name="y">Ground Y coordinate.</param>
        /// <param name="width">Required width of flat ground.</param>
        /// <returns>True if the ground is sufficiently flat.</returns>
        private bool IsFlatEnough(int x, int y, int width)
        {
            if (x < 0 || x + width >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            int tolerance = 2; // Allow 2 tiles of variation

            for (int i = 0; i < width; i++)
            {
                int checkX = x + i;
                int groundY = -1;

                // Find ground at this position
                for (int checkY = y - tolerance; checkY <= y + tolerance; checkY++)
                {
                    if (checkY >= 0 && checkY < Main.maxTilesY)
                    {
                        Tile tile = Main.tile[checkX, checkY + 1];
                        if (tile.HasTile && Main.tileSolid[tile.TileType])
                        {
                            groundY = checkY;
                            break;
                        }
                    }
                }

                // Check if ground is within tolerance
                if (groundY < 0 || Math.Abs(groundY - y) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Gets the distance from Terra to a tile placement.
        /// </summary>
        /// <param name="placement">The tile placement to measure distance to.</param>
        /// <returns>Distance in tiles.</returns>
        private float GetDistanceToTile(TilePlacement placement)
        {
            if (placement == null)
                return float.MaxValue;

            Vector2 tileWorldPos = new Vector2(placement.X * TileSize + TileSize / 2, placement.Y * TileSize + TileSize / 2);
            return Vector2.Distance(terra.NPC.Center, tileWorldPos) / TileSize;
        }

        /// <summary>
        /// Initiates navigation to a tile location.
        /// </summary>
        /// <param name="placement">The tile placement to navigate to.</param>
        private void NavigateToTile(TilePlacement placement)
        {
            if (placement == null)
                return;

            int terraX = (int)(terra.NPC.Center.X / TileSize);
            int terraY = (int)(terra.NPC.Center.Y / TileSize);

            // Find a path to near the tile
            var path = pathfinder.FindPath(terraX, terraY, placement.X, placement.Y);

            if (path != null && path.Count > 0)
            {
                pathExecutor.SetPath(path);
                isNavigating = true;
            }
            else
            {
                // Can't find path, try to move directly
                float direction = Math.Sign(placement.X - terraX);
                terra.NPC.velocity.X = direction * 3f;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the default width for a structure type.
        /// </summary>
        /// <param name="type">The structure type.</param>
        /// <returns>Default width in tiles.</returns>
        private int GetDefaultWidth(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "house":
                case "npchouse":
                case "housing":
                    return MinHousingWidth;
                case "tower":
                    return 8;
                case "bridge":
                    return 30;
                case "arena":
                case "bossarena":
                    return 100;
                case "hellevator":
                case "shaft":
                    return 3;
                default:
                    return MinHousingWidth;
            }
        }

        /// <summary>
        /// Gets the default height for a structure type.
        /// </summary>
        /// <param name="type">The structure type.</param>
        /// <returns>Default height in tiles.</returns>
        private int GetDefaultHeight(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "house":
                case "npchouse":
                case "housing":
                    return MinHousingHeight;
                case "tower":
                    return 30;
                case "bridge":
                    return 1;
                case "arena":
                case "bossarena":
                    return 40;
                case "hellevator":
                case "shaft":
                    return 1000;
                default:
                    return MinHousingHeight;
            }
        }

        #endregion
    }
}
