FROM amazonlinux:2023 AS builder

# Install the required packages
RUN yum -y update && \
    yum -y install \
    python3.11 \
    python3.11-pip \
    zip \
    && yum clean all

FROM builder AS lambda

# Copy the source code
COPY lambda_function.py requirements.txt app/

RUN mkdir -p /asset
WORKDIR /app
RUN python3.11 -m pip install -r requirements.txt -t .
# copy to /asset
RUN cp -r /app/* /asset/
