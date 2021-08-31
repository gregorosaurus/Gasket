using Azure.Identity;
using System;
using System.Collections.Generic;
using Microsoft.Azure.Management.Subscription;
using System.Threading.Tasks;
using Microsoft.Rest;
using Microsoft.Azure;
using Microsoft.Azure.Management.Subscription.Models;
using System.Linq;
using Gasket.Extensions;
using System.Text.RegularExpressions;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using CsvHelper;
using System.Globalization;
using System.IO;

namespace Gasket
{
    /// <summary>
    /// Used to ask the client where to search for 
    /// Pipeline runs. 
    /// </summary>
    enum PipelineType
    {
        Synapse,
        DataFactory
    }

    class Program
    {
        private static readonly int NumberOfDaysToRetrieve = 30;

        static async Task Main(string[] args)
        {
            var azureCredential = new InteractiveBrowserCredential();
            var accessToken = await azureCredential.GetTokenAsync(new Azure.Core.TokenRequestContext(new string[] { "https://management.azure.com/.default" }));
            TokenCredentials azureTokenCredentials = new TokenCredentials(accessToken.Token);
            var availabeSubscriptions = await FindAvailableSubscriptions(azureTokenCredentials);

            var selectedSubscription = SelectSubscription(availabeSubscriptions);
            Console.WriteLine($"Working subscription set: {selectedSubscription.DisplayName}");

            var searchArea = PipelineType.Synapse; //temp until ADF support is added. SelectSearchArea();

            if(searchArea == PipelineType.Synapse)
            {
                Console.WriteLine("Finding Synapse workspaces in subscription...");
                var workspaces = await GetResourcesOfType(selectedSubscription.SubscriptionId, "Microsoft.Synapse/workspaces");
                var selectedWorkspace = SelectResource(workspaces);

                var outputPath = GetOutputPath();
                if (string.IsNullOrEmpty(Path.GetExtension(outputPath)))
                {
                    //just a directory, write to the file under the directory 
                    outputPath = Path.Join(outputPath, $"{selectedWorkspace.Name}_activities_{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.csv");
                }
                using (var writer = new StreamWriter(outputPath))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    //we do this write away so that we can see if we're going to have an IO error 
                    csv.WriteHeader(typeof(PipelineActivityCost));
                    csv.Flush();

                    //now that we have a workspace to analyze, let's go do it!
                    var costs = await ProduceReportOnSynapseWorkspace(selectedSubscription.SubscriptionId, selectedWorkspace.Name, azureCredential);
                    csv.WriteRecords(costs);
                }

            }
        }

        #region Processing
        private async static Task<List<PipelineActivityCost>> ProduceReportOnSynapseWorkspace(string subscriptionId, string workspaceName, Azure.Core.TokenCredential azureCredential)
        {
            List<PipelineActivityCost> costs = new List<PipelineActivityCost>();
            SynapsePipelineQueryer synapsePipelineQueryer = new SynapsePipelineQueryer(subscriptionId,workspaceName, azureCredential);
            Console.WriteLine("Finding pipeline runs in workspace...");
            var pipelineRuns = await synapsePipelineQueryer.FindPipelineRuns(workspaceName, DateTime.UtcNow.AddDays(-NumberOfDaysToRetrieve), DateTime.UtcNow);
            Console.WriteLine("Now finding activity data in each pipeline run");

            foreach (var pipelineRun in pipelineRuns)
            {
                Console.WriteLine($"Processing pipeline run {pipelineRun.RunId} for pipeline {pipelineRun.PipelineName}");
                var activityRuns = await synapsePipelineQueryer.FindActivityRunsInPipeline(workspaceName, pipelineRun, DateTime.UtcNow.AddDays(-NumberOfDaysToRetrieve), DateTime.UtcNow);
                foreach (var activityRun in activityRuns)
                {
                    if (activityRun.Output == null)
                        continue;

                    var outputDictionary = activityRun.Output as Dictionary<string, object>;
                    if (outputDictionary == null || !outputDictionary.ContainsKey("billingReference"))
                        continue;

                    var billingReference = outputDictionary["billingReference"] as Dictionary<string, object>;
                    if (billingReference == null)
                        continue;

                    if (!billingReference.ContainsKey("activityType") || !billingReference.ContainsKey("billableDuration"))
                        continue;

                    var activityType = billingReference["activityType"];

                    var billableDurationArray = billingReference["billableDuration"] as object[];
                    if (billableDurationArray == null)
                        continue;

                    var billableDuration = billableDurationArray.First() as Dictionary<string, object>;
                    if (billableDuration == null)
                        continue;

                    var billableDurationHours = billableDuration["duration"] as double?;
                    var billableMetreType = billableDuration["meterType"] as string;
                    var billableUnit = billableDuration["unit"] as string;

                    //we have a cost for this activity, create a cost object and add it to the list. 
                    PipelineActivityCost activityCost = new PipelineActivityCost()
                    {
                        ActivityType = activityType as string,
                        ActivityStartTime = activityRun.ActivityRunStart?.UtcDateTime,
                        ActivityEndTime = activityRun.ActivityRunEnd?.UtcDateTime,
                        PipelineName = pipelineRun.PipelineName,
                        PipelineRunId = pipelineRun.RunId,
                        BilledDurationHours = billableDurationHours ?? 0,
                        BilledMetreType = billableMetreType,
                        BilledUnit = billableUnit,
                    };
                    costs.Add(activityCost);
                }
            }
            return costs;
        }
        #endregion

        #region Azure Queries
        private static async Task<List<SubscriptionModel>> FindAvailableSubscriptions(TokenCredentials credentials)
        {
            var subscriptionClient = new SubscriptionClient(credentials);
            var subscriptionsResult = await subscriptionClient.Subscriptions.ListAsync();

            return subscriptionsResult.GetEnumerator().ToIEnumerable().OrderBy(x => x.DisplayName).ToList();

        }

        private static async Task<List<GenericResourceExpanded>> GetResourcesOfType(string subscriptionId, string resourceType)
        {
            List<GenericResourceExpanded> resourcesFound = new List<GenericResourceExpanded>();
            var resourceClient = new ResourcesManagementClient(subscriptionId, new DefaultAzureCredential());
            await foreach (var resource in resourceClient.Resources.ListAsync($"resourceType eq '{resourceType}'"))
            {
                resourcesFound.Add(resource);
            }
            return resourcesFound;
        }

        #endregion

        #region Console UI
        private static string GetOutputPath()
        {
            Console.Write("*Output path (.\\):");
            var input = Console.ReadLine();
            return input;
        }

        private static PipelineType SelectSearchArea()
        {
            while(true)
            {
                Console.Write("Select Pipeline Type (adf/synapse):");
                string pipelineTypeInput = Console.ReadLine();
                if (pipelineTypeInput.Length > 0 && 
                    (pipelineTypeInput[0] == 's' || pipelineTypeInput[0] == 'S' || pipelineTypeInput.ToLower().Contains("synapse")))
                {
                    return PipelineType.Synapse;
                }
                else if (pipelineTypeInput.Length > 0 && 
                    (pipelineTypeInput[0] == 'f' || pipelineTypeInput[0] == 'F' || pipelineTypeInput.ToLower().Contains("adf")))
                {
                    return PipelineType.DataFactory;
                }
            }
        }

        private static GenericResourceExpanded SelectResource(List<GenericResourceExpanded> resources)
        {
            while (true)
            {
                var index = 1;
                foreach (var resource in resources)
                {
                    Console.WriteLine($"{index}.\t{resource.Name}");
                    index += 1;
                }
                Console.Write("Select a Resource: ");
                string subscriptionNumberInput = Console.ReadLine();
                Match subscriptionNumberMatch = Regex.Match(subscriptionNumberInput, @"\s*(\d+)[\s\.]*");
                if (subscriptionNumberMatch.Success)
                {
                    return resources[int.Parse(subscriptionNumberMatch.Groups[1].Value) - 1];
                }
                else
                {
                    Console.WriteLine("Invalid selection.");
                }
            }
        }

        private static SubscriptionModel SelectSubscription(List<SubscriptionModel> subscriptions)
        {
            while (true) {
                WriteHeader("Subscriptions");
                var index = 1;
                foreach (var subscription in subscriptions)
                {
                    Console.WriteLine($"{index}.\t{subscription.DisplayName} ({subscription.SubscriptionId})");
                    index += 1;
                }
                Console.Write("Select a Subscription: ");
                string subscriptionNumberInput = Console.ReadLine();
                Match subscriptionNumberMatch = Regex.Match(subscriptionNumberInput, @"\s*(\d+)[\s\.]*");
                if (subscriptionNumberMatch.Success)
                {
                    return subscriptions[int.Parse(subscriptionNumberMatch.Groups[1].Value) - 1];
                }else
                {
                    Console.WriteLine("Invalid selection.");
                }
            }
            
        }

        private static void WriteHeader(string header)
        {
            Console.WriteLine(header);
        }
        #endregion

    }
}
