# CDK Project for Timer using Step Functions and Lambda

This is an example of how to deploy the Timer using Step Functions and Lambda using the AWS CDK.

## How to use it?

1. Build the Lambda functions first:
```
cd ../src
dotnet publish -c Release -r linux-arm64
```

2. Deploy the CDK stack:
```
cdk deploy --parameters StateMachineNamePrefix=MyTimerStateMachine
```

## More information

For more information about this project, please refer to the blog post at [fastfoodcoding.com: Timer with Step Functions and AWS Lambda (.NET8)](https://fastfoodcoding.com/recipes/aws/timer-with-step-functions-and-lambdas/).
