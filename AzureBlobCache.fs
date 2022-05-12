
namespace AzureUtils
open Azure
open Azure.Storage.Blobs
open Azure.Storage.Blobs.Models
open System.IO
open System
open System.Threading.Tasks
    

    type AzureJsonBlobCache(connectionString: string, containerName: string, blobName : string, cacheRefreshInterval : TimeSpan) =
        
        let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
        let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
        let blobClient = blobContainerClient.GetBlobClient(blobName)
        let mutable blobJsonCache = None
        let mutable lastCacheRefresh = DateTimeOffset.UtcNow
        
        let readAsString (s : Stream)  = 
            use sr = new StreamReader(s)
            sr.ReadToEnd()
        
        member private this.RefreshCacheIfStale()  =
                async {
                    try
                        match blobJsonCache with
                        | Some(v) -> 
                            let! lastModifiedDate = this.GetLastModifiedDate()  // gets property data from Azure Storage only, save bandwith
                            match (lastModifiedDate > lastCacheRefresh) with
                            | true ->
                                let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                                blobJsonCache <- Some(readAsString azureResponse.Value.Content)
                                lastCacheRefresh <- DateTimeOffset.UtcNow
                            | false -> 
                                lastCacheRefresh <- DateTimeOffset.UtcNow 
                        | None -> 
                             let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                             blobJsonCache <- Some(readAsString azureResponse.Value.Content)
                             lastCacheRefresh <- DateTimeOffset.UtcNow
                       
                    with
                        | ex -> return! raise (new AzureBlobCacheException("Error refreshing cache from Azure Blob Storage.",ex))
                } 
                        
        
        member this.GetBlobJsonContentAsString() =
            task {
                let timeSpanSinceLastCacheCheck = DateTimeOffset.UtcNow - lastCacheRefresh
                return!
                    async {
                        match (timeSpanSinceLastCacheCheck > cacheRefreshInterval) with
                        | true ->
                            let! r = this.RefreshCacheIfStale()
                            return blobJsonCache.Value
                        | false -> return blobJsonCache.Value
                    }
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
                match writeResult with
                | Ok() -> return Task.CompletedTask
                | Error(ex) -> return! raise (new AzureBlobCacheException("Error refreshing cache from Azure Blob Storage.",ex))
        }
        

        member private this.GetLastModifiedDate() =
            async { 
                 let! metaDataResponse = blobClient.GetPropertiesAsync() |> Async.AwaitTask 
                 return metaDataResponse.Value.LastModified
             }
                 
    
        


