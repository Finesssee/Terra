using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace TerraAIMod.Action
{
    public class TilePlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int TileType { get; set; }
        public int WallType { get; set; }
        public bool IsWall { get; set; }

        public TilePlacement(int x, int y, int tileType, int wallType = -1, bool isWall = false)
        {
            X = x;
            Y = y;
            TileType = tileType;
            WallType = wallType;
            IsWall = isWall;
        }
    }

    public class BuildSection
    {
        public string SectionName { get; set; }
        public List<TilePlacement> Tiles { get; set; }
        private int nextTileIndex;
        private readonly object _lock = new object();

        public BuildSection(string sectionName)
        {
            SectionName = sectionName;
            Tiles = new List<TilePlacement>();
            nextTileIndex = 0;
        }

        public TilePlacement GetNextTile()
        {
            lock (_lock)
            {
                if (nextTileIndex >= Tiles.Count)
                    return null;

                return Tiles[nextTileIndex++];
            }
        }

        public bool IsComplete
        {
            get
            {
                lock (_lock)
                {
                    return nextTileIndex >= Tiles.Count;
                }
            }
        }

        public int TilesPlaced
        {
            get
            {
                lock (_lock)
                {
                    return nextTileIndex;
                }
            }
        }

        public int TotalTiles => Tiles.Count;
    }

    public class CollaborativeBuild
    {
        public string StructureId { get; set; }
        public Point StartPos { get; set; }
        public List<TilePlacement> BuildPlan { get; set; }
        public List<BuildSection> Sections { get; set; }
        public HashSet<string> ParticipatingTerras { get; set; }
        public Dictionary<string, int> terraToSection { get; set; }
        private readonly object _lock = new object();

        public CollaborativeBuild(string structureId, List<TilePlacement> buildPlan, Point startPos)
        {
            StructureId = structureId;
            BuildPlan = buildPlan;
            StartPos = startPos;
            Sections = new List<BuildSection>();
            ParticipatingTerras = new HashSet<string>();
            terraToSection = new Dictionary<string, int>();

            DivideIntoQuadrants(buildPlan);
        }

        private void DivideIntoQuadrants(List<TilePlacement> plan)
        {
            if (plan == null || plan.Count == 0)
                return;

            int minX = plan.Min(t => t.X);
            int maxX = plan.Max(t => t.X);
            int minY = plan.Min(t => t.Y);
            int maxY = plan.Max(t => t.Y);

            int midX = (minX + maxX) / 2;
            int midY = (minY + maxY) / 2;

            var topLeft = new BuildSection("top-left");
            var topRight = new BuildSection("top-right");
            var bottomLeft = new BuildSection("bottom-left");
            var bottomRight = new BuildSection("bottom-right");

            foreach (var tile in plan)
            {
                bool isLeft = tile.X <= midX;
                bool isTop = tile.Y <= midY;

                if (isTop && isLeft)
                    topLeft.Tiles.Add(tile);
                else if (isTop && !isLeft)
                    topRight.Tiles.Add(tile);
                else if (!isTop && isLeft)
                    bottomLeft.Tiles.Add(tile);
                else
                    bottomRight.Tiles.Add(tile);
            }

            // Sort each section bottom-to-top (higher Y values first in Terraria's coordinate system)
            topLeft.Tiles = topLeft.Tiles.OrderByDescending(t => t.Y).ThenBy(t => t.X).ToList();
            topRight.Tiles = topRight.Tiles.OrderByDescending(t => t.Y).ThenBy(t => t.X).ToList();
            bottomLeft.Tiles = bottomLeft.Tiles.OrderByDescending(t => t.Y).ThenBy(t => t.X).ToList();
            bottomRight.Tiles = bottomRight.Tiles.OrderByDescending(t => t.Y).ThenBy(t => t.X).ToList();

            Sections.Add(topLeft);
            Sections.Add(topRight);
            Sections.Add(bottomLeft);
            Sections.Add(bottomRight);
        }

        public BuildSection GetAssignedSection(string terraName)
        {
            lock (_lock)
            {
                if (terraToSection.TryGetValue(terraName, out int sectionIndex))
                {
                    if (sectionIndex >= 0 && sectionIndex < Sections.Count)
                        return Sections[sectionIndex];
                }
                return null;
            }
        }

        public BuildSection AssignToSection(string terraName)
        {
            lock (_lock)
            {
                // Check if already assigned
                if (terraToSection.TryGetValue(terraName, out int existingIndex))
                {
                    if (existingIndex >= 0 && existingIndex < Sections.Count)
                        return Sections[existingIndex];
                }

                ParticipatingTerras.Add(terraName);

                // Find an empty section (no one assigned to it)
                var assignedSections = new HashSet<int>(terraToSection.Values);
                for (int i = 0; i < Sections.Count; i++)
                {
                    if (!assignedSections.Contains(i) && !Sections[i].IsComplete)
                    {
                        terraToSection[terraName] = i;
                        return Sections[i];
                    }
                }

                // No empty sections, help with an incomplete one
                for (int i = 0; i < Sections.Count; i++)
                {
                    if (!Sections[i].IsComplete)
                    {
                        terraToSection[terraName] = i;
                        return Sections[i];
                    }
                }

                return null;
            }
        }

        public bool IsComplete
        {
            get
            {
                lock (_lock)
                {
                    return Sections.All(s => s.IsComplete);
                }
            }
        }

        public int TilesPlaced
        {
            get
            {
                lock (_lock)
                {
                    return Sections.Sum(s => s.TilesPlaced);
                }
            }
        }

        public int TotalTiles
        {
            get
            {
                lock (_lock)
                {
                    return Sections.Sum(s => s.TotalTiles);
                }
            }
        }

        public float ProgressPercent
        {
            get
            {
                int total = TotalTiles;
                if (total == 0)
                    return 100f;
                return (TilesPlaced / (float)total) * 100f;
            }
        }
    }

    public static class CollaborativeBuildManager
    {
        private static readonly Dictionary<string, CollaborativeBuild> activeBuilds = new Dictionary<string, CollaborativeBuild>();
        private static readonly object _lock = new object();

        public static CollaborativeBuild RegisterBuild(string structureType, List<TilePlacement> plan, Point startPos)
        {
            lock (_lock)
            {
                string structureId = $"{structureType}_{startPos.X}_{startPos.Y}_{DateTime.Now.Ticks}";
                var build = new CollaborativeBuild(structureId, plan, startPos);
                activeBuilds[structureId] = build;
                return build;
            }
        }

        public static TilePlacement GetNextTile(CollaborativeBuild build, string terraName)
        {
            if (build == null || string.IsNullOrEmpty(terraName))
                return null;

            var section = build.GetAssignedSection(terraName);
            if (section == null)
            {
                section = build.AssignToSection(terraName);
            }

            if (section == null)
                return null;

            var tile = section.GetNextTile();

            // If current section is done, try to help with another
            if (tile == null)
            {
                lock (_lock)
                {
                    foreach (var s in build.Sections)
                    {
                        if (!s.IsComplete)
                        {
                            tile = s.GetNextTile();
                            if (tile != null)
                                break;
                        }
                    }
                }
            }

            return tile;
        }

        public static CollaborativeBuild FindActiveBuild(string structureType)
        {
            lock (_lock)
            {
                foreach (var kvp in activeBuilds)
                {
                    if (kvp.Key.StartsWith(structureType + "_") && !kvp.Value.IsComplete)
                    {
                        return kvp.Value;
                    }
                }
                return null;
            }
        }

        public static void CompleteBuild(string structureId)
        {
            lock (_lock)
            {
                activeBuilds.Remove(structureId);
            }
        }

        public static void CleanupCompleted()
        {
            lock (_lock)
            {
                var completedIds = activeBuilds
                    .Where(kvp => kvp.Value.IsComplete)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var id in completedIds)
                {
                    activeBuilds.Remove(id);
                }
            }
        }
    }
}
