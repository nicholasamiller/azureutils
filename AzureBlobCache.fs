namespace AzureUtils
open Azure
open Azure.Storage.Blobs
open Azure.Storage.Blobs.Models
open System.IO
open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Caching.Memory
open Azure.Core
open System.Text.Json
   
    type private CacheEntry =
            {
                Contents : BinaryData;
                ETag: ETag;
                CurrencyLastChecked: DateTimeOffset;
            }
    
    type AzureBlobCache(connectionString: string, containerName: string, cacheRefreshInterval : TimeSpan, memoryCache: IMemoryCache, logFactory : ILoggerFactory) =
        
        let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
        let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
        let logger = logFactory.CreateLogger(typeof<AzureBlobCache>)
         
        let readAsString (s : Stream)  = 
            use sr = new StreamReader(s)
            sr.ReadToEnd()
        let putInMemoryCache (blobName: string, entry : CacheEntry) = 
            memoryCache.Set(blobName,entry)

        let getFromMemoryCache(blobName : string) : Option<CacheEntry> = 
            match memoryCache.Get(blobName) with
            | null -> None
            | v -> Some(v :?> CacheEntry)

        member private this.RefreshCacheIfStale(blobName : string)  =
                async {
                    try
                        let blobClient = blobContainerClient.GetBlobClient(blobName)
                        match getFromMemoryCache blobName with
                        | None ->
                            logger.LogInformation("Cache empty.")
                            let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                            putInMemoryCache (blobName, {Contents = BinaryData.FromStream(azureResponse.Value.Content); ETag = azureResponse.Value.Details.ETag; CurrencyLastChecked = DateTimeOffset.UtcNow}) |> ignore
                            return getFromMemoryCache blobName
                        | Some(v) -> 
                            let! etagInBlobStorage = this.GetEtag(blobClient)  // gets property data from Azure Storage only, save bandwith
                            match (etagInBlobStorage <>  v.ETag) with
                            | true ->
                                logger.LogInformation("Cache stale.")
                                let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                                putInMemoryCache (blobName,{Contents = BinaryData.FromStream( azureResponse.Value.Content); ETag = etagInBlobStorage; CurrencyLastChecked = DateTimeOffset.UtcNow}) |> ignore
                                return getFromMemoryCache blobName
                            | false -> 
                                logger.LogInformation("Cache not stale.")
                                putInMemoryCache (blobName,{ v with ETag = etagInBlobStorage; CurrencyLastChecked = DateTimeOffset.UtcNow }) |> ignore
                                return getFromMemoryCache blobName
                    with
                        | ex -> return! raise (new AzureBlobCacheException("Error refreshing cache from Azure Blob Storage.",ex))
                } 
                        
        
        member this.GetJsonBlob(blobName : string) =
            task {
                return!
                    async {
                        match getFromMemoryCache blobName with
                        | Some(v) -> 
                           let timeSinceCurrencyLastChecked = DateTimeOffset.UtcNow - v.CurrencyLastChecked
                    
                           match (timeSinceCurrencyLastChecked > cacheRefreshInterval) with
                           | true ->
                                logger.LogInformation("Due to check cache currency.")
                                let! currentCache = this.RefreshCacheIfStale(blobName)
                                return currentCache.Value.Contents
                           | false ->  
                                logger.LogInformation("Not yet due to check cache currency.")
                                return v.Contents
                        | None ->
                            let! freshCache = this.RefreshCacheIfStale(blobName)
                            return freshCache.Value.Contents
                    }           
            }
         

        member this.WriteBlobAsync(blobName : string, blob : obj) =
            task {
                let binaryData = new BinaryData(blob)
                let! writeResult =
                    async {
                        let blobClient = blobContainerClient.GetBlobClient(blobName)
                        // create container if does not exist
                        let options = new BlobUploadOptions(HttpHeaders = new BlobHttpHeaders(ContentEncoding = "utf-8",ContentType = "application/json") )
                        try
                            let! createIfNotExistsResponse = blobContainerClient.CreateIfNotExistsAsync() |> Async.AwaitTask 
                            let! azureResponse = blobClient.UploadAsync(binaryData,options) |> Async.AwaitTask
                            return Ok(azureResponse)
                        with
                            | ex -> return Error ex
                    }
                match writeResult with
                | Ok(r) -> 
                    putInMemoryCache (blobName,{Contents = binaryData; ETag = r.Value.ETag; CurrencyLastChecked = DateTimeOffset.UtcNow }) |> ignore
                    return Task.CompletedTask
                | Error(ex) -> return! raise (new AzureBlobCacheException("Error refreshing cache from Azure Blob Storage.",ex))
        }
        

        member private this.GetEtag(blobClient : BlobClient) =
            async { 
                 let! metaDataResponse = blobClient.GetPropertiesAsync() |> Async.AwaitTask 
                 return metaDataResponse.Value.ETag
             }
