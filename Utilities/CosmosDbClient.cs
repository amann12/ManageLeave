using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
//using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;

namespace CoreBotCLU.Utilities
{
    public class CosmosDbClient
    {
        // The Cosmos client instance
        private CosmosClient cosmosClient;

        // The database we will create
        private Database database;

        // The container we will create.
        private Container container;

        
        // <GetStartedDemoAsync>
        /// <summary>
        /// Entry point to call methods that operate on Azure Cosmos DB resources in this sample
        /// </summary>
        public async Task GetStartedDemoAsync(string EndpointUri,string PrimaryKey,string databaseId,string containerId,string partitionKey)
        {
            // Create a new instance of the Cosmos Client
            this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = "CosmosDBDotnetQuickstart" });
            await this.CreateDatabaseAsync(databaseId);
            await this.CreateContainerAsync(containerId,partitionKey);
            //await this.ScaleContainerAsync();
            //await this.AddItemsToContainerAsync();
            //await this.QueryItemsAsync();
            //await this.ReplaceFamilyItemAsync();
            //await this.DeleteFamilyItemAsync();
            //await this.DeleteDatabaseAndCleanupAsync();
        }
        // <CreateDatabaseAsync>
        /// <summary>
        /// Create the database if it does not exist
        /// </summary>
        private async Task CreateDatabaseAsync(string databaseId)
        {
            // Create a new database
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            Console.WriteLine("Created Database: {0}\n", this.database.Id);
        }
        // </CreateDatabaseAsync>

        // <CreateContainerAsync>
        /// <summary>
        /// Create the container if it does not exist. 
        /// Specifiy "/LastName" as the partition key since we're storing family information, to ensure good distribution of requests and storage.
        /// </summary>
        /// <returns></returns>
        private async Task CreateContainerAsync(string containerId,string partitionKey)
        {
            // Create a new container
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, partitionKey, 400);
            Console.WriteLine("Created Container: {0}\n", this.container.Id);
        }
        // </CreateContainerAsync>

        // <AddItemsToContainerAsync>
        /// <summary>
        /// Add ToDoTask items to the container
        /// </summary>
        public async Task<int> AddItemsToContainerAsync(string userId,string leaveId)
        {
            LeaveDetail leave = new LeaveDetail
            {
                Id = userId,
                LeaveId = leaveId,
                
            };

            try
            {
                // Read the item to see if it exists.  
                ItemResponse<LeaveDetail> leaveResponse = await this.container.ReadItemAsync<LeaveDetail>(leave.Id, new PartitionKey(leave.LeaveId));
                Console.WriteLine("Item in database with id: {0} already exists\n", leaveResponse.Resource.Id);
                return -1;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                ItemResponse<LeaveDetail> todotaskResponse = await this.container.CreateItemAsync<LeaveDetail>(leave, new PartitionKey(leave.LeaveId));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", todotaskResponse.Resource.Id, todotaskResponse.RequestCharge);
                return 1;
            }

           

        }
        // </AddItemsToContainerAsync>

        // <QueryItemsAsync>
        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// Including the partition key value of lastName in the WHERE filter results in a more efficient query
        /// </summary>
        public async Task<bool> CheckNewUserIdAsync(string userId,string EndpointUri,string PrimaryKey,string databaseId,string containerId,string partitionKey)
        {
            await GetStartedDemoAsync(EndpointUri, PrimaryKey, databaseId, containerId, partitionKey);
            var sqlQueryText = $"SELECT * FROM c WHERE c.id = '{userId}'";

            Console.WriteLine("Running query: {0}\n", sqlQueryText);

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<LeaveDetail> queryResultSetIterator = this.container.GetItemQueryIterator<LeaveDetail>(queryDefinition);

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<LeaveDetail> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                if(currentResultSet.Count>0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
        // </QueryItemsAsync>

        // <QueryItemsAsync>
        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// Including the partition key value of lastName in the WHERE filter results in a more efficient query
        /// </summary>
        public async Task<List<LeaveDetail>> QueryItemsAsync(string userId, string EndpointUri, string PrimaryKey, string databaseId, string containerId, string partitionKey)
        {
            await GetStartedDemoAsync(EndpointUri, PrimaryKey, databaseId, containerId, partitionKey);
            var sqlQueryText = $"SELECT * FROM c WHERE c.id = '{userId}' order by c._ts desc";

            Console.WriteLine("Running query: {0}\n", sqlQueryText);

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<LeaveDetail> queryResultSetIterator = this.container.GetItemQueryIterator<LeaveDetail>(queryDefinition);

            List<LeaveDetail> todoTasks = new List<LeaveDetail>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<LeaveDetail> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (LeaveDetail task in currentResultSet)
                {
                    todoTasks.Add(task);
                    Console.WriteLine("\tRead {0}\n", task);
                }
            }
            return todoTasks;
        }
        // </QueryItemsAsync>
        public async Task<bool> DeleteTaskItemAsync(string partitionKey,string id)
        {
            var partitionKeyValue = partitionKey;
            var userId = id;

            // Delete an item. Note we must provide the partition key value and id of the item to delete
            try
            {
                ItemResponse<LeaveDetail> todoTaskResponse = await this.container.DeleteItemAsync<LeaveDetail>(userId, new PartitionKey(partitionKeyValue));
                Console.WriteLine("Deleted Task [{0},{1}]\n", partitionKeyValue, userId);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<List<LeaveDetail>> QueryItemsAsync(string userId)
        {
            var sqlQueryText = $"SELECT * FROM c WHERE c.id = '{userId}' order by c._ts asc";

            Console.WriteLine("Running query: {0}\n", sqlQueryText);

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<LeaveDetail> queryResultSetIterator = this.container.GetItemQueryIterator<LeaveDetail>(queryDefinition);

            List<LeaveDetail> todoTasks = new List<LeaveDetail>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<LeaveDetail> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (LeaveDetail task in currentResultSet)
                {
                    todoTasks.Add(task);
                    Console.WriteLine("\tRead {0}\n", task);
                }
            }
            return todoTasks;
        }
    }
}
