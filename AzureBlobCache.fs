
namespace AzureUtils
open Azure
open Azure.Storage.Blobs
open Azure.Storage.Blobs.Models


    type AzureBlobCache(connectionString: string, containerName: string, blobName : string) =
        
        let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
        let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
        let blobClient = blobContainerClient.GetBlobClient(blobName)

        member this.GetBlobAsync() =
             task {
                let! blobResultWithMatadata  =
                    async {
                        try
                            let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                            return Ok(azureResponse.Value)
                        with
                            | ex -> return Error ex
                     } 
                            
                return blobResultWithMatadata
            }
        
        member this.WriteJsonBlobAsync(json : string) =
            task {
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
        


        member this.GetLastModifiedDate() =
             task {
                let! result = 

                     async {
                        try
                            let! metaDataResponse = blobClient.GetPropertiesAsync() |> Async.AwaitTask 
                            return Ok(metaDataResponse.Value.LastModified)
                        with
                            | :? RequestFailedException -> return Error "Could not get blob metadata"
                     }
                return result
             }
    
        


