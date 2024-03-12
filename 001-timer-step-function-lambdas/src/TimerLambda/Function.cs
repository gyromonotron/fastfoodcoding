using Amazon.Lambda.Core;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using System.Text.Json;
using System.Text.Json.Nodes;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TimerLambda;

public class Function
{
    private readonly IAmazonStepFunctions _stepFunctionsClient;
    private readonly string? _targetLambdaArn;
    private readonly int _targetLambdaCallBeforeSec;
    private readonly string? _statMachineRoleArn;
    private readonly string _stateMachineNamePrefix;

    public Function()
    {
        _stepFunctionsClient = new AmazonStepFunctionsClient();
        _targetLambdaArn = Environment.GetEnvironmentVariable("TARGET_LAMBDA_ARN");
        _targetLambdaCallBeforeSec = int.TryParse(Environment.GetEnvironmentVariable("TARGET_LAMBDA_CALL_BEFORE_SEC"), out int result) ? result : 0;
        _stateMachineNamePrefix = Environment.GetEnvironmentVariable("STATE_MACHINE_NAME_PREFIX") ?? "TimerStateMachine";
        _statMachineRoleArn = Environment.GetEnvironmentVariable("STATE_MACHINE_ROLE_ARN");
    }

    public async Task<object> FunctionHandler(Input input, ILambdaContext context)
    {
        context.Logger.LogLine($"FunctionHandler: {JsonSerializer.Serialize(input)}");

        var inputObject = input;
        if (inputObject == null)
        {
            throw new ArgumentException("Input cannot be null");
        }

        var waitTime = inputObject.RunAt.Subtract(DateTime.UtcNow);
        context.Logger.LogLine($"WaitTime: {waitTime}");
        if (waitTime < TimeSpan.Zero)
        {
            throw new ArgumentException("RunAt time cannot be in the past");
        }

        if (string.IsNullOrEmpty(_targetLambdaArn))
        {
            throw new ArgumentException("TARGET_LAMBDA_ARN environment variable not found");
        }

        var createStateMachineRequest = new CreateStateMachineRequest
        {
            Name = $"{_stateMachineNamePrefix}-{Guid.NewGuid()}",
            Definition = GetStateMachineDefinition(inputObject.RunAt, _targetLambdaArn, _targetLambdaCallBeforeSec, inputObject.Payload ?? "{}"),
            RoleArn = _statMachineRoleArn
        };

        context.Logger.LogLine($"CreateStateMachineRequest: {JsonSerializer.Serialize(createStateMachineRequest)}");

        var createStateMachineResult = await _stepFunctionsClient.CreateStateMachineAsync(createStateMachineRequest);
        var startExecutionRequest = new StartExecutionRequest
        {
            StateMachineArn = createStateMachineResult.StateMachineArn
        };

        return await _stepFunctionsClient.StartExecutionAsync(startExecutionRequest);
    }

    private static string GetStateMachineDefinition(DateTime runAtUtc, string targetLambdaArn, int targetLambdaCallBeforeSec, string payloadJson)
    {
        string waitUntil = runAtUtc.Subtract(TimeSpan.FromSeconds(targetLambdaCallBeforeSec)).ToString("yyyy-MM-ddTHH:mm:ssZ");
        string exactRunAt = runAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var stateMachineDefinition = new JsonObject
        {
            ["Comment"] = "Timer Definition",
            ["StartAt"] = "Wait",
            ["States"] = new JsonObject
            {
                ["Wait"] = new JsonObject
                {
                    ["Type"] = "Wait",
                    ["Next"] = "Lambda Invoke",
                    ["Timestamp"] = waitUntil
                },
                ["Lambda Invoke"] = new JsonObject
                {
                    ["Type"] = "Task",
                    ["Resource"] = "arn:aws:states:::lambda:invoke",
                    ["OutputPath"] = "$.Payload",
                    ["Parameters"] = new JsonObject
                    {
                        ["FunctionName"] = targetLambdaArn,
                        ["Payload"] = new JsonObject
                        {
                            ["RunAt"] = exactRunAt,
                            ["Payload"] = payloadJson
                        }
                    },
                    ["End"] = true
                }
            }
        };

        return stateMachineDefinition.ToJsonString();
    }

    public sealed class Input
    {
        public DateTime RunAt { get; set; }
        public string? Payload { get; set; }
    }
}
