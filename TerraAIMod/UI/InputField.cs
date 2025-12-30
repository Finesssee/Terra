using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace TerraAIMod.UI
{
    /// <summary>
    /// A text input field component with cursor support for the Terra AI UI.
    /// Extends UIElement to integrate with Terraria's UI system.
    /// </summary>
    public class InputField : UIElement
    {
        /// <summary>
        /// The current text content of the input field.
        /// </summary>
        public string CurrentString = "";

        /// <summary>
        /// Whether the input field currently has focus.
        /// </summary>
        public bool Focused;

        /// <summary>
        /// The hint text displayed when the field is empty.
        /// </summary>
        private string hintText;

        /// <summary>
        /// The background color of the input field.
        /// </summary>
        public Color BackgroundColor = new Color(30, 30, 30, 200);

        /// <summary>
        /// Timer for cursor blinking animation.
        /// </summary>
        private int cursorTimer;

        /// <summary>
        /// Whether the cursor is currently visible (for blinking effect).
        /// </summary>
        private bool cursorVisible;

        /// <summary>
        /// Event triggered when the Enter key is pressed while focused.
        /// </summary>
        public event EventHandler OnEnterPressed;

        /// <summary>
        /// Event triggered when the Escape key is pressed while focused.
        /// </summary>
        public event EventHandler OnEscapePressed;

        /// <summary>
        /// Creates a new InputField with optional hint text.
        /// </summary>
        /// <param name="hintText">The placeholder text shown when the field is empty.</param>
        public InputField(string hintText = "")
        {
            this.hintText = hintText;
        }

        /// <summary>
        /// Sets the text content of the input field.
        /// </summary>
        /// <param name="text">The text to set.</param>
        public void SetText(string text)
        {
            CurrentString = text ?? "";
        }

        /// <summary>
        /// Focuses the input field for text input.
        /// </summary>
        public void Focus()
        {
            Focused = true;
            Main.clrInput();
        }

        /// <summary>
        /// Removes focus from the input field.
        /// </summary>
        public void Unfocus()
        {
            Focused = false;
        }

        /// <summary>
        /// Updates the input field state each frame.
        /// Handles text input, cursor blinking, and Enter key detection.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Focused)
            {
                // Prevent player interaction while typing
                Main.LocalPlayer.mouseInterface = true;
                PlayerInput.WritingText = true;

                // Handle IME input for international keyboards
                Main.instance.HandleIME();

                // Get new text from keyboard input
                string newText = Main.GetInputText(CurrentString);
                if (newText != CurrentString)
                {
                    CurrentString = newText;
                }

                // Check for Enter key press
                if (Main.inputText.IsKeyDown(Keys.Enter) && !Main.oldInputText.IsKeyDown(Keys.Enter))
                {
                    OnEnterPressed?.Invoke(this, EventArgs.Empty);
                }

                // Check for Escape key press to unfocus
                if (Main.inputText.IsKeyDown(Keys.Escape) && !Main.oldInputText.IsKeyDown(Keys.Escape))
                {
                    Unfocus();
                    OnEscapePressed?.Invoke(this, EventArgs.Empty);
                }

                // Cursor blink timer (toggle every 30 ticks)
                cursorTimer++;
                if (cursorTimer >= 30)
                {
                    cursorTimer = 0;
                    cursorVisible = !cursorVisible;
                }
            }
            else
            {
                // Reset cursor visibility when not focused
                cursorVisible = false;
                cursorTimer = 0;
            }
        }

        /// <summary>
        /// Handles left click events to focus the input field.
        /// </summary>
        /// <param name="evt">The mouse event data.</param>
        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            Focus();
        }

        /// <summary>
        /// Handles right click events (can also focus the input field).
        /// </summary>
        /// <param name="evt">The mouse event data.</param>
        public override void RightClick(UIMouseEvent evt)
        {
            base.RightClick(evt);
            Focus();
        }

        /// <summary>
        /// Called when the mouse is outside this element and a click occurs.
        /// Used to unfocus the input field when clicking elsewhere.
        /// </summary>
        /// <param name="evt">The mouse event data.</param>
        public override void LeftMouseDown(UIMouseEvent evt)
        {
            base.LeftMouseDown(evt);
            // If the click is on this element, we'll focus in LeftClick
            // This handler is for when we need to track the source element
        }

        /// <summary>
        /// Unfocuses the input field when clicking outside of it.
        /// This should be called by the parent UI state when appropriate.
        /// </summary>
        public void TryUnfocus()
        {
            if (Focused && !ContainsPoint(Main.MouseScreen))
            {
                Unfocus();
            }
        }

        /// <summary>
        /// Draws the input field including background, border, text, and cursor.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch for drawing.</param>
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            Rectangle rect = new Rectangle(
                (int)dimensions.X,
                (int)dimensions.Y,
                (int)dimensions.Width,
                (int)dimensions.Height
            );

            // Draw background
            spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                rect,
                BackgroundColor
            );

            // Draw border (highlight if focused)
            Color borderColor = Focused ? new Color(100, 149, 237) : new Color(80, 80, 80);
            int borderThickness = Focused ? 2 : 1;
            DrawBorder(spriteBatch, rect, borderColor, borderThickness);

            // Determine text to display
            string displayText;
            Color textColor;

            if (string.IsNullOrEmpty(CurrentString) && !Focused)
            {
                // Show hint text when empty and not focused
                displayText = hintText;
                textColor = Color.Gray;
            }
            else
            {
                displayText = CurrentString;
                textColor = Color.White;
            }

            // Calculate text position with padding
            Vector2 textPosition = new Vector2(dimensions.X + 8, dimensions.Y + (dimensions.Height - 20) / 2);

            // Draw the text
            if (!string.IsNullOrEmpty(displayText))
            {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                spriteBatch.DrawString(font, displayText, textPosition, textColor);
            }

            // Draw blinking cursor if focused
            if (Focused && cursorVisible)
            {
                float cursorX = textPosition.X;
                if (!string.IsNullOrEmpty(CurrentString))
                {
                    Vector2 textSize = FontAssets.MouseText.Value.MeasureString(CurrentString);
                    cursorX += textSize.X;
                }

                Rectangle cursorRect = new Rectangle(
                    (int)cursorX,
                    (int)(dimensions.Y + 4),
                    2,
                    (int)(dimensions.Height - 8)
                );

                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    cursorRect,
                    Color.White
                );
            }
        }

        /// <summary>
        /// Draws a border around a rectangle by drawing 4 edge rectangles.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch for drawing.</param>
        /// <param name="rect">The rectangle to draw a border around.</param>
        /// <param name="color">The border color.</param>
        /// <param name="thickness">The border thickness in pixels.</param>
        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // Top edge
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);

            // Bottom edge
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);

            // Left edge
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);

            // Right edge
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
