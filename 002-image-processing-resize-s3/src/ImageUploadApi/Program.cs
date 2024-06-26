using Amazon.S3;
using Amazon.S3.Model;

var builder = WebApplication.CreateBuilder(args);

// Add AWS Lambda support. When application is run in Lambda Kestrel is swapped out as the web server with Amazon.Lambda.AspNetCoreServer. This
// package will act as the webserver translating request and responses between the Lambda event source and ASP.NET Core.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseHttpsRedirection();

// Enable middleware to serve generated Swagger as a JSON endpoint. 
// Don't do this in production, as it's a security risk.
app.UseSwagger();
app.UseSwaggerUI();

var s3Client = new AmazonS3Client();
var bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME") ?? "fastfoodcoding-imageprocessing";
var uploadPath = Environment.GetEnvironmentVariable("UPLOAD_PATH") ?? "images/";

var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

// upload file endpoint (images)
app.MapPost("/upload", async (IFormFile file) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("File is empty");
    }

    // only allow certain file extensions
    if (!allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLower(), StringComparer.OrdinalIgnoreCase))
    {
        return Results.BadRequest("Invalid file extension");
    }

    // don't allow files larger than 25MB
    if (file.Length > 25 * 1024 * 1024)
    {
        return Results.BadRequest("File is too large");
    }

    using var inputStream = file.OpenReadStream();
    using var memoryStream = new MemoryStream();
    inputStream.CopyTo(memoryStream);
    memoryStream.Seek(0, SeekOrigin.Begin);

    var putRequest = new PutObjectRequest
    {
        BucketName = bucketName,
        Key = Path.Combine(uploadPath, file.FileName),
        InputStream = memoryStream
    };

    var response = await s3Client.PutObjectAsync(putRequest);
    return Results.StatusCode((int)response.HttpStatusCode);
})
.DisableAntiforgery(); // Disable antiforgery for this endpoint. Don't do this in production.

app.Run();
