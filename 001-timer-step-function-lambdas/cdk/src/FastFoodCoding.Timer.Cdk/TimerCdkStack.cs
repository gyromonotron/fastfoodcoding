using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using System.Collections.Generic;
using Constructs;
using Amazon.CDK.AWS.IAM;

namespace FastFoodCoding.Timer.Cdk
{
    public class TimerCdkStack : Stack
    {
        internal TimerCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var stateMachineNamePrefix = new CfnParameter(this, "StateMachineNamePrefix", new CfnParameterProps
            {
                Type = "String",
                Description = "The prefix for the state machine name",
                Default = "TimerStateMachine"
            });

            var targetLambdaBeforeSec = new CfnParameter(this, "TargetLambdaBeforeSec", new CfnParameterProps
            {
                Type = "Number",
                Description = "The number of seconds before the exact time the target lambda is called to handle cold start",
                Default = 10
            });

            #region Executor Callback Lambda

            // Extend this role to allow the Executor Lambda to call other resources
            var executorLambdaRole = new Role(this, "ExecutorLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies =
                [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                ]
            });

            var executorLambda = new Function(this, "ExecutorLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "ExecutorLambda::ExecutorLambda.Function::FunctionHandler",
                Code = Code.FromAsset("../src/ExecutorLambda/bin/Release/net8.0/linux-arm64/publish"),
                Architecture = Architecture.ARM_64,
                MemorySize = 256,
                Timeout = Duration.Minutes(2),
                Role = executorLambdaRole
            });

            #endregion

            #region Timer Lambda

            var stateMachineRole = new Role(this, "StateMachineRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("states.amazonaws.com"),
                ManagedPolicies =
                [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["StepFunctionPolicy"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements = new[]
                        {
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Actions = new[] { "lambda:InvokeFunction" },
                                Resources = executorLambda.ResourceArnsForGrantInvoke
                            })
                        }
                    })
                }
            });

            var timerLambdaRole = new Role(this, "TimerLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = new IManagedPolicy[]
                {
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                },
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["StepFunctionPolicy"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements =
                        [
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Actions = new[] { "states:CreateStateMachine", "states:StartExecution" },
                                Resources = new[] { "*" }
                            }),
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Actions = new[] { "iam:PassRole" },
                                Resources = new[] { stateMachineRole.RoleArn }
                            })
                        ]
                    })
                }
            });

            _ = new Function(this, "TimerLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "TimerLambda::TimerLambda.Function::FunctionHandler",
                Code = Code.FromAsset("../src/TimerLambda/bin/Release/net8.0/linux-arm64/publish"),
                Architecture = Architecture.ARM_64,
                MemorySize = 256,
                Timeout = Duration.Minutes(2),
                Environment = new Dictionary<string, string>
                {
                    ["TARGET_LAMBDA_ARN"] = executorLambda.FunctionArn,
                    ["TARGET_LAMBDA_CALL_BEFORE_SEC"] = targetLambdaBeforeSec.ValueAsString,
                    ["STATE_MACHINE_ROLE_ARN"] = stateMachineRole.RoleArn,
                    ["STATE_MACHINE_NAME_PREFIX"] = stateMachineNamePrefix.ValueAsString,
                },
                Role = timerLambdaRole,
            });

            #endregion
        }
    }
}
