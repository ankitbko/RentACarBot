using Microsoft.Bot.Builder.Dialogs.Internals;
using System;
using Microsoft.Bot.Connector;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Internals.Fibers;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace RentACar
{
    internal class BotToUserSpeech: IBotToUser
    {
        private Action<string> _callback;
        private readonly IMessageActivity toBot;
        public BotToUserSpeech(IMessageActivity toBot, Action<string> _callback)
        {
            SetField.NotNull(out this.toBot, nameof(toBot), toBot);
            this._callback = _callback;
        }

        public IMessageActivity MakeMessage()
        {
            return this.toBot;
        }

        public async Task PostAsync(IMessageActivity message, CancellationToken cancellationToken = default(CancellationToken))
        {
            _callback(message.Text);
            if (message.Attachments?.Count > 0)
                _callback(ButtonsToText(message.Attachments));
        }

        private static string ButtonsToText(IList<Attachment> attachments)
        {
            var cardAttachments = attachments?.Where(attachment => attachment.ContentType.StartsWith("application/vnd.microsoft.card"));
            var builder = new StringBuilder();
            if (cardAttachments != null && cardAttachments.Any())
            {
                builder.AppendLine();
                foreach (var attachment in cardAttachments)
                {
                    string type = attachment.ContentType.Split('.').Last();
                    if (type == "hero" || type == "thumbnail")
                    {
                        var card = (HeroCard)attachment.Content;
                        if (!string.IsNullOrEmpty(card.Title))
                        {
                            builder.AppendLine(card.Title);
                        }
                        if (!string.IsNullOrEmpty(card.Subtitle))
                        {
                            builder.AppendLine(card.Subtitle);
                        }
                        if (!string.IsNullOrEmpty(card.Text))
                        {
                            builder.AppendLine(card.Text);
                        }
                        if (card.Buttons != null)
                        {
                            foreach (var button in card.Buttons)
                            {
                                builder.AppendLine(button.Title);
                            }
                        }
                    }
                }
            }
            return builder.ToString();
        }
    }
}