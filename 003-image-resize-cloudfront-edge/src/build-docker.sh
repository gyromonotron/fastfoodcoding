#!/bin/bash

docker build -t lambda-edge:latest . && \
docker create --name lambda-edge-container lambda-edge:latest && \
docker container cp lambda-edge-container:/asset . && \
docker container rm lambda-edge-container && \
docker image rm lambda-edge
