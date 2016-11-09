using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using Microsoft.CognitiveServices.SpeechRecognition;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using Autofac;
using System.Threading;
using static RentACar.MessagesController;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Calling.ObjectModel.Contracts;

namespace RentACar
{
    public class BingSpeech
    {
        private DataRecognitionClient dataClient;
        private Action<string> _callback;
        private ConversationResult conversationResult;
        private Action<bool> _failedCallback;

        public BingSpeech(ConversationResult conversationResult, Action<string> callback, Action<bool> failedCallback)
        {
            this.conversationResult = conversationResult;
            _callback = callback;
            _failedCallback = failedCallback;
        }

        public string DefaultLocale { get; } = "en-US";
        public string SubscriptionKey { get; } = "Bing Speech subscription key";
        public void CreateDataRecoClient()
        {
            this.dataClient = SpeechRecognitionServiceFactory.CreateDataClient(
                SpeechRecognitionMode.ShortPhrase,
                this.DefaultLocale,
                this.SubscriptionKey);

            this.dataClient.OnResponseReceived += this.OnDataShortPhraseResponseReceivedHandler;
        }

        public void SendAudioHelper(Stream recordedStream)
        {
            // Note for wave files, we can just send data from the file right to the server.
            // In the case you are not an audio file in wave format, and instead you have just
            // raw data (for example audio coming over bluetooth), then before sending up any 
            // audio data, you must first send up an SpeechAudioFormat descriptor to describe 
            // the layout and format of your raw audio data via DataRecognitionClient's sendAudioFormat() method.
            int bytesRead = 0;
            byte[] buffer = new byte[1024];
            try
            {
                do
                {
                    // Get more Audio data to send into byte buffer.
                    bytesRead = recordedStream.Read(buffer, 0, buffer.Length);

                    // Send of audio data to service. 
                    this.dataClient.SendAudio(buffer, bytesRead);
                }
                while (bytesRead > 0);
            }
            catch (Exception ex)
            {
                WriteLine("Exception ------------ " + ex.Message);
            }
            finally
            {
                // We are done sending audio.  Final recognition results will arrive in OnResponseReceived event call.
                this.dataClient.EndAudio();
            }
        }

        private async void OnDataShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {

            this.WriteLine("--- OnDataShortPhraseResponseReceivedHandler ---");
            this.WriteResponseResult(e);

            // we got the final result, so it we can end the mic reco.  No need to do this
            // for dataReco, since we already called endAudio() on it as soon as we were done
            // sending all the data.

            // Send to bot
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.RecognitionSuccess)
            {
                await SendToBot(e.PhraseResponse.Results
                    .OrderBy(k => k.Confidence)
                    .FirstOrDefault());
            }
            else
            {
                _failedCallback(true);
            }
        }

        private async Task SendToBot(RecognizedPhrase recognizedPhrase)
        {
            Activity activity = new Activity()
            {
                From = new ChannelAccount { Id = conversationResult.Id },
                Conversation = new ConversationAccount { Id = conversationResult.Id },
                Recipient = new ChannelAccount { Id = "Bot" },
                ServiceUrl = "https://skype.botframework.com",
                ChannelId = "skype",
            };

            activity.Text = recognizedPhrase.DisplayText;

            using (var scope = Microsoft.Bot.Builder.Dialogs.Conversation
                .Container.BeginLifetimeScope(DialogModule.LifetimeScopeTag, Configure))
            {
                scope.Resolve<IMessageActivity>
                    (TypedParameter.From((IMessageActivity)activity));
                DialogModule_MakeRoot.Register
                    (scope, () => new Dialogs.RentLuisDialog());
                var postToBot = scope.Resolve<IPostToBot>();
                await postToBot.PostAsync(activity, CancellationToken.None);
            }
        }

        private void Configure(ContainerBuilder builder)
        {
            builder.Register(c => new BotToUserSpeech(c.Resolve<IMessageActivity>(), _callback))
                .As<IBotToUser>()
                .InstancePerLifetimeScope();
        }

        private void WriteResponseResult(SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.Results.Length == 0)
            {
                this.WriteLine("No phrase response is available.");
            }
            else
            {
                this.WriteLine("********* Final n-BEST Results *********");
                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    this.WriteLine(
                        "[{0}] Confidence={1}, Text=\"{2}\"",
                        i,
                        e.PhraseResponse.Results[i].Confidence,
                        e.PhraseResponse.Results[i].DisplayText);
                }

                this.WriteLine(string.Empty);
            }
        }

        private void WriteLine(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            Trace.WriteLine(formattedStr);
            Debug.WriteLine(formattedStr);
        }
    }
}