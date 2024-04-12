# CDK Project for hosting Hugo static site on AWS S3 and CloudFront

This project is a simple example of how to use AWS CDK to host a Hugo static site on AWS S3 and CloudFront.
Running this CDK deployment will:
- create an S3 bucket,
- upload the Hugo site files to the bucket,
- create a CloudFront distribution to serve the site,
- create a CloudFront Function to allow pretty URLs.

## Prerequisites

- AWS CDK installed and configured
- Hugo
- .Net 8 SDK

## How to deploy

```
cd ../mysite
hugo --minify --environment production

cd ../cdk
cdk deploy --parameters BucketName=fastfoodcoding-hugo
```

## More information

For more information about this project, please refer to the blog post at [fastfoodcoding.com: Hosting Hugo on AWS with S3 and CloudFront](https://fastfoodcoding.com/recipes/hugo/hosting-hugo-on-aws-s3-cloudfront/).
