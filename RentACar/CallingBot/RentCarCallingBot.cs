using Microsoft.Bot.Builder.Calling;
using Microsoft.Bot.Builder.Calling.Events;
using Microsoft.Bot.Builder.Calling.ObjectModel.Contracts;
using Microsoft.Bot.Builder.Calling.ObjectModel.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentACar
{
    public class RentCarCallingBot : ICallingBot
    {
        public ICallingBotService CallingBotService
        {
            get; private set;
        }

        private List<string> response = new List<string>();

        int silenceTimes = 0;

        bool sttFailed = false;

        public RentCarCallingBot(ICallingBotService callingBotService)
        {
            if (callingBotService == null)
                throw new ArgumentNullException(nameof(callingBotService));

            this.CallingBotService = callingBotService;

            CallingBotService.OnIncomingCallReceived += OnIncomingCallReceived;
            CallingBotService.OnPlayPromptCompleted += OnPlayPromptCompleted;
            CallingBotService.OnRecordCompleted += OnRecordCompleted;
            CallingBotService.OnHangupCompleted += OnHangupCompleted;
        }

        private Task OnIncomingCallReceived(IncomingCallEvent incomingCallEvent)
        {
            var id = Guid.NewGuid().ToString();
            incomingCallEvent.ResultingWorkflow.Actions = new List<ActionBase>
                {
                    new Answer { OperationId = id },
                    GetRecordForText("Welcome! How can I help you?")
                };

            return Task.FromResult(true);
        }

        private ActionBase GetRecordForText(string promptText)
        {
            PlayPrompt prompt;
            if (string.IsNullOrEmpty(promptText))
                prompt = null;
            else
                prompt = GetPromptForText(promptText);
            var id = Guid.NewGuid().ToString();
            return new Record()
            {
                OperationId = id,
                PlayPrompt = prompt,
                MaxDurationInSeconds = 10,
                InitialSilenceTimeoutInSeconds = 5,
                MaxSilenceTimeoutInSeconds = 2,
                PlayBeep = false,
                RecordingFormat = RecordingFormat.Wav,
                StopTones = new List<char> { '#' }
            };
        }

        private Task OnPlayPromptCompleted(PlayPromptOutcomeEvent playPromptOutcomeEvent)
        {
            if (response.Count > 0)
            {
                silenceTimes = 0;
                var actionList = new List<ActionBase>();
                foreach (var res in response)
                {
                    Debug.WriteLine($"Response ----- {res}");
                }
                actionList.Add(GetPromptForText(response));
                actionList.Add(GetRecordForText(string.Empty));
                playPromptOutcomeEvent.ResultingWorkflow.Actions = actionList;
                response.Clear();
            }
            else
            {
                if (sttFailed)
                {
                    playPromptOutcomeEvent.ResultingWorkflow.Actions = new List<ActionBase>
                    {
                        GetRecordForText("I didn't catch that, would you kindly repeat?")
                    };
                    sttFailed = false;
                    silenceTimes = 0;
                }
                else if (silenceTimes > 2)
                {
                    playPromptOutcomeEvent.ResultingWorkflow.Actions = new List<ActionBase>
                    {
                        GetPromptForText("Something went wrong. Call again later."),
                        new Hangup() { OperationId = Guid.NewGuid().ToString() }
                    };
                    playPromptOutcomeEvent.ResultingWorkflow.Links = null;
                    silenceTimes = 0;
                }
                else
                {
                    silenceTimes++;
                    playPromptOutcomeEvent.ResultingWorkflow.Actions = new List<ActionBase>
                    {
                        GetSilencePrompt(2000)
                    };
                }
            }
            return Task.CompletedTask;
        }

        private async Task OnRecordCompleted(RecordOutcomeEvent recordOutcomeEvent)
        {
            if (recordOutcomeEvent.RecordOutcome.Outcome == Outcome.Success)
            {
                var record = await recordOutcomeEvent.RecordedContent;
                BingSpeech bs = new BingSpeech(recordOutcomeEvent.ConversationResult, t => response.Add(t), s => sttFailed = s);
                bs.CreateDataRecoClient();
                bs.SendAudioHelper(record);
                recordOutcomeEvent.ResultingWorkflow.Actions = new List<ActionBase>
                {
                    GetSilencePrompt()
                };
            }
            else
            {
                if (silenceTimes > 1)
                {
                    recordOutcomeEvent.ResultingWorkflow.Actions = new List<ActionBase>
                    {
                        GetPromptForText("Thank you for calling"),
                        new Hangup() { OperationId = Guid.NewGuid().ToString() }
                    };
                    recordOutcomeEvent.ResultingWorkflow.Links = null;
                    silenceTimes = 0;
                }
                else
                {
                    silenceTimes++;
                    recordOutcomeEvent.ResultingWorkflow.Actions = new List<ActionBase>
                    {
                        GetRecordForText("I didn't catch that, would you kinly repeat?")
                    };
                }
            }
        }

        private Task OnHangupCompleted(HangupOutcomeEvent hangupOutcomeEvent)
        {
            hangupOutcomeEvent.ResultingWorkflow = null;
            return Task.FromResult(true);
        }

        private static PlayPrompt GetPromptForText(string text)
        {
            var prompt = new Prompt { Value = text, Voice = VoiceGender.Female };
            return new PlayPrompt { OperationId = Guid.NewGuid().ToString(), Prompts = new List<Prompt> { prompt } };
        }

        private static PlayPrompt GetPromptForText(List<string> text)
        {
            var prompts = new List<Prompt>();
            foreach (var txt in text)
            {
                if (!string.IsNullOrEmpty(txt))
                    prompts.Add(new Prompt { Value = txt, Voice = VoiceGender.Female });
            }
            if (prompts.Count == 0)
                return GetSilencePrompt(1000);
            return new PlayPrompt { OperationId = Guid.NewGuid().ToString(), Prompts = prompts };
        }

        private static PlayPrompt GetSilencePrompt(uint silenceLengthInMilliseconds = 3000)
        {
            var prompt = new Prompt { Value = string.Empty, Voice = VoiceGender.Female, SilenceLengthInMilliseconds = silenceLengthInMilliseconds };
            return new PlayPrompt { OperationId = Guid.NewGuid().ToString(), Prompts = new List<Prompt> { prompt } };
        }
    }
}
