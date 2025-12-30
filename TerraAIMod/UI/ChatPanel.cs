using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TerraAIMod.UI
{
    /// <summary>
    /// A scrollable chat panel that displays message history for Terra AI conversations.
    /// Supports user messages, Terra responses, and system notifications with colored bubbles.
    /// </summary>
    public class ChatPanel : UIElement
    {
        #region Constants

        /// <summary>
        /// Maximum number of messages to keep in history before removing oldest.
        /// </summary>
        public const int MAX_MESSAGES = 500;

        /// <summary>
        /// Color for user message bubbles.
        /// </summary>
        public static readonly Color UserColor = new Color(76, 175, 80);

        /// <summary>
        /// Color for Terra message bubbles.
        /// </summary>
        public static readonly Color TerraColor = new Color(33, 150, 243);

        /// <summary>
        /// Color for system message bubbles.
        /// </summary>
        public static readonly Color SystemColor = new Color(255, 152, 0);

        /// <summary>
        /// Padding inside message bubbles.
        /// </summary>
        private const float BubblePadding = 8f;

        /// <summary>
        /// Margin between message bubbles.
        /// </summary>
        private const float BubbleMargin = 6f;

        /// <summary>
        /// Width of the scrollbar.
        /// </summary>
        private const float ScrollbarWidth = 20f;

        #endregion

        #region Nested Classes

        /// <summary>
        /// Represents a single chat message with sender, text, and display properties.
        /// </summary>
        public class ChatMessage
        {
            /// <summary>
            /// The name of the message sender.
            /// </summary>
            public string Sender { get; set; }

            /// <summary>
            /// The text content of the message.
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            /// The background color for the message bubble.
            /// </summary>
            public Color BubbleColor { get; set; }

            /// <summary>
            /// Whether this message was sent by the user (affects alignment).
            /// </summary>
            public bool IsUser { get; set; }

            /// <summary>
            /// Creates a new chat message.
            /// </summary>
            /// <param name="sender">The sender's name.</param>
            /// <param name="text">The message text.</param>
            /// <param name="bubbleColor">The bubble background color.</param>
            /// <param name="isUser">True if sent by user.</param>
            public ChatMessage(string sender, string text, Color bubbleColor, bool isUser)
            {
                Sender = sender;
                Text = text;
                BubbleColor = bubbleColor;
                IsUser = isUser;
            }
        }

        /// <summary>
        /// UI element that renders a single message bubble.
        /// </summary>
        public class MessageBubble : UIElement
        {
            /// <summary>
            /// The chat message to display.
            /// </summary>
            public ChatMessage Message { get; private set; }

            /// <summary>
            /// Cached wrapped text lines.
            /// </summary>
            private List<string> _wrappedLines;

            /// <summary>
            /// The maximum width available for the bubble content.
            /// </summary>
            private float _maxContentWidth;

            /// <summary>
            /// Creates a new message bubble for the given message.
            /// </summary>
            /// <param name="message">The chat message to display.</param>
            public MessageBubble(ChatMessage message)
            {
                Message = message;
                _wrappedLines = new List<string>();
            }

            /// <summary>
            /// Recalculates dimensions when the element is recalculated.
            /// </summary>
            public override void Recalculate()
            {
                base.Recalculate();
                CalculateDimensions();
            }

            /// <summary>
            /// Calculates the bubble dimensions based on text content.
            /// </summary>
            private void CalculateDimensions()
            {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                if (font == null || Parent == null)
                    return;

                // Calculate available width (70% of parent for bubble, leaving room for alignment)
                float parentWidth = Parent.GetInnerDimensions().Width;
                _maxContentWidth = parentWidth * 0.7f - (BubblePadding * 2);

                if (_maxContentWidth < 50f)
                    _maxContentWidth = 50f;

                // Wrap the text
                _wrappedLines = WrapText(font, Message.Text, _maxContentWidth);

                // Calculate total height: sender name + wrapped text + padding
                float lineHeight = font.LineSpacing;
                float senderHeight = lineHeight + 2f; // Sender name above bubble
                float textHeight = _wrappedLines.Count * lineHeight;
                float totalHeight = senderHeight + textHeight + (BubblePadding * 2) + BubbleMargin;

                Height.Set(totalHeight, 0f);
                Width.Set(0f, 1f); // Full width of parent
            }

            /// <summary>
            /// Draws the message bubble with background and text.
            /// </summary>
            /// <param name="spriteBatch">The sprite batch to draw with.</param>
            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                if (font == null)
                    return;

                CalculatedStyle dimensions = GetDimensions();
                float lineHeight = font.LineSpacing;

                // Calculate bubble width based on actual text width
                float maxLineWidth = 0f;
                foreach (string line in _wrappedLines)
                {
                    float lineWidth = font.MeasureString(line).X;
                    if (lineWidth > maxLineWidth)
                        maxLineWidth = lineWidth;
                }

                // Also consider sender name width
                float senderWidth = font.MeasureString(Message.Sender).X;
                float bubbleWidth = Math.Max(maxLineWidth, senderWidth) + (BubblePadding * 2);
                bubbleWidth = Math.Min(bubbleWidth, dimensions.Width * 0.7f);

                float bubbleHeight = (_wrappedLines.Count * lineHeight) + (BubblePadding * 2);

                // Position bubble (right-aligned for user, left for others)
                float bubbleX;
                if (Message.IsUser)
                {
                    bubbleX = dimensions.X + dimensions.Width - bubbleWidth - ScrollbarWidth - 4f;
                }
                else
                {
                    bubbleX = dimensions.X + 4f;
                }

                float senderY = dimensions.Y;
                float bubbleY = senderY + lineHeight + 2f;

                // Draw sender name above bubble
                Color senderColor = Message.IsUser ? Color.LightGreen : Color.LightBlue;
                Vector2 senderPos = new Vector2(bubbleX + BubblePadding, senderY);
                spriteBatch.DrawString(font, Message.Sender, senderPos, senderColor);

                // Draw rounded rectangle background
                Rectangle bubbleRect = new Rectangle(
                    (int)bubbleX,
                    (int)bubbleY,
                    (int)bubbleWidth,
                    (int)bubbleHeight
                );
                DrawRoundedRect(spriteBatch, bubbleRect, Message.BubbleColor * 0.85f);

                // Draw wrapped text inside bubble
                float textX = bubbleX + BubblePadding;
                float textY = bubbleY + BubblePadding;

                foreach (string line in _wrappedLines)
                {
                    spriteBatch.DrawString(font, line, new Vector2(textX, textY), Color.White);
                    textY += lineHeight;
                }
            }
        }

        #endregion

        #region Fields

        /// <summary>
        /// List of all chat messages in history.
        /// </summary>
        private List<ChatMessage> _messages;

        /// <summary>
        /// UI elements corresponding to messages (kept in sync with <see cref="_messages"/>).
        /// </summary>
        private List<MessageBubble> _messageBubbles;

        /// <summary>
        /// UI list for scrollable message display.
        /// </summary>
        private UIList _messageList;

        /// <summary>
        /// Scrollbar for the message list.
        /// </summary>
        private UIScrollbar _scrollbar;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the list of chat messages.
        /// </summary>
        public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a new chat panel.
        /// </summary>
        public ChatPanel()
        {
            _messages = new List<ChatMessage>();
            _messageBubbles = new List<MessageBubble>();
        }

        /// <summary>
        /// Initializes the chat panel UI elements.
        /// </summary>
        public override void OnInitialize()
        {
            // Create the scrollable message list
            _messageList = new UIList();
            _messageList.Width.Set(-ScrollbarWidth - 4f, 1f);
            _messageList.Height.Set(0f, 1f);
            _messageList.Left.Set(0f, 0f);
            _messageList.Top.Set(0f, 0f);
            _messageList.ListPadding = 4f;
            Append(_messageList);

            // Create the scrollbar
            _scrollbar = new UIScrollbar();
            _scrollbar.Width.Set(ScrollbarWidth, 0f);
            _scrollbar.Height.Set(0f, 1f);
            _scrollbar.Left.Set(-ScrollbarWidth, 1f);
            _scrollbar.Top.Set(0f, 0f);
            Append(_scrollbar);

            // Attach scrollbar to list
            _messageList.SetScrollbar(_scrollbar);

            // Add welcome message
            AddMessage("System", "Welcome! Terra AI is ready to assist you. Type a message to start chatting.", SystemColor, false);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a new message to the chat panel.
        /// </summary>
        /// <param name="sender">The name of the message sender.</param>
        /// <param name="text">The message text content.</param>
        /// <param name="color">The bubble background color.</param>
        /// <param name="isUser">Whether this message is from the user.</param>
        public void AddMessage(string sender, string text, Color color, bool isUser)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Create the chat message
            ChatMessage message = new ChatMessage(sender, text, color, isUser);

            // Add to messages list
            _messages.Add(message);

            // Remove oldest messages if over limit
            while (_messages.Count > MAX_MESSAGES)
            {
                _messages.RemoveAt(0);

                // Also remove from UI list
                if (_messageBubbles.Count > 0)
                {
                    MessageBubble oldestBubble = _messageBubbles[0];
                    _messageBubbles.RemoveAt(0);
                    oldestBubble.Remove();
                }
            }

            // Create and add message bubble to UI list
            MessageBubble bubble = new MessageBubble(message);
            bubble.Width.Set(0f, 1f);
            _messageBubbles.Add(bubble);
            _messageList.Add(bubble);

            // Auto-scroll to bottom
            ScrollToBottom();
        }

        /// <summary>
        /// Clears all messages from the chat panel.
        /// </summary>
        public void ClearMessages()
        {
            _messages.Clear();
            _messageBubbles.Clear();
            _messageList.Clear();
        }

        /// <summary>
        /// Scrolls the message list to the bottom.
        /// </summary>
        public void ScrollToBottom()
        {
            // Set scrollbar to maximum position
            // We need to recalculate the list first to ensure proper scroll bounds
            if (_messageList != null && _scrollbar != null)
            {
                _messageList.Recalculate();

                // The scrollbar's view position should be set to show the bottom content
                // ViewPosition represents how far down we've scrolled
                // We want to scroll to the end, so we need the total height minus visible height
                float totalHeight = _messageList.GetTotalHeight();
                float viewHeight = _messageList.GetInnerDimensions().Height;
                float maxScroll = System.Math.Max(0f, totalHeight - viewHeight);

                _scrollbar.ViewPosition = maxScroll;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Wraps text to fit within a maximum width.
        /// </summary>
        /// <param name="font">The font to use for measurement.</param>
        /// <param name="text">The text to wrap.</param>
        /// <param name="maxWidth">The maximum width in pixels.</param>
        /// <returns>A list of wrapped text lines.</returns>
        public static List<string> WrapText(DynamicSpriteFont font, string text, float maxWidth)
        {
            List<string> lines = new List<string>();

            if (string.IsNullOrEmpty(text) || font == null || maxWidth <= 0)
            {
                return lines;
            }

            // Split by existing line breaks first
            string[] paragraphs = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrEmpty(paragraph))
                {
                    lines.Add(string.Empty);
                    continue;
                }

                // Split into words
                string[] words = paragraph.Split(' ');
                StringBuilder currentLine = new StringBuilder();

                foreach (string word in words)
                {
                    string testLine = currentLine.Length == 0
                        ? word
                        : currentLine + " " + word;

                    Vector2 size = font.MeasureString(testLine);

                    if (size.X > maxWidth && currentLine.Length > 0)
                    {
                        // Current line is full, start a new line
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                        currentLine.Append(word);
                    }
                    else
                    {
                        // Word fits, add to current line
                        if (currentLine.Length > 0)
                            currentLine.Append(" ");
                        currentLine.Append(word);
                    }
                }

                // Add the last line of the paragraph
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                }
            }

            // Ensure at least one line
            if (lines.Count == 0)
            {
                lines.Add(text);
            }

            return lines;
        }

        /// <summary>
        /// Draws a rounded rectangle (simplified as filled rectangle).
        /// </summary>
        /// <param name="spriteBatch">The sprite batch to draw with.</param>
        /// <param name="rect">The rectangle bounds.</param>
        /// <param name="color">The fill color.</param>
        public static void DrawRoundedRect(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            // Use MagicPixel texture for drawing rectangles
            Texture2D texture = TextureAssets.MagicPixel.Value;

            if (texture == null)
                return;

            // Draw main rectangle body
            spriteBatch.Draw(texture, rect, color);

            // Draw subtle border for depth
            Color borderColor = color * 0.6f;
            int borderWidth = 1;

            // Top border
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Y, rect.Width, borderWidth), borderColor);
            // Bottom border
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Bottom - borderWidth, rect.Width, borderWidth), borderColor);
            // Left border
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Y, borderWidth, rect.Height), borderColor);
            // Right border
            spriteBatch.Draw(texture, new Rectangle(rect.Right - borderWidth, rect.Y, borderWidth, rect.Height), borderColor);
        }

        #endregion
    }
}
