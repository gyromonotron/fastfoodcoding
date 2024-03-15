# Image Upload API

This project is a simple API for uploading images. It is built using .NET8 and it provides a single endpoint for uploading images. The imaages are transferred to a S3 bucket.

## Usage

The API has the following endpoints:

- `POST /upload`: Uploads a new image and returns status code 201 (Created) if the image was successfully uploaded. The request must include a `multipart/form-data` body with a single field named `image` containing the image file.

## Swagger

The API includes a Swagger UI that can be accessed at `/swagger` when running the application.

## Note

This is not a production ready application but a learning project. It is not recommended to use this in a production environment.