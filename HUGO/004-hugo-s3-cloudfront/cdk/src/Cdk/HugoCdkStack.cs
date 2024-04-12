using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Constructs;
using System.Collections.Generic;
using static Amazon.CDK.AWS.CloudFront.CfnDistribution;
using static Amazon.CDK.AWS.CloudFront.CfnOriginAccessControl;

namespace Cdk
{
    public class HugoCdkStack : Stack
    {
        internal HugoCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var bucketNameParam = new CfnParameter(this, "BucketName", new CfnParameterProps
            {
                Type = "String",
                Description = "The name of the bucket to host the HUGO website",
                Default = "fastfoodcoding-hugo"
            });

            // uncomment the following lines to enable custom domain and ACM certificate
            //var domainNameParam = new CfnParameter(this, "DomainName", new CfnParameterProps
            //{
            //    Type = "String",
            //    Description = "The domain name to use for the CloudFront distribution. Default is null",
            //});

            //var certificateArnParam = new CfnParameter(this, "CertificateArn", new CfnParameterProps
            //{
            //    Type = "String",
            //    Description = "The ARN of the ACM certificate to use for the CloudFront distribution. Default is null",
            //});

            var s3Bucket = new Bucket(this, "HugoSiteBucket", new BucketProps
            {
                BucketName = bucketNameParam.ValueAsString,
                RemovalPolicy = RemovalPolicy.DESTROY,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            });

            var cfnOriginAccessControl = new CfnOriginAccessControl(this, "OriginAccessControl", new CfnOriginAccessControlProps
            {
                OriginAccessControlConfig = new OriginAccessControlConfigProperty
                {
                    Name = "HugoSiteBucket-OriginAccessControl",
                    OriginAccessControlOriginType = "s3",
                    SigningBehavior = "always",
                    SigningProtocol = "sigv4"
                }
            });

            // Hugo uses pretty URLs instead of index.html for every path
            // S3 or CloudFront does not support this out of the box
            // Let's use a CloudFront lightweight function on Viewer Request event to achieve that
            // As a bonus, for consistency we can redirect www to non-www and fix trailing slashes in URLs
            var redirectFunction = new Function(this, "RedirectFunction", new FunctionProps
            {
                FunctionName = "HugoSiteViewerRequestFunction",
                Runtime = FunctionRuntime.JS_2_0,
                Comment = "Redirect to index.html if the request is for a directory",
                Code = FunctionCode.FromInline(GetFunctionCode()),
            });

            var cfnDistribution = new Distribution(this, "HugoSiteDistribution", new DistributionProps
            {
                DefaultRootObject = "index.html",
                DefaultBehavior = new BehaviorOptions
                {
                    Origin = new S3OacOrigin(s3Bucket, new S3OriginProps
                    {
                        OriginAccessIdentity = null,
                        ConnectionAttempts = 3,
                        ConnectionTimeout = Duration.Seconds(10)
                    }),
                    CachedMethods = CachedMethods.CACHE_GET_HEAD,
                    CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    FunctionAssociations = new[]
                    {
                        new FunctionAssociation
                        {
                            EventType = FunctionEventType.VIEWER_REQUEST,
                            Function = redirectFunction
                        }
                    }
                },
                // uncomment the following lines to enable custom domain and ACM certificate
                //DomainNames = bucketNameParam.ValueAsString,
                //Certificate = Certificate.FromCertificateArn(this, "Certificate", certificateArnParam.ValueAsString)
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

            // sync our Hugo website public/ folder with the S3 bucket
            // don't forget to run "hugo" before deploying the CDK stack
            var _ = new BucketDeployment(this, "DeployWebSite", new BucketDeploymentProps
            {
                // sync the contents of the public/ folder with the S3 bucket
                Sources = [Source.Asset("../mysite/public")],
                DestinationBucket = s3Bucket,
                // invalidate the cache on the CloudFront distribution when the website is updated
                Distribution = cfnDistribution,
                DistributionPaths = ["/*"],
            });
        }

        private static string GetFunctionCode()
        {
            return @"
             async function handler(event) {
                const request = event.request;
                const host = request.headers.host.value;
                const uri = request.uri;

                if (!request.uri.endsWith('/') && !request.uri.includes('.')) {
                    request.uri += '/';
                    return redirectResponse('https://' + host + request.uri);
                }

                if (host.startsWith('www.')) {
                    return redirectResponse('https://' + host.replace('www.', '') + request.uri);
                }

                if (uri.endsWith('/')) {
                    request.uri += 'index.html';
                }

                return request;
            }

            function redirectResponse(newurl) {
                return {
                    statusCode: 301,
                    statusDescription: 'Moved Permanently',
                    headers: { 'location': { 'value': newurl } }
                };
            }";
        }

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
