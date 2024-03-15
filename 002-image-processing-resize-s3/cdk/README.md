# CDK Project for image resizing using S3, Lambdas and API Gateway

This project is a simple example of how to use AWS CDK to create a serverless image resizing service using:
- S3 to store the original and processed images.
- Lambda function to resize the images.
- API Gateway to expose the Lambda function as a HTTP API for image upload.

## How to use it?

1. Build the Lambda functions first:
```
cd ../src
dotnet publish -c Release -r linux-arm64
```

2. Deploy the CDK stack:
```
cdk deploy --parameters BucketName=fastfoodcoding-imageprocessing SourceFolder=images DestinationFolder=resized
```

## More information

For more information about this project, please refer to the blog post at [fastfoodcoding.com](https://fastfoodcoding.com/docs/aws/image-processing-and-resize/).
