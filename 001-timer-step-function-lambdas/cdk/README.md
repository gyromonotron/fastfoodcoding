# CDK Project for Timer using Step Functions and Lambda

This is an example of how to deploy the Timer using Step Functions and Lambda using the AWS CDK.

## What is it?

CDK allows you to design AWS resources using familiar programming languages, enabling you to take advantage of the power of modern programming languages to define your AWS environment in a predictable and efficient manner.

## How to use it?

1. Build the Lambda functions first:
```
cd ../src
dotnet build --c Release
```

2. Deploy the CDK stack:
```
cdk deploy --parameters StateMachineNamePrefix=MyTimerStateMachine
```
