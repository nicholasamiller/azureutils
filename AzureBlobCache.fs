
namespace AzureUtils
open Azure
open Azure.Storage.Blobs
open Azure.Storage.Blobs.Models
open System.IO
open System
open System.Threading.Tasks
   
    type private Cache =
            {
                Contents : string;
                Refreshed: DateTimeOffset 
            }

    type AzureJsonBlobCache(connectionString: string, containerName: string, blobName : string, cacheRefreshInterval : TimeSpan) =
        
       
        let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
        let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
        let blobClient = blobContainerClient.GetBlobClient(blobName)
        let mutable (cache : Option<Cache>) = None
        
        let readAsString (s : Stream)  = 
            use sr = new StreamReader(s)
            sr.ReadToEnd()
        
        member private this.RefreshCacheIfStale()  =
                async {
                    try
                        match cache with
                        | None -> 
                             let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                             cache <-  Some({Contents = readAsString azureResponse.Value.Content; Refreshed = DateTimeOffset.UtcNow})
                             return cache.Value
                        | Some(v) -> 
                            let! lastModifiedDate = this.GetLastModifiedDate()  // gets property data from Azure Storage only, save bandwith
                            match (lastModifiedDate >= v.Refreshed) with
                            | true ->
                                let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                                cache <-  Some({Contents = readAsString azureResponse.Value.Content; Refreshed = DateTimeOffset.UtcNow})
                                return cache.Value
                            | false -> 
                                cache <- Some({ v with Refreshed = DateTimeOffset.UtcNow})
                                return cache.Value
                    with
                        | ex -> return! raise (new AzureBlobCacheException("Error refreshing cache from Azure Blob Storage.",ex))
                } 
                        
        
        member this.GetBlobJsonContentAsString() =
            task {
                return!
                    async {
                        match cache with
                        | Some(v) -> 
                           let timeSinceRefresh = DateTimeOffset.UtcNow - v.Refreshed
                    
                           match (timeSinceRefresh > cacheRefreshInterval) with
                           | true ->
                                let! currentCache = this.RefreshCacheIfStale()
                                return currentCache.Contents
                           | false ->  
                                return v.Contents
                        | None ->
                            let! freshCache = this.RefreshCacheIfStale()
                            return freshCache.Contents
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
                | Ok() -> 
                    cache <- Some({Contents = json; Refreshed = DateTimeOffset.UtcNow})
                    return Task.CompletedTask
                | Error(ex) -> return! raise (new AzureBlobCacheException("Error refreshing cache from Azure Blob Storage.",ex))
        }
        

        member private this.GetLastModifiedDate() =
            async { 
                 let! metaDataResponse = blobClient.GetPropertiesAsync() |> Async.AwaitTask 
                 return metaDataResponse.Value.LastModified
             }
                 
    
        


