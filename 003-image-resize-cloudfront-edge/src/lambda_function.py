import mimetypes
import os
import boto3
from io import BytesIO
from PIL import Image

ALLOWED_RESIZE_EXTENSIONS = ["jpg", "jpeg", "png", "gif", "bmp", "webp"]
SIZE_MAP = {
    'L': {'WIDE': (1280, 720), 'TALL': (720, 1280)},
    'M': {'WIDE': (1200, 628), 'TALL': (628, 1200)},
    'S': {'WIDE': (854, 480), 'TALL': (480, 854)},
    'XS': {'WIDE': (427, 240), 'TALL': (240, 427)},
}
SQUARE_SIZE_MAP = {'SL': (1080, 1080), 'SM': (540, 540), 'SS': (360, 360), 'SXS': (180, 180)}
SIZE_PARAMETER_NAME = "size="
SIZE_MAP.update(SQUARE_SIZE_MAP)
ALLOWED_RESIZE_VALUES = [SIZE_PARAMETER_NAME + size for size in {**SIZE_MAP, **SQUARE_SIZE_MAP}]
CONVERT_TO_WEBP = "to_webp=1"

s3 = boto3.client('s3')

def lambda_handler(event, context):
    request = event['Records'][0]['cf']['request']
    request_uri: str = request['uri'].lstrip('/')
    request_uri_no_extension: str = os.path.splitext(request_uri)[0]
    request_uri_extension: str = os.path.splitext(request_uri)[1].lstrip('.').lower()
    request_querystring: str = request['querystring']

    ## since environment variables are not supported in Lambda@Edge and we don't want to hardcode values
    ## we are passing them as custom headers in the S3 origin configuration
    ## alternatively, you can use AWS Systems Manager Parameter Store to store these values
    custom_headers = request['origin']['s3']['customHeaders']   
    env_resized_path = custom_headers['x-env-resized-path'][0]['value']
    env_bucket_name = custom_headers['x-env-bucket-name'][0]['value']
    env_quality = int(custom_headers['x-env-quality'][0]['value'])

    print(f"Resized path: {env_resized_path}, Bucket name: {env_bucket_name}, Quality: {env_quality}")

    if request_uri_extension not in ALLOWED_RESIZE_EXTENSIONS:
        print(f"Not allowed extension: {request_uri_extension}. Skipping...")
        return request

    query_params: list[str] = request_querystring.split('&')
    if not any(query in query_params for query in ALLOWED_RESIZE_VALUES):
         print("No allowed size parameter found. Skipping...")
         return request

    size_parameter: str = next((query for query in query_params if query.startswith(SIZE_PARAMETER_NAME)), "").replace(SIZE_PARAMETER_NAME, "")
    convert_to_webp: bool = any(query in query_params for query in [CONVERT_TO_WEBP])
    dest_file_extension = "webp" if convert_to_webp else request_uri_extension
    dest_file_path: str = f"{env_resized_path}/{size_parameter}/{request_uri_no_extension}.{dest_file_extension}"
    dest_image_exists: bool = is_s3_obj_exists(env_bucket_name, dest_file_path)

    if dest_image_exists:
        request['uri'] = '/' + dest_file_path
        print(f"Resized file already exists: {dest_file_path}. Returning...")
        return request

    source_image_exists: bool = is_s3_obj_exists(env_bucket_name, request_uri)
    if not source_image_exists:
        print(f"Source image does not exist: {request_uri}. Skipping...")
        return request

    # download original image from S3 and resize
    source_image_obj = s3.get_object(Bucket=env_bucket_name, Key=request_uri)['Body'].read()
    image: Image.Image = Image.open(BytesIO(source_image_obj))

    # resize image and save it to in-memory file
    image.thumbnail(get_size(image, size_parameter))
    in_mem_file = BytesIO()
    image.save(in_mem_file, format="webp" if convert_to_webp else image.format, quality=env_quality)
    in_mem_file.seek(0)

    # upload resized image to S3
    s3.upload_fileobj(
        in_mem_file,
        env_bucket_name,
        dest_file_path,
        ExtraArgs={
            # set proper content-type instead of default 'binary/octet-stream'
            'ContentType': mimetypes.guess_type(dest_file_path)[0] or f'image/{dest_file_extension}'
        }
    )

    # change request uri to the resized file path and return it to CloudFront
    request['uri'] = '/' + dest_file_path
    return request

def get_size(image, size_parameter):
    if size_parameter in SQUARE_SIZE_MAP:
        return SQUARE_SIZE_MAP[size_parameter]
    elif image.width > image.height:
        return SIZE_MAP[size_parameter]['WIDE']
    else:
        return SIZE_MAP[size_parameter]['TALL']

def is_s3_obj_exists(bucket, key: str) -> bool:
    objs = s3.list_objects_v2(Bucket=bucket, Prefix=key).get('Contents', [])
    return any(obj['Key'] == key for obj in objs)
