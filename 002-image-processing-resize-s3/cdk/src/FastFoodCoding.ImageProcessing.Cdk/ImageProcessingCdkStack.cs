using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using System.Collections.Generic;

namespace FastFoodCoding.ImageProcessing.Cdk
{
    public class ImageProcessingCdkStack : Stack
    {
        internal ImageProcessingCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var bucketName = new CfnParameter(this, "BucketName", new CfnParameterProps
            {
                Type = "String",
                Description = "The name of the S3 bucket to store images",
                Default = "fastfoodcoding-imageprocessing"
            });

            var sourceFolder = new CfnParameter(this, "SourceFolder", new CfnParameterProps
            {
                Type = "String",
                Description = "The name of the folder in the S3 bucket to store images",
                Default = "images"
            });

            var destinationFolder = new CfnParameter(this, "DestinationFolder", new CfnParameterProps
            {
                Type = "String",
                Description = "The name of the folder in the S3 bucket to store resized images",
                Default = "r"
            });

            // define a public S3 bucket to store images
            var s3Bucket = new Bucket(this, "ImageBucket", new BucketProps
            {
                BucketName = bucketName.ValueAsString,
                RemovalPolicy = RemovalPolicy.DESTROY,
                BlockPublicAccess = new BlockPublicAccess(new BlockPublicAccessOptions
                {
                    BlockPublicAcls = false,
                    IgnorePublicAcls = false,
                    BlockPublicPolicy = false,
                    RestrictPublicBuckets = false
                })
            });

            // allow anyone to read objects from the bucket
            s3Bucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = ["s3:GetObject"],
                Resources = [s3Bucket.ArnForObjects("*")],
                Principals = [new StarPrincipal()]
            }));

            // allow the lambda function to read and write objects to the bucket
            var imageResizeLambdaRole = new Role(this, "ImageResizeLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies =
                [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["S3Policy"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements =
                        [
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Actions = ["s3:GetObject", "s3:PutObject"],
                                Resources = [s3Bucket.ArnForObjects("*")]
                            })
                        ]
                    })
                }
            });

            // define a lambda function to resize images
            var imageResizeLambda = new Function(this, "ImageResizeLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "ImageResizeLambda::ImageResizeLambda.Function::FunctionHandler",
                Code = Code.FromAsset("../src/ImageResizeLambda/bin/Release/net8.0/linux-arm64/publish"),
                Architecture = Architecture.ARM_64,
                MemorySize = 512,
                Timeout = Duration.Minutes(2),
                Role = imageResizeLambdaRole,
                Environment = new Dictionary<string, string>
                {
                    ["RESIZED_OBJECT_PATH"] = destinationFolder.ValueAsString
                }
            });

            // add an event notification to the bucket to trigger the lambda function when an image is uploaded.
            // prefix is used to filter the event notifications to only trigger when an image is uploaded to the source folder
            s3Bucket.AddEventNotification(EventType.OBJECT_CREATED, new LambdaDestination(imageResizeLambda), new NotificationKeyFilter
            {
                Prefix = sourceFolder.ValueAsString + "/"
            });

            // allow the API lambda function to write objects to the bucket
            var imageUploadApiLambdaRole = new Role(this, "ImageUploadApiLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies =
                [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["S3Policy"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements = [
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Actions = ["s3:PutObject"],
                                Resources = [s3Bucket.ArnForObjects("*")]
                            })
                        ]
                    })
                }
            });

            // define a lambda function to upload images
            var imageUploadApiLambda = new Function(this, "ImageUploadApiLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "ImageUploadApi",
                Code = Code.FromAsset("../src/ImageUploadApi/bin/Release/net8.0/linux-arm64/publish"),
                Architecture = Architecture.ARM_64,
                MemorySize = 512,
                Timeout = Duration.Minutes(2),
                Role = imageUploadApiLambdaRole,
                Environment = new Dictionary<string, string>
                {
                    ["BUCKET_NAME"] = bucketName.ValueAsString,
                    ["UPLOAD_PATH"] = sourceFolder.ValueAsString
                }
            });

            // define an API Gateway (HTTP) to upload images
            _ = new HttpApi(this, "ImageUploadApi", new HttpApiProps
            {
                DefaultIntegration = new HttpLambdaIntegration(
                    "ImageUploadApiIntegration",
                    imageUploadApiLambda,
                    new HttpLambdaIntegrationProps
                    {
                        PayloadFormatVersion = PayloadFormatVersion.VERSION_2_0
                    }),
                ApiName = "ImageUploadApi",
                CorsPreflight = new CorsPreflightOptions
                {
                    AllowOrigins = new[] { "*" },
                    AllowMethods = new[] { CorsHttpMethod.ANY },
                    AllowHeaders = new[] { "*" }
                },
            });
        }
    }
}
