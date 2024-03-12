using Amazon.Lambda.Core;
using System.Text.Json.Nodes;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ExecutorLambda;

public class Function
{
    public void FunctionHandler(JsonObject input, ILambdaContext context)
    {
        context.Logger.LogLine($"Entry Time: {DateTime.UtcNow}");
        context.Logger.LogLine($"Input: {input}");

        var runAt = input.TryGetPropertyValue("RunAt", out var runAtValue) ? runAtValue!.GetValue<DateTime>() : DateTime.MinValue;
        TimeSpan delay = runAt - DateTime.UtcNow;

        // If the delay is negative, the time has already passed
        if (delay.TotalMilliseconds > 0)
        {
            // Wait for the delay to run at the exact time
            Task.Delay(delay).Wait();
            context.Logger.LogLine($"Run Time: {DateTime.UtcNow}");

            // Here you can do anything you want with the input
            // For example, you can call another Lambda function
            // or call a REST API
            // or call a database
        }
    }
}
