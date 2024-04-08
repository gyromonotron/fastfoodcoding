using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Constructs;
using System.Collections.Generic;
using static Amazon.CDK.AWS.CloudFront.CfnDistribution;
using static Amazon.CDK.AWS.CloudFront.CfnOriginAccessControl;

namespace FastFoodCoding.ImageResizeEdge.Cdk
{
    public class ImageResizeEdgeCdkStack : Stack
    {
        internal ImageResizeEdgeCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var bucketName = new CfnParameter(this, "BucketName", new CfnParameterProps
            {
                Type = "String",
                Description = "The name of the S3 bucket to store images",
                Default = "fastfoodcoding-imageprocessing"
            });

            var destinationFolder = new CfnParameter(this, "DestinationFolder", new CfnParameterProps
            {
                Type = "String",
                Description = "The name of the folder in the S3 bucket to store resized images",
                Default = "resize"
            });

            var quality = new CfnParameter(this, "Quality", new CfnParameterProps
            {
                Type = "Number",
                Description = "The quality of the resized images",
                Default = 80
            });

            // define a public S3 bucket to store images
            var s3Bucket = new Bucket(this, "ImageBucket", new BucketProps
            {
                BucketName = bucketName.ValueAsString,
                RemovalPolicy = RemovalPolicy.DESTROY,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            });

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
                                Actions = ["s3:GetObject", "s3:PutObject", "s3:Listbucket"],
                                Resources = [s3Bucket.ArnForObjects("*"), s3Bucket.BucketArn]
                            })
                        ]
                    })
                }
            });

            // define a lambda function to resize images
            var imageResizeLambda = new Amazon.CDK.AWS.Lambda.Function(this, "ImageResizeLambda", new Amazon.CDK.AWS.Lambda.FunctionProps
            {
                Runtime = Runtime.PYTHON_3_11,
                Handler = "lambda_function.lambda_handler",
                // build our function with Docker since Pillow has platform-specific dependencies. In our case, we're building for linux/amd64
                Code = Code.FromDockerBuild("../src", new DockerBuildAssetOptions { Platform = "linux/amd64" }),
                Architecture = Architecture.X86_64,
                MemorySize = 512,
                Timeout = Duration.Seconds(30),
                Role = imageResizeLambdaRole
            });

            var cfnOriginAccessControl = new CfnOriginAccessControl(this, "OriginAccessControl", new CfnOriginAccessControlProps
            {
                OriginAccessControlConfig = new OriginAccessControlConfigProperty
                {
                    Name = "ImageResize-OriginAccessControl",
                    OriginAccessControlOriginType = "s3",
                    SigningBehavior = "always",
                    SigningProtocol = "sigv4"
                }
            });

            // define CloudFront distribution to serve images from the bucket
            var cfnDistribution = new Distribution(this, "ImageBucketDistribution", new DistributionProps
            {
                DefaultBehavior = new BehaviorOptions
                {
                    Origin = new S3OacOrigin(s3Bucket, new S3OriginProps
                    {
                        OriginAccessIdentity = null,
                        ConnectionAttempts = 3,
                        ConnectionTimeout = Duration.Seconds(10),
                        // since Edge@Lambda doesn't support Environment variables, we're passing those as custom headers
                        // please note, that this make sense only for the OriginRequest event type and not for the ViewerRequest
                        CustomHeaders = new Dictionary<string, string>
                        {
                            ["X-Env-Resized-Path"] = destinationFolder.ValueAsString,
                            ["X-Env-Bucket-Name"] = bucketName.ValueAsString,
                            ["X-Env-Quality"] = quality.ValueAsString
                        }
                    }),
                    CachePolicy = new CachePolicy(this, "ImageBucketCachePolicy", new CachePolicyProps
                    {
                        QueryStringBehavior = CacheQueryStringBehavior.AllowList("size", "to_webp"),
                        DefaultTtl = Duration.Days(1),
                        MaxTtl = Duration.Days(365),
                        MinTtl = Duration.Seconds(1),
                        EnableAcceptEncodingGzip = true
                    }),
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    EdgeLambdas = new[]
                    {
                        new EdgeLambda
                        {
                            EventType = LambdaEdgeEventType.ORIGIN_REQUEST,
                            FunctionVersion = imageResizeLambda.CurrentVersion
                        }
                    }
                }
            });

            s3Bucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = ["s3:GetObject"],
                Principals = [new ServicePrincipal("cloudfront.amazonaws.com")],
                Effect = Effect.ALLOW,
                Resources = [s3Bucket.ArnForObjects("*")],
                Conditions = new Dictionary<string, object>
                {
                    ["StringEquals"] = new Dictionary<string, object>
                    {
                        ["AWS:SourceArn"] = $"arn:aws:cloudfront::{this.Account}:distribution/{cfnDistribution.DistributionId}"
                    }
                }
            }));

            // workaround using the L1 construct to attach the OriginAccessControl to the CloudFront Distribution
            var l1CfnDistribution = cfnDistribution.Node.DefaultChild as CfnDistribution;
            l1CfnDistribution.AddPropertyOverride("DistributionConfig.Origins.0.OriginAccessControlId", cfnOriginAccessControl.AttrId);
        }
    }

    public class S3OacOrigin : OriginBase
    {
        public S3OacOrigin(IBucket bucket, IOriginProps props = null) : base(bucket.BucketRegionalDomainName, props)
        {
        }

        // workaround to avoid the "OriginAccessIdentity" property to be rendered in the CloudFormation template
        protected override IS3OriginConfigProperty RenderS3OriginConfig()
        {
            return new S3OriginConfigProperty
            {
                OriginAccessIdentity = ""
            };
        }
    }
}
