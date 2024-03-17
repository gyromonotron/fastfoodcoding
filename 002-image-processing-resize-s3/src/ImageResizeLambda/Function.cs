using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using SkiaSharp;
using System.Text.Json;
using static Amazon.Lambda.S3Events.S3Event;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImageResizeLambda;

public class Function
{
    private readonly IAmazonS3 _s3Client;

    // Predefined sizes for images
    private readonly List<(int Width, int Height, string Label)> _wideSizes = [(1280, 720, "L"), (1200, 628, "M"), (854, 480, "S"), (427, 240, "XS")];
    private readonly List<(int Width, int Height, string Label)> _tallSizes = [(720, 1280, "L"), (628, 1200, "M"), (480, 854, "S"), (240, 427, "XS")];
    private readonly List<(int Width, int Height, string Label)> _squareSizes = [(1080, 1080, "SL"), (540, 540, "SM"), (360, 360, "SS"), (180, 180, "SXS")];

    private readonly string _resizedObjectPath;
    private readonly bool _convertToWebp;
    private readonly int _convertQuality;

    public Function() : this(new AmazonS3Client())
    {
    }

    public Function(IAmazonS3 s3Client)
    {
        this._s3Client = s3Client;
        _resizedObjectPath = Environment.GetEnvironmentVariable("RESIZED_OBJECT_PATH") ?? "/resized/";
        _convertToWebp = bool.TryParse(Environment.GetEnvironmentVariable("CONVERT_TO_WEBP"), out bool convert) && convert;
        _convertQuality = int.TryParse(Environment.GetEnvironmentVariable("CONVERT_QUALITY"), out int quality) ? quality : 100;
    }

    public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        context.Logger.LogLine($"Received S3 event: {JsonSerializer.Serialize(evnt)}");
        var eventRecords = evnt.Records ?? new List<S3EventNotificationRecord>();
        foreach (var s3EventEntity in eventRecords.Select(r => r.S3))
        {
            try
            {
                using var streamResponse = await this._s3Client.GetObjectStreamAsync(s3EventEntity.Bucket.Name, s3EventEntity.Object.Key, null);
                using var memoryStream = new MemoryStream();
                streamResponse.CopyTo(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                using var bitmap = SKBitmap.Decode(memoryStream);

                if (bitmap == null)
                {
                    context.Logger.LogError($"Error decoding object {s3EventEntity.Object.Key} from bucket {s3EventEntity.Bucket.Name}.");
                    continue;
                }

                var sizes = bitmap.Height > bitmap.Width ? _tallSizes : _wideSizes;
                foreach (var size in sizes)
                {
                    await ResizeAndPut(s3EventEntity, bitmap, size.Width, size.Height, size.Label, context.Logger);
                }

                foreach (var size in _squareSizes)
                {
                    await ResizeAndPut(s3EventEntity, bitmap, size.Width, size.Height, size.Label, context.Logger);
                }
            }
            catch (Exception e)
            {
                context.Logger.LogError($"Error getting object {s3EventEntity.Object.Key} from bucket {s3EventEntity.Bucket.Name}.");
                context.Logger.LogError(e.Message);
                context.Logger.LogError(e.StackTrace);
                throw;
            }
        }
    }

    private async Task ResizeAndPut(S3Entity s3EventEntity, SKBitmap bitmap, int width, int height, string sizeLabel, ILambdaLogger logger)
    {
        string filePath = Path.GetDirectoryName(s3EventEntity.Object.Key) ?? string.Empty;
        string fileExtension = Path.GetExtension(s3EventEntity.Object.Key);
        string filename = Path.GetFileNameWithoutExtension(s3EventEntity.Object.Key) + (_convertToWebp ? ".webp" : fileExtension);
        string destination = Path.Combine(_resizedObjectPath, filePath, sizeLabel, filename);

        logger.LogLine($"Resizing {s3EventEntity.Object.Key} to {width}x{height} and putting it to {destination}");

        try
        {
            using var resizedBitmap = bitmap.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
            using var image = SKImage.FromBitmap(resizedBitmap);
            using var data = image.Encode(GetEncodedImageFormat(_convertToWebp, fileExtension), _convertQuality);
            using var ms = new MemoryStream();
            data.SaveTo(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var request = new PutObjectRequest
            {
                BucketName = s3EventEntity.Bucket.Name,
                Key = destination,
                InputStream = ms
            };

            await this._s3Client.PutObjectAsync(request);
        }
        catch (Exception e)
        {
            logger.LogError($"Error processing {s3EventEntity.Object.Key}: Destination: {destination}, Width: {width}, Height: {height}");
            logger.LogError(e.Message);
            logger.LogError(e.StackTrace);
        }
    }

    // Helper method to get the image format based on the file extension and the convertToWebp flag
    private static SKEncodedImageFormat GetEncodedImageFormat(bool convertToWebp, string fileExtension) =>
    convertToWebp ? SKEncodedImageFormat.Webp : fileExtension.ToLower() switch
    {
        ".png" => SKEncodedImageFormat.Png,
        ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
        ".gif" => SKEncodedImageFormat.Gif,
        ".bmp" => SKEncodedImageFormat.Bmp,
        ".wbmp" => SKEncodedImageFormat.Wbmp,
        ".dng" => SKEncodedImageFormat.Dng,
        ".heif" or ".heic" => SKEncodedImageFormat.Heif,
        ".webp" => SKEncodedImageFormat.Webp,
        _ => SKEncodedImageFormat.Png
    };
}
