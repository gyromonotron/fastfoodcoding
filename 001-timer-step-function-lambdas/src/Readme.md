# Timer using Step Functions and AWS Lambdas

This is a simple example of a timer using AWS Step Functions and AWS Lambdas.

`TimerLambda` is responsible for creating and running of a Step Function state machine that waits for a given amount of time and then calls `ExecutorLambda`.

## How to deploy

You can deploy the stack using the AWS CDK:

```bash
dotnet build -c Release

cd ../cdk/src
cdk deploy --parameters StateMachineNamePrefix=MyTimerStateMachine
```

## More information

For more information about this project, please refer to the blog post at [fastfoodcoding.com: Timer with Step Functions and AWS Lambda (.NET8)](https://fastfoodcoding.com/recipes/aws/timer-with-step-functions-and-lambdas/).
