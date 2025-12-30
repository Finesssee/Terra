using Terraria.ModLoader;

namespace TerraAIMod
{
    /// <summary>
    /// Terra AI Mod - Main entry point for the tModLoader mod.
    /// This mod provides AI-powered assistance and automation for Terraria gameplay.
    /// </summary>
    public class TerraAIMod : Mod
    {
        /// <summary>
        /// Singleton instance of the mod for global access.
        /// </summary>
        public static TerraAIMod Instance { get; private set; }

        /// <summary>
        /// Called when the mod is loaded. Initializes the singleton instance.
        /// </summary>
        public override void Load()
        {
            Instance = this;
            Logger.Info("Terra AI Mod has been loaded!");
        }

        /// <summary>
        /// Called when the mod is unloaded. Cleans up the singleton instance.
        /// </summary>
        public override void Unload()
        {
            Instance = null;
        }
    }
}
