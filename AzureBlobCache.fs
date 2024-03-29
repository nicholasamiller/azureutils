﻿namespace AzureUtils
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
        
        // if etag not in memory cache, then need to get form Azure Storage anyway

        member this.GetEtag(blobName : string)  =
            task {
                return!
                    async {
                        let! currentCacheEntry = this.RefreshCacheIfStale(blobName)
                        match currentCacheEntry with 
                        | None -> return Nullable()
                        | Some(v) -> return Nullable(v.ETag)
                    }
            }

        member private this.RefreshCacheIfStale(blobName : string)  =
                async {
                    try
                        let blobClient = blobContainerClient.GetBlobClient(blobName)
                        match getFromMemoryCache blobName with
                        | None ->
                            logger.LogTrace("Cache empty.")
                            let! blobExists = blobClient.ExistsAsync() |> Async.AwaitTask
                            match blobExists.Value with
                            | true ->
                                let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                                putInMemoryCache (blobName, {Contents = BinaryData.FromStream(azureResponse.Value.Content); ETag = azureResponse.Value.Details.ETag; CurrencyLastChecked = DateTimeOffset.UtcNow}) |> ignore
                                return getFromMemoryCache blobName
                            | false -> return None
                        | Some(v) -> 
                            let! etagInBlobStorage = this.GetEtag(blobClient)  // gets property data from Azure Storage only, save bandwith
                            match (etagInBlobStorage <>  v.ETag) with
                            | true ->
                                logger.LogTrace("Cache stale.")
                                let! blobExists = blobClient.ExistsAsync() |> Async.AwaitTask
                                match blobExists.Value with
                                | true ->
                                    let! azureResponse = blobClient.DownloadAsync() |> Async.AwaitTask
                                    putInMemoryCache (blobName,{Contents = BinaryData.FromStream( azureResponse.Value.Content); ETag = etagInBlobStorage; CurrencyLastChecked = DateTimeOffset.UtcNow}) |> ignore
                                    return getFromMemoryCache blobName
                                | false -> return None
                            | false -> 
                                logger.LogTrace("Cache not stale.")
                                putInMemoryCache (blobName,{ v with ETag = etagInBlobStorage; CurrencyLastChecked = DateTimeOffset.UtcNow }) |> ignore
                                return getFromMemoryCache blobName
                    with
                        | ex -> return! raise (new AzureBlobCacheException("Error refreshing cache from Azure Blob Storage.",ex))
                } 
                        
        
        
        member this.GetBlob(blobName : string) : Task<BinaryData> =
            task {
                return!
                    async {
                        match getFromMemoryCache blobName with
                        | Some(v) -> 
                           let timeSinceCurrencyLastChecked = DateTimeOffset.UtcNow - v.CurrencyLastChecked
                    
                           match (timeSinceCurrencyLastChecked > cacheRefreshInterval) with
                           | true ->
                                logger.LogTrace("Due to check cache currency.")
                                let! currentCache = this.RefreshCacheIfStale(blobName)
                                match currentCache with
                                | Some(v) -> return v.Contents
                                | None -> return null
                           | false ->  
                                logger.LogTrace("Not yet due to check cache currency.")
                                return v.Contents
                        | None ->
                           let! freshCache = this.RefreshCacheIfStale(blobName)
                           match freshCache with
                           | Some(v) -> return v.Contents
                           | None -> return null
                    }           
            }
         
        member this.WriteBlobAsync(blobName: string, binaryData: BinaryData) = 
            let defaultHeaders =  (new BlobHttpHeaders(ContentEncoding = "utf-8",ContentType = "application/json"));
            this.WriteBlobAsync(blobName,binaryData, defaultHeaders);
            

        member this.WriteBlobAsync(blobName: string, binaryData: BinaryData, headers: BlobHttpHeaders) =
            task {
                let! writeResult =
                    async {
                        let blobClient = blobContainerClient.GetBlobClient(blobName)
                        // create container if does not exist
                        let options = new BlobUploadOptions(HttpHeaders = headers )
                        try
                            blobContainerClient.CreateIfNotExistsAsync() |> Async.AwaitTask  |> ignore
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

        member this.WriteBlobAsync(blobName : string, blob : obj) =
            task {
                let binaryData = new BinaryData(blob)
                return this.WriteBlobAsync(blobName, binaryData)
            }        
        

        member private this.GetEtag(blobClient : BlobClient) =
            async { 
                 let! metaDataResponse = blobClient.GetPropertiesAsync() |> Async.AwaitTask 
                 return metaDataResponse.Value.ETag
             }
