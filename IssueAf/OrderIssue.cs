using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace IssueAf
{
    public static class OrderIssue
    {
        private const int _numberOfFans = 20;
        private const int _delayForActivityA = 20;
        private const int _delayForActivityB = 5;
        private static readonly HttpClient _client = HttpClientFactory.Create();
        private static readonly string _baseUrl = 
            "https://localhost:5001/api/values/";

        [FunctionName(nameof(OrderIssue_HttpStart))]
        public static async Task<HttpResponseMessage> OrderIssue_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(OrderIssue_Orchestration), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(OrderIssue_Orchestration))]
        public static async Task<List<string[]>> OrderIssue_Orchestration(
            [OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            try {
                if (!context.IsReplaying) log.LogInformation("~Starting outer orchestration");
                
                var list = Enumerable.Range(1, _numberOfFans);
                var provisioningTasks = new List<Task<string[]>>();

                foreach (var item in list) {
                    var provisioningTask = context.CallSubOrchestratorAsync<string[]>(nameof(InnerOrchestration), item);
                    provisioningTasks.Add(provisioningTask);
                }

                if (!context.IsReplaying) log.LogInformation("~Starting fan out of inner orchestrations");
                var outputs = await Task.WhenAll(provisioningTasks);
                if (!context.IsReplaying) log.LogInformation("~Finishing fan out of inner orchestrations");

                return outputs.ToList();
            } finally {
                log.LogInformation("~Finishing outer orchestration");
            }
        }

        [FunctionName(nameof(InnerOrchestration))]
        public static async Task<string[]> InnerOrchestration(
            [OrchestrationTrigger] DurableOrchestrationContext context, ILogger log) {
            var input = context.GetInput<int>();
            var outputs = new List<string>();
            try
            {
                if (!context.IsReplaying) log.LogInformation($"~Starting inner orchestration for {input}.");

                if (!context.IsReplaying) log.LogInformation($"~Calling ActivityA for {input}.");
                outputs.Add(await context.CallActivityAsync<string>(nameof(ActivityA), input));
                if (!context.IsReplaying) log.LogInformation($"~Finished calling ActivityA for {input}.");

                if (!context.IsReplaying) log.LogInformation($"~Calling ActivityB for {input}.");
                outputs.Add(await context.CallActivityAsync<string>(nameof(ActivityB), input));
                if (!context.IsReplaying) log.LogInformation($"~Finished calling ActivityB for {input}.");

                return outputs.ToArray();
            }
            finally
            {
                log.LogInformation($"~Finishing inner orchestration for {input}.");
            }
        }

        [FunctionName(nameof(ActivityA))]
        public static async Task<string> ActivityA([ActivityTrigger] int value, ILogger log)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                log.LogInformation($"~Starting ActivityA {value} at {startTime}.");
                var response = await _client.GetAsync($"{_baseUrl}{_delayForActivityA}");
                var endTime = DateTime.UtcNow;

                return $"ActivityA:{startTime.ToString("H:mm:ss")}-{endTime.ToString("H:mm:ss")}-api response:{response.StatusCode}";
            }
            finally
            {
                log.LogInformation($"~Completed ActivityA {value} at {DateTime.UtcNow}.");
            }
        }

        [FunctionName(nameof(ActivityB))]
        public static async Task<string> ActivityB([ActivityTrigger] int value, ILogger log)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                log.LogInformation($"~Starting ActivityB {value} at {startTime}.");
                var response = await _client.GetAsync($"{_baseUrl}{_delayForActivityB}");
                var endTime = DateTime.UtcNow;

                return $"ActivityB:{startTime.ToString("H:mm:ss")}-{endTime.ToString("H:mm:ss")}-api response:{response.StatusCode}";
            }
            finally
            {
                log.LogInformation($"~Completed ActivityB {value} at {DateTime.UtcNow}.");
            }
        }        
    }
}