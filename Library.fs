namespace AzureUtils
open Azure.Storage.Blobs
open Azure.Storage.Blobs.Models
open Azure


module BlobUtils =
    
    let createBlobClient(connectionString : string, containerName : string, blobName : string) =
         let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
         let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
         let blobClient = blobContainerClient.GetBlobClient(blobName)
         blobClient
    
    let createBlockBlobClient(connectionString : string, containerName : string, blobName : string) =
         new Azure.Storage.Blobs.Specialized.BlockBlobClient(connectionString,containerName,blobName)
         
    
    let getBlobAsync(connectionString: string, containerName: string, blobName: string) = 
        task {
            let blobClient = createBlobClient(connectionString, containerName, blobName)
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
    
    let getLastModified(connectionString : string, containerName: string, blobName : string) =
        task {
            let blobClient = createBlobClient(connectionString, containerName, blobName)
            let! lastModified =
                async {
                    try
                        let! metaDataResponse = blobClient.GetPropertiesAsync() |> Async.AwaitTask 
                        return Ok(metaDataResponse.Value.LastModified)
                    with
                        | :? RequestFailedException -> return Error "Could not get blob metadata"
                
                }

            return lastModified
        }

    let writeJsonBlobAsync(connectionString : string, containerName: string, blobName : string, json : string ) =
        task {
            let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
            let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
            let blobClient = blobContainerClient.GetBlobClient(blobName)
            let! writeResult =
                async {
                    // create container if does not exist
                    
                    let options = new BlobUploadOptions(HttpHeaders = new BlobHttpHeaders(ContentEncoding = "utf-8",ContentType = "application/json") )
                    let stringAsBytes = System.Text.Encoding.UTF8.GetBytes(json)
                    use ms = new System.IO.MemoryStream(stringAsBytes)
                    
                    try
                        let! createIfNotExistsResponse = blobContainerClient.CreateIfNotExistsAsync() |> Async.AwaitTask 
                        
                        let! azureResponse = blobClient.UploadAsync(ms,options) |> Async.AwaitTask
                        return Ok()
                    with
                        | ex -> return Error ex
                }
            return writeResult
         }

    

        


