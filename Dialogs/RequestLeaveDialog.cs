using AdaptiveCards;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples;
using Microsoft.BotBuilderSamples.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBotCLU.Dialogs
{
    public class RequestLeaveDialog : CancelAndHelpDialog
    {
        public RequestLeaveDialog() : base(nameof(RequestLeaveDialog))
        {
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetLeaveTypeStepAsync,
                GetLeaveDateStepAsync,                                
                ConfirmStepAsync,
                FinalStepAsync,
            }));

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }
        private async Task<DialogTurnResult> GetLeaveTypeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var leaveDetails = (LeaveDetails)stepContext.Options;

            if (leaveDetails.LeaveType == null)
            {
                //var promptMessage = MessageFactory.Text("Which type of leave do you want?", "Which type of leave do you want?", InputHints.ExpectingInput);
                //return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
                await stepContext.Context.SendActivityAsync(
                MessageFactory.Text("Which type of leave you want to apply?"), cancellationToken);

                List<string> operationList = new List<string> { "Casual Leave", "Earned Leave", "Sick Leave" };
                // Create card
                var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
                {
                    Actions = operationList.Select(choice => new AdaptiveSubmitAction
                    {
                        Title = choice,
                        Data = choice, 
                    }).ToList<AdaptiveAction>(),
                };
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = (Activity)MessageFactory.Attachment(new Attachment
                    {
                        ContentType = AdaptiveCard.ContentType,
                        Content = JObject.FromObject(card),
                    }),
                    Choices = ChoiceFactory.ToChoices(operationList),
                    Style = ListStyle.None,
                },
                    cancellationToken);
            }
            return await stepContext.NextAsync(leaveDetails.LeaveType, cancellationToken);
        }

        private async Task<DialogTurnResult> GetLeaveDateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["LeaveType"] = ((FoundChoice)stepContext.Result).Value;
            string leaveType = (string)stepContext.Values["LeaveType"];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("You have selected - " + leaveType), cancellationToken);

            var leaveDetails = (LeaveDetails)stepContext.Options;
            leaveDetails.LeaveType = leaveType;
            if (leaveDetails.LeaveDate == null)
            {
                var promptMessage = MessageFactory.Text("On which date do you want a leave", "On which date do you want a leave", InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }

            return await stepContext.NextAsync(leaveDetails.LeaveDate, cancellationToken);
        }        

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var leaveDetails = (LeaveDetails)stepContext.Options;

            //leaveDetails..TravelDate = (string)stepContext.Result;
            leaveDetails.LeaveDate=(string)stepContext.Result;
            var messageText = $"Please confirm, I have requested your : {leaveDetails.LeaveType} for: {leaveDetails.LeaveDate}. Is this correct?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);

            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                var leaveDetails = (LeaveDetails)stepContext.Options;

                return await stepContext.EndDialogAsync(leaveDetails, cancellationToken);
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}
