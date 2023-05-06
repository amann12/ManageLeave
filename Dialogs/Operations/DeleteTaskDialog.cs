using AdaptiveCards;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ToDoBot.Utilities;

namespace ToDoBot.Dialogs.Operations
{
    public class DeleteTaskDialog: ComponentDialog
    {
        private CosmosDBClient _cosmosDBClient;
        public DeleteTaskDialog(CosmosDBClient cosmosDBClient) : base(nameof(DeleteTaskDialog))
        {
            var waterfallSteps = new WaterfallStep[]
            {
                ShowTasksAsync,
                DeleteTasksAsync,
                DeleteMoreTasksAsync,
            };

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            InitialDialogId = nameof(WaterfallDialog);
            _cosmosDBClient = cosmosDBClient;
        }

        private async Task<DialogTurnResult> ShowTasksAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            List<ToDoTask> toDoTasks = await _cosmosDBClient.QueryItemsAsync(User.UserID);

            if (toDoTasks.Count == 0)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("You Don't have any task to be Deleted."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
       
            List<string> taskList=new List<string>();

            for (int i = 0; i < toDoTasks.Count; i++)
            {
                taskList.Add(toDoTasks[i].Task);
            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please Select the task you want to delete."), cancellationToken);


            // Create card
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                // Use LINQ to turn the choices into submit actions
                Actions = taskList.Select(choice => new AdaptiveSubmitAction
                {
                    Title = choice,
                    Data = choice, // This will be a string
                }).ToList<AdaptiveAction>(),
            };
            // Prompt
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = (Activity)MessageFactory.Attachment(new Attachment
                {
                    ContentType = AdaptiveCard.ContentType,
                    // Convert the AdaptiveCard to a Jobject
                    Content = JObject.FromObject(card),
                }),
                Choices = ChoiceFactory.ToChoices(taskList),
                // Don't render the choices outside the card
                Style = ListStyle.None,
            },
                cancellationToken);
        }

        private async Task<DialogTurnResult> DeleteTasksAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["TaskToDelete"] = ((FoundChoice)stepContext.Result).Value;
            string taskToDelete = (string)stepContext.Values["TaskToDelete"];

            bool deleteTask = await _cosmosDBClient.DeleteTaskItemAsync(taskToDelete, User.UserID);

            if (deleteTask)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Task '"+ taskToDelete +"' successfully deleted"), cancellationToken);
            List<ToDoTask> toDoTasks = await _cosmosDBClient.QueryItemsAsync(User.UserID);
                if(toDoTasks.Count == 0)
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("No Task left all your tasks are deleted "), cancellationToken);
                    return await stepContext.EndDialogAsync(null,cancellationToken);
                }
                return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Would You like to Delete More Tasks?")
                }, cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Task '" + taskToDelete + "' could not be deleted. Either it has been already deleted or some error occurred."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> DeleteMoreTasksAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                return await stepContext.ReplaceDialogAsync(InitialDialogId, null, cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("OK"), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
    }
}
