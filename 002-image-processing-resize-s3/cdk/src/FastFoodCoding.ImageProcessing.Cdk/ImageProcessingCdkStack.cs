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

            s3Bucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = ["s3:GetObject"],
                Resources = [s3Bucket.ArnForObjects("*")],
                Principals = [new StarPrincipal()]
            }));

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

            s3Bucket.AddEventNotification(EventType.OBJECT_CREATED, new LambdaDestination(imageResizeLambda), new NotificationKeyFilter
            {
                Prefix = sourceFolder.ValueAsString + "/"
            });

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
