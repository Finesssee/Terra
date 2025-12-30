using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerraAIMod.Systems;

namespace TerraAIMod.UI
{
    /// <summary>
    /// Main UI state container for the Terra AI companion interface.
    /// Features a sliding panel with chat history, input field, and send button.
    /// </summary>
    public class TerraUIState : UIState
    {
        #region Constants

        /// <summary>
        /// Width of the main panel in pixels.
        /// </summary>
        public const float PANEL_WIDTH = 300f;

        /// <summary>
        /// Padding inside the panel in pixels.
        /// </summary>
        public const float PANEL_PADDING = 10f;

        /// <summary>
        /// Animation speed for panel slide in/out (lerp factor per frame).
        /// </summary>
        public const float ANIMATION_SPEED = 0.15f;

        #endregion

        #region Fields

        /// <summary>
        /// The main container panel for all UI elements.
        /// </summary>
        private UIPanel mainPanel;

        /// <summary>
        /// The scrollable chat panel displaying message history.
        /// </summary>
        private ChatPanel chatPanel;

        /// <summary>
        /// The text input field for entering commands.
        /// </summary>
        private InputField inputField;

        /// <summary>
        /// The send button for submitting commands.
        /// </summary>
        private UITextPanel<string> sendButton;

        /// <summary>
        /// Target X position for panel animation (slide in/out).
        /// </summary>
        private float targetX;

        /// <summary>
        /// Current X position during animation.
        /// </summary>
        private float currentX;

        /// <summary>
        /// Tracks if Escape key was pressed last frame (for edge detection).
        /// </summary>
        private bool wasEscapePressed;

        #endregion

        #region Colors

        /// <summary>
        /// Background color for panels (semi-transparent dark).
        /// </summary>
        private static readonly Color BackgroundColor = new Color(0x20, 0x20, 0x20, 0xCC);

        /// <summary>
        /// Border color for panels.
        /// </summary>
        private static readonly Color BorderColor = new Color(0x40, 0x40, 0x40, 0xFF);

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the UI state, creating all UI elements.
        /// </summary>
        public override void OnInitialize()
        {
            // Create main panel - starts off-screen to the right
            mainPanel = new UIPanel();
            mainPanel.Width.Set(PANEL_WIDTH, 0f);
            mainPanel.Height.Set(0f, 1f); // Full height
            mainPanel.HAlign = 1f; // Align to right
            mainPanel.BackgroundColor = BackgroundColor;
            mainPanel.BorderColor = BorderColor;
            mainPanel.SetPadding(PANEL_PADDING);

            // Initialize position off-screen
            currentX = PANEL_WIDTH;
            targetX = PANEL_WIDTH;
            mainPanel.Left.Set(currentX, 0f);

            // Create header text
            UIText headerText = new UIText("Terra AI", 1.2f, true);
            headerText.Top.Set(5f, 0f);
            headerText.HAlign = 0.5f;
            mainPanel.Append(headerText);

            // Create subheader text
            UIText subheaderText = new UIText("Press K to close", 0.8f, false);
            subheaderText.Top.Set(35f, 0f);
            subheaderText.HAlign = 0.5f;
            subheaderText.TextColor = Color.Gray;
            mainPanel.Append(subheaderText);

            // Create chat panel (scrollable area)
            chatPanel = new ChatPanel();
            chatPanel.Top.Set(60f, 0f);
            chatPanel.Width.Set(-PANEL_PADDING * 2, 1f);
            chatPanel.Height.Set(-180f, 1f); // Leave room for input area at bottom
            chatPanel.HAlign = 0.5f;
            mainPanel.Append(chatPanel);

            // Create input area background panel
            UIPanel inputAreaPanel = new UIPanel();
            inputAreaPanel.Width.Set(-PANEL_PADDING * 2, 1f);
            inputAreaPanel.Height.Set(70f, 0f);
            inputAreaPanel.Top.Set(-100f, 1f);
            inputAreaPanel.HAlign = 0.5f;
            inputAreaPanel.BackgroundColor = new Color(0x18, 0x18, 0x18, 0xEE);
            inputAreaPanel.BorderColor = BorderColor;
            inputAreaPanel.SetPadding(5f);
            mainPanel.Append(inputAreaPanel);

            // Create input field
            inputField = new InputField("Tell Terra what to do...");
            inputField.Width.Set(-70f, 1f); // Leave room for send button
            inputField.Height.Set(30f, 0f);
            inputField.Top.Set(5f, 0f);
            inputField.Left.Set(0f, 0f);
            inputField.OnEnterPressed += (sender, args) => SendCommand();
            inputField.OnEscapePressed += (sender, args) => TerraSystem.Close();
            inputAreaPanel.Append(inputField);

            // Create send button
            sendButton = new UITextPanel<string>("Send", 0.9f, false);
            sendButton.Width.Set(55f, 0f);
            sendButton.Height.Set(30f, 0f);
            sendButton.Top.Set(5f, 0f);
            sendButton.Left.Set(-55f, 1f);
            sendButton.BackgroundColor = new Color(0x30, 0x60, 0x90, 0xFF);
            sendButton.OnLeftClick += (evt, elem) => SendCommand();
            sendButton.OnMouseOver += (evt, elem) =>
            {
                ((UITextPanel<string>)elem).BackgroundColor = new Color(0x40, 0x80, 0xB0, 0xFF);
            };
            sendButton.OnMouseOut += (evt, elem) =>
            {
                ((UITextPanel<string>)elem).BackgroundColor = new Color(0x30, 0x60, 0x90, 0xFF);
            };
            inputAreaPanel.Append(sendButton);

            // Create help text at bottom
            UIText helpText = new UIText("Type 'spawn' to summon a new Terra", 0.7f, false);
            helpText.Top.Set(-25f, 1f);
            helpText.HAlign = 0.5f;
            helpText.TextColor = new Color(0x80, 0x80, 0x80, 0xFF);
            mainPanel.Append(helpText);

            // Append main panel to this UIState
            Append(mainPanel);
        }

        #endregion

        #region Panel Animation

        /// <summary>
        /// Called when the UI panel should open (slide in from right).
        /// </summary>
        public void OnOpen()
        {
            targetX = 0f; // Slide to visible position
            inputField?.Focus();
        }

        /// <summary>
        /// Called when the UI panel should close (slide out to right).
        /// </summary>
        public void OnClose()
        {
            targetX = PANEL_WIDTH; // Slide off-screen
            inputField?.Unfocus();
        }

        /// <summary>
        /// Gets whether the panel animation is complete (fully open or fully closed).
        /// </summary>
        public bool IsAnimationComplete => System.Math.Abs(currentX - targetX) < 0.5f;

        /// <summary>
        /// Gets whether the panel is currently visible (not fully off-screen).
        /// </summary>
        public bool IsVisible => currentX < PANEL_WIDTH - 1f;

        /// <summary>
        /// Updates the UI state each frame, handling animation and input focus.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Lerp current position toward target for smooth animation
            if (System.Math.Abs(currentX - targetX) > 0.5f)
            {
                currentX = MathHelper.Lerp(currentX, targetX, ANIMATION_SPEED);
                mainPanel.Left.Set(currentX, 0f);
            }
            else if (currentX != targetX)
            {
                currentX = targetX;
                mainPanel.Left.Set(currentX, 0f);
            }

            // Block game input when mouse is over the panel or input field is focused
            if (IsVisible && mainPanel != null)
            {
                // Check if mouse is over the main panel
                if (mainPanel.ContainsPoint(Main.MouseScreen))
                {
                    Main.LocalPlayer.mouseInterface = true;
                }
            }

            // Also block when input field is focused (regardless of mouse position)
            if (inputField != null && inputField.Focused)
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            // Handle click outside input field to unfocus
            if (inputField != null && inputField.Focused && Main.mouseLeft && Main.mouseLeftRelease)
            {
                inputField.TryUnfocus();
            }

            // Handle Escape key to close the UI (when input field is not focused)
            // When input field IS focused, the InputField's OnEscapePressed event handles it
            bool isEscapePressed = Main.keyState.IsKeyDown(Keys.Escape);
            if (isEscapePressed && !wasEscapePressed && (inputField == null || !inputField.Focused))
            {
                TerraSystem.Close();
            }
            wasEscapePressed = isEscapePressed;
        }

        #endregion

        #region Command Handling

        /// <summary>
        /// Sends the current command from the input field.
        /// </summary>
        private void SendCommand()
        {
            if (inputField == null)
                return;

            string text = inputField.CurrentString?.Trim();

            // Don't send empty commands
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Clear the input field
            inputField.SetText("");

            // Add user message to chat panel
            TerraSystem.AddUserMessage(text);

            // Handle special "spawn" command
            if (text.ToLowerInvariant() == "spawn" || text.ToLowerInvariant().StartsWith("spawn "))
            {
                HandleSpawnCommand(text);
                return;
            }

            // Find an active Terra to send the command to
            SendCommandToTerra(text);
        }

        /// <summary>
        /// Handles the special "spawn" command to create a new Terra.
        /// </summary>
        /// <param name="command">The full spawn command.</param>
        private void HandleSpawnCommand(string command)
        {
            if (TerraManager.Instance == null)
            {
                TerraSystem.AddSystemMessage("Error: TerraManager not initialized.");
                return;
            }

            // Parse name from command (e.g., "spawn Bob" -> "Bob")
            string terraName = "Terra";
            string[] parts = command.Split(' ');
            if (parts.Length > 1)
            {
                terraName = string.Join(" ", parts.Skip(1));
            }
            else
            {
                // Generate a unique name if none provided
                int count = TerraManager.Instance.ActiveCount;
                if (count > 0)
                {
                    terraName = $"Terra #{count + 1}";
                }
            }

            // Spawn near the player
            Player player = Main.LocalPlayer;
            if (player == null || !player.active)
            {
                TerraSystem.AddSystemMessage("Error: No active player found.");
                return;
            }

            Vector2 spawnPosition = player.Center + new Vector2(50f * player.direction, -20f);

            var terra = TerraManager.Instance.SpawnTerra(spawnPosition, terraName);
            if (terra != null)
            {
                TerraSystem.AddSystemMessage($"Spawned {terraName} near you!");
            }
            else
            {
                TerraSystem.AddSystemMessage($"Failed to spawn Terra. Check if name '{terraName}' is already in use or max limit reached.");
            }
        }

        /// <summary>
        /// Sends a command to the first available active Terra.
        /// </summary>
        /// <param name="command">The command to send.</param>
        private void SendCommandToTerra(string command)
        {
            if (TerraManager.Instance == null)
            {
                TerraSystem.AddSystemMessage("Error: TerraManager not initialized.");
                return;
            }

            // Get all active Terras
            var terras = TerraManager.Instance.GetAllTerras()?.ToList();
            if (terras == null || terras.Count == 0)
            {
                TerraSystem.AddSystemMessage("No Terra companions found. Type 'spawn' to create one!");
                return;
            }

            // Send command to the first Terra (or could implement selection logic)
            var activeTerra = terras.First();
            if (activeTerra != null)
            {
                // Process the command asynchronously
                _ = activeTerra.ProcessCommandAsync(command);
                TerraSystem.AddTerraMessage(activeTerra.TerraName, "Processing your request...");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a message to the chat panel.
        /// </summary>
        /// <param name="sender">The name of the message sender.</param>
        /// <param name="text">The message content.</param>
        /// <param name="color">The color to display the message in.</param>
        /// <param name="isUser">Whether this is a user message (affects alignment).</param>
        public void AddMessage(string sender, string text, Color color, bool isUser)
        {
            chatPanel?.AddMessage(sender, text, color, isUser);
        }

        /// <summary>
        /// Clears all messages from the chat panel.
        /// </summary>
        public void ClearMessages()
        {
            chatPanel?.ClearMessages();
        }

        #endregion
    }
}
