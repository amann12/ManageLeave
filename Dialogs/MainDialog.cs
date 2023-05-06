// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
using CoreBotCLU;
using CoreBotCLU.Dialogs;
using CoreBotCLU.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly FlightBookingRecognizer _cluRecognizer;
        protected readonly ILogger Logger;
        private readonly string UserValidationDialogID = "UserValidationDlg";
        private readonly IConfiguration Configuration;
        private readonly CosmosDbClient _cosmosDbClient;

        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(FlightBookingRecognizer cluRecognizer, RequestLeaveDialog requestLeaveDialog, ILogger<MainDialog> logger,IConfiguration configuration, CosmosDbClient cosmosDbClient)
            : base(nameof(MainDialog))
        {
            _cluRecognizer = cluRecognizer;
            Logger = logger;
            Configuration = configuration;
            _cosmosDbClient = cosmosDbClient;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(requestLeaveDialog);
            AddDialog(new TextPrompt(UserValidationDialogID, UserValidation));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                UserExistsStepAsync,
                UserIDStepAsync,
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }
        private async Task<DialogTurnResult> UserExistsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (User.UserID == null)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("Please Specify Your User Type!!!", inputHint: InputHints.IgnoringInput), cancellationToken);
                List<string> operationList = new List<string> { "Existing User", "New User" };
                // Create card
                var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
                {
                    // Use LINQ to turn the choices into submit actions
                    Actions = operationList.Select(choice => new AdaptiveSubmitAction
                    {
                        Title = choice,
                        Data = choice,  // This will be a string
                    }).ToList<AdaptiveAction>(),
                };
                // Prompt
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = (Activity)MessageFactory.Attachment(new Attachment
                    {
                        ContentType = AdaptiveCard.ContentType,
                        // Convert the AdaptiveCard to a JObject
                        Content = JObject.FromObject(card),
                    }),
                    Choices = ChoiceFactory.ToChoices(operationList),
                    // Don't render the choices outside the card
                    Style = ListStyle.None,
                },
                    cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> UserIDStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (User.UserID == null)
            {
                stepContext.Values["UserType"] = ((FoundChoice)stepContext.Result).Value;
                string userType = (string)stepContext.Values["UserType"];
                string userId = null;
                if ("Existing User".Equals(userType))
                {
                    return await stepContext.PromptAsync(UserValidationDialogID, new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Please Enter Your UserId")
                    }, cancellationToken);
                }
                else
                {
                    do
                    {
                        userId = Repository.RandomString(7);
                    } while (await _cosmosDbClient.CheckNewUserIdAsync(userId, Configuration["CosmosEndPointURI"], Configuration["CosmosPrimaryKey"], Configuration["CosmosDatabaseId"], Configuration["CosmosContainerId"], Configuration["CosmosPartitionKey"]));
                    User.UserID = userId;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please make a note of your user id"), cancellationToken);
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(User.UserID), cancellationToken);
                    return await stepContext.NextAsync(null, cancellationToken);

                }
            }
            else
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_cluRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: CLU is not configured. To enable all capabilities, add 'CluProjectName', 'CluDeploymentName', 'CluAPIKey' and 'CluAPIHostName' to the appsettings.json file.", inputHint: InputHints.IgnoringInput), cancellationToken);

                return await stepContext.NextAsync(null, cancellationToken);
            }

            // Use the text provided in FinalStepAsync or the default if it is the first time.
            var weekLaterDate = DateTime.Now.AddDays(7).ToString("MMMM d, yyyy");
            var messageText = stepContext.Options?.ToString() ?? $"What can I help you with today?\nSay something like \"Request for a leave on {weekLaterDate}\"";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_cluRecognizer.IsConfigured)
            {
                // CLU is not configured, we just run the BookingDialog path with an empty BookingDetailsInstance.
                return await stepContext.BeginDialogAsync(nameof(RequestLeaveDialog), new LeaveDetails(), cancellationToken);
            }

            // Call CLU and gather any potential booking details. (Note the TurnContext has the response to the prompt.)
            var cluResult = await _cluRecognizer.RecognizeAsync<ApplyLeave>(stepContext.Context, cancellationToken);
            switch (cluResult.GetTopIntent().intent)
            {
                case ApplyLeave.Intent.RequestLeave:
                    var requestLeaveText = $"You have requested for a leave (intent was {cluResult.GetTopIntent().intent})";
                    var requestLeaveMessage = MessageFactory.Text(requestLeaveText, requestLeaveText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(requestLeaveMessage, cancellationToken);

                    var leaveDetails = new LeaveDetails()
                    {
                        LeaveType = cluResult.Entities.GetLeaveType(),
                        LeaveDate = cluResult.Entities.GetLeaveDay()
                    };
                    return await stepContext.BeginDialogAsync(nameof(RequestLeaveDialog), leaveDetails, cancellationToken);
                    //break;
                case ApplyLeave.Intent.CancelLeave:
                    var cancelLeaveText = $"You have requested for cancel leave (intent was {cluResult.GetTopIntent().intent})";
                    var cancelLeaveMessage = MessageFactory.Text(cancelLeaveText, cancelLeaveText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(cancelLeaveMessage, cancellationToken);
                    break;
                case ApplyLeave.Intent.CheckBalance:
                    var checkBalanceLeaveText = $"You have requested for check balance leave (intent was {cluResult.GetTopIntent().intent})";
                    var checkBalanceLeaveMessage = MessageFactory.Text(checkBalanceLeaveText, checkBalanceLeaveText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(checkBalanceLeaveMessage, cancellationToken);
                    break;

                default:
                    // Catch all for unhandled intents
                    var didntUnderstandMessageText = $"Sorry, I didn't get that. Please try asking in a different way (intent was {cluResult.GetTopIntent().intent})";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // If the child dialog ("BookingDialog") was cancelled, the user failed to confirm or if the intent wasn't BookFlight
            // the Result here will be null.
            if (stepContext.Result is LeaveDetails result)
            {
                // Now we have all the booking details call the booking service.

                // If the call to the booking service was successful tell the user.

                //var timeProperty = new TimexProperty(result.TravelDate);
                //var travelDateMsg = timeProperty.ToNaturalLanguage(DateTime.Now);
                var messageText = $"I have applied your  {result.LeaveType} for {result.LeaveDate}";
                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(message, cancellationToken);
            }

            // Restart the main dialog with a different message the second time around
            var promptMessage = "What else can I do for you?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }

        private async Task<bool> UserValidation(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            string userId = promptContext.Recognized.Value;
            await promptContext.Context.SendActivityAsync("Please wait, while I validate your details...", cancellationToken: cancellationToken);
            if (await _cosmosDbClient.CheckNewUserIdAsync(userId, Configuration["CosmosEndPointURI"], Configuration["CosmosPrimaryKey"], Configuration["CosmosDatabaseId"], Configuration["CosmosContainerId"], Configuration["CosmosPartitionKey"]))
            {
                await promptContext.Context.SendActivityAsync("Your details are verified", cancellationToken: cancellationToken);
                User.UserID = userId;
                return true;
            }
            await promptContext.Context.SendActivityAsync("The user id you entered is not found,Please enter correct id", cancellationToken: cancellationToken);
            return false;
        }
    }
}
