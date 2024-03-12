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