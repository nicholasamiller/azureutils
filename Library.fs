namespace AzureUtils
open Azure.Storage.Blobs
open Azure


module BlobCache =
    let getBlobAsync(connectionString: string, containerName: string, blobName: string) = 
        task {
            let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_06_08))
            let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
            let blobClient = blobContainerClient.GetBlobClient(blobName)
            let! blobResultWithMatadata  =
                async {
                    try
                        let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                        return Ok(azureResponse.Value)
                    with
                        | :? RequestFailedException -> return Error "Could not download blob."
                  } 
                        
            return blobResultWithMatadata
        }
    


