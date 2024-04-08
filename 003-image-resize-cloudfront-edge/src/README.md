# Image resize using Lambda@Edge and CloudFront

> **Disclaimer:** This project is for educational purposes only. Do not use it in production without proper testing and security checks. More information can be found on [Resize images on CloudFront using Lambda@Edge (Python/Pillow)](https://fastfoodcoding.com/recipes/aws/image-resize/resize-on-request-cloudfront-edge/).

This is a simple example how to resize images on CloudFront Origin Request using Lambda@Edge.
The function is written in Python and uses [Pillow](https://pillow.readthedocs.io/) to process the images.

## Build

To build the function correctly for Lambda@Edge environment you need to build it in an Linux environment. For this reason, we are using Docker with the Amazon Linux 2023 image and Python 3.11 pre-installed.

To get assets from the container, you can use the following commands or simply run the `build-docker.sh` script:

```bash
docker build -t lambda-edge:latest .
docker create --name lambda-edge-container lambda-edge:latest
docker container cp lambda-edge-container:/assets .
docker container rm lambda-edge-container
docker image rm lambda-edge
```

As a result, you will have the `assets` folder with the function code and dependencies.

## Parameters

Since Lambda@Edge does not support Environment variables, the parameters are passed as custom headers values as part of the request from CloudFront. The following parameters are supported:
- `x-env-bucket-name` - the name of the S3 bucket where the original and resized images are stored.
- `x-env-resized-path` - the folder where the resized images are stored.
- `x-env-quality` - the quality of the resized image.

## Usage

Once someone requests an image from CloudFront, the Lambda@Edge function will be triggered. The function will check if the requested resized image is already exist. If not, it will resize the image and store it in the S3 bucket. The resized image will be served to the user.

Request an image from CloudFront like this:

```
https://xxxxxxxxxxx.cloudfront.net/kitty.jpg?size=<SIZE>&to_webp=<1|0>
```
    
Where:
- `size` - the size of the resized image. The allowed values are predefine and hardcoded for the sake of simplicity. You can change them in the `lambda_function.py` file. The allowed values are:
  - `XS`, `S`, `M`, `L` for 'long' and 'tall' images
  - `SXS`, `SS`, `SM`, `SL` for 'square' images 

- `to_webp` - if set to `1`, the image will be converted to WebP format. Otherwise, the image format will be the same as the original image.
