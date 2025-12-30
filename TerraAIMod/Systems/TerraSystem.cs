using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerraAIMod.UI;

namespace TerraAIMod.Systems
{
    /// <summary>
    /// ModSystem responsible for world events and UI management for Terra AI companions.
    /// Handles UI layer integration, world lifecycle events, and message routing.
    /// </summary>
    public class TerraSystem : ModSystem
    {
        /// <summary>
        /// The UserInterface instance managing Terra UI state.
        /// </summary>
        public static UserInterface TerraInterface { get; private set; }

        /// <summary>
        /// The UI state for Terra companion interactions.
        /// </summary>
        public static TerraUIState TerraUI { get; private set; }

        /// <summary>
        /// Whether the Terra UI panel is currently open.
        /// </summary>
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// Initializes the UI system if not a dedicated server.
        /// Note: TerraManager is a ModSystem and initializes automatically via its own Load().
        /// </summary>
        public override void Load()
        {
            // Only create UI on clients, not dedicated servers
            if (!Main.dedServ)
            {
                TerraInterface = new UserInterface();
                TerraUI = new TerraUIState();
                TerraUI.Activate();
            }

            IsOpen = false;
        }

        /// <summary>
        /// Cleans up resources when the mod is unloaded.
        /// Note: TerraManager is a ModSystem and cleans up automatically via its own Unload().
        /// </summary>
        public override void Unload()
        {
            // Clean up UI resources
            TerraInterface = null;
            TerraUI = null;
            IsOpen = false;
        }

        /// <summary>
        /// Called when a world is loaded. Resets UI state.
        /// </summary>
        public override void OnWorldLoad()
        {
            // Reset state when entering a world
            IsOpen = false;

            if (TerraInterface != null && TerraUI != null)
            {
                TerraInterface.SetState(null);
            }
        }

        /// <summary>
        /// Called when a world is unloaded. Clears all Terra companions.
        /// </summary>
        public override void OnWorldUnload()
        {
            // Clear all Terras when leaving a world
            TerraManager.Instance?.ClearAllTerras();

            // Ensure UI is closed
            IsOpen = false;
            if (TerraInterface != null)
            {
                TerraInterface.SetState(null);
            }
        }

        /// <summary>
        /// Called after world update logic. Ticks the TerraManager.
        /// Note: TerraManager.Tick() is now called automatically via PostUpdateNPCs().
        /// This method is kept for any future UI-related world updates.
        /// </summary>
        public override void PostUpdateWorld()
        {
            // TerraManager handles its own ticking via PostUpdateNPCs()
            // This method can be used for UI-related updates if needed
        }

        /// <summary>
        /// Updates the UI state each frame.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void UpdateUI(GameTime gameTime)
        {
            if (TerraInterface?.CurrentState != null)
            {
                TerraInterface.Update(gameTime);

                // If the panel is closed and animation is complete, clear the state
                if (!IsOpen && TerraUI != null && TerraUI.IsAnimationComplete && !TerraUI.IsVisible)
                {
                    TerraInterface.SetState(null);
                }
            }
        }

        /// <summary>
        /// Modifies interface layers to insert the Terra UI after the inventory layer.
        /// </summary>
        /// <param name="layers">The list of game interface layers.</param>
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // Find the inventory layer index
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));

            if (inventoryIndex != -1)
            {
                // Insert our UI layer after the inventory layer
                layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                    "TerraAIMod: Terra UI",
                    delegate
                    {
                        // Draw if open, OR if still animating out (panel visible)
                        if (TerraInterface?.CurrentState != null &&
                            (IsOpen || (TerraUI != null && TerraUI.IsVisible)))
                        {
                            TerraInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI
                ));
            }
        }

        #region Static Helper Methods

        /// <summary>
        /// Toggles the Terra UI panel open or closed.
        /// </summary>
        public static void Toggle()
        {
            if (TerraInterface == null || TerraUI == null)
                return;

            IsOpen = !IsOpen;

            if (IsOpen)
            {
                TerraInterface.SetState(TerraUI);
                TerraUI?.OnOpen();
            }
            else
            {
                TerraUI?.OnClose();
                // Don't set state to null immediately - let the animation complete
                // The ModifyInterfaceLayers will handle visibility based on IsOpen flag
            }
        }

        /// <summary>
        /// Opens the Terra UI panel.
        /// </summary>
        public static void Open()
        {
            if (TerraInterface == null || TerraUI == null)
                return;

            if (!IsOpen)
            {
                IsOpen = true;
                TerraInterface.SetState(TerraUI);
                TerraUI?.OnOpen();
            }
        }

        /// <summary>
        /// Closes the Terra UI panel.
        /// </summary>
        public static void Close()
        {
            if (TerraInterface == null || TerraUI == null)
                return;

            if (IsOpen)
            {
                IsOpen = false;
                TerraUI?.OnClose();
            }
        }

        /// <summary>
        /// Adds a message to the Terra UI chat panel.
        /// </summary>
        /// <param name="sender">The name of the message sender.</param>
        /// <param name="text">The message text content.</param>
        /// <param name="color">The color to display the message in.</param>
        /// <param name="isUser">Whether this message is from the user.</param>
        public static void AddMessage(string sender, string text, Color color, bool isUser)
        {
            TerraUI?.AddMessage(sender, text, color, isUser);
        }

        /// <summary>
        /// Adds a user message to the chat panel.
        /// </summary>
        /// <param name="text">The user's message text.</param>
        public static void AddUserMessage(string text)
        {
            AddMessage("You", text, new Color(100, 149, 237), true); // Cornflower blue
        }

        /// <summary>
        /// Adds a Terra companion message to the chat panel.
        /// </summary>
        /// <param name="terraName">The name of the Terra companion.</param>
        /// <param name="text">The Terra's message text.</param>
        public static void AddTerraMessage(string terraName, string text)
        {
            AddMessage(terraName, text, new Color(144, 238, 144), false); // Light green
        }

        /// <summary>
        /// Adds a system message to the chat panel.
        /// </summary>
        /// <param name="text">The system message text.</param>
        public static void AddSystemMessage(string text)
        {
            AddMessage("System", text, new Color(255, 215, 0), false); // Gold
        }

        #endregion
    }
}
