using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Threading;
using System.Threading.Tasks;
using ToDoBot.Utilities;

namespace ToDoBot.Dialogs.Operations
{
    public class CreateTaskDialog:ComponentDialog
    {
        private readonly CosmosDBClient _cosmosDBClient;
        public CreateTaskDialog( CosmosDBClient cosmosDBClient) :base(nameof(CreateTaskDialog))
        {
            _cosmosDBClient = cosmosDBClient;
            var waterfallSteps = new WaterfallStep[]
            {
                TasksStepAsync,
                ActStepAsync,
                MoreTasksStepAsync,
                SummaryStepAsync
            };

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new CreateMoreTaskDialog());
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> TasksStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Please give the task to Add.")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userDetails = (User)stepContext.Options;
            stepContext.Values["Task"] = (string)stepContext.Result;
            userDetails.TasksList.Add((string)stepContext.Values["Task"]);

            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Would You like to Add More Tasks?")
            }, cancellationToken);

        }

        private async Task<DialogTurnResult> MoreTasksStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userDetails=(User)stepContext.Options;
            if ((bool)stepContext.Result)
            {
                return await stepContext.BeginDialogAsync(nameof(CreateMoreTaskDialog),userDetails,cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(userDetails,cancellationToken);
            }
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userDetails = (User)stepContext.Result;
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Here are the Task you provided -"), cancellationToken);
            for(int i = 0; i < userDetails.TasksList.Count; i++)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(userDetails.TasksList[i]), cancellationToken);
            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please wait while I add the Task into the Database"), cancellationToken);
            for (int i = 0; i < userDetails.TasksList.Count; i++)
            {
                if(await _cosmosDBClient.AddItemsToContainerAsync(User.UserID, userDetails.TasksList[i])== -1)
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("The Task '" + userDetails.TasksList[i]+"' already present"), cancellationToken);
                }
            }
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("The task is operation is completed. Thank You"), cancellationToken);

            return await stepContext.EndDialogAsync(userDetails, cancellationToken);

        }
    }
}
