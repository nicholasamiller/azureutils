& docker run --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite azurite --skipApiVersionCheck --loose -l /workspace -d /workspace/debug.log --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0