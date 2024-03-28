# CDK Project for image resize on request using S3, CloudFront, and Lambda@Edge

This project is a simple example of how to use AWS CDK to create a serverless image resizing service using:
- S3 to store the original and processed images.
- CloudFront to access and cache the resized images.
- Lambda@Edge to resize the images on request.

## Prerequisites

- AWS CDK installed and configured.
- Docker installed and running. This is required to build the Lambda function.

## How to deploy

```
cdk deploy --parameters BucketName=fastfoodcoding-edge --parameters DestinationFolder=resized --parameters Quality=80
```

## More information

For more information about this project, please refer to the blog post at [fastfoodcoding.com](https://fastfoodcoding.com/docs/aws/resize-images-cloudfront-lambda-edge/).
