using Microsoft.Azure.Management.Synapse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gasket.Extensions;
using Azure.Analytics.Synapse.Artifacts;
using Azure.Analytics.Synapse.Artifacts.Models;

namespace Gasket
{
    public class SynapsePipelineQueryer : PipelineQueryer
    {
        const string SynapseworkspaceDevSuffix = "dev.azuresynapse.net";
        private Azure.Core.TokenCredential _credentials;
        private string _subscriptionId;

        private PipelineRunClient _pipelineRunClient;
        private PipelineClient _pipelineClient;
        public SynapsePipelineQueryer(string subscriptionId, string workspaceName, Azure.Core.TokenCredential credentials)
        {
            _credentials = credentials;
            _subscriptionId = subscriptionId;

            _pipelineClient = new PipelineClient(new Uri($"https://{workspaceName}.{SynapseworkspaceDevSuffix}"), _credentials);
            _pipelineRunClient = new PipelineRunClient(new Uri($"https://{workspaceName}.{SynapseworkspaceDevSuffix}"), _credentials);
        }

        public async Task<List<PipelineResource>> FindPipelinesInWorkspace()
        {
            List<PipelineResource> pipelines = new List<PipelineResource>();
           
            await foreach(var pipeline in _pipelineClient.GetPipelinesByWorkspaceAsync())
            {
                pipelines.Add(pipeline);
            }
            return pipelines;
        }

        public async Task<List<PipelineRun>> FindPipelineRuns(string workspaceName, DateTime start, DateTime end)
        {
            var pipelineResult = await _pipelineRunClient.QueryPipelineRunsByWorkspaceAsync(new RunFilterParameters(start, end));

            return pipelineResult.Value.Value.ToList();
        }

        public async Task<List<ActivityRun>> FindActivityRunsInPipeline(string workspaceName, PipelineRun pipelineRun, DateTime start, DateTime end)
        {
            PipelineRunClient pipelineRunClient = new PipelineRunClient(new Uri($"https://{workspaceName}.{SynapseworkspaceDevSuffix}"), _credentials);
            var activityResult = await pipelineRunClient.QueryActivityRunsAsync(pipelineRun.PipelineName, pipelineRun.RunId, new RunFilterParameters(start, end));
            return activityResult.Value.Value.ToList();
        }
    }
}
