namespace AzureUtils.Tests
open AzureUtils

open System
open System.IO
open Azure.Storage.Blobs
open System.Threading
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Xunit
open Microsoft.Extensions.Caching.Memory
open Microsoft.Extensions.Options


module Tests =
    
    //Make sure local storage emulator or Azurite is running with -skipApiVersionCheck flag in correct working folder.

    let connectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
    let containerName = "test"
    
    type Cat =
        {
            Name: string;
            Age: int
        }

        

    let testCat1 = {Name = "Tabby"; Age = 10}  
    let testCat2 =  {Name = "Milly"; Age = 9}  
    
    let testBlobName = "test.json"
    let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
    let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
    let blobClient = blobContainerClient.GetBlobClient(testBlobName)
    let loggerFactory = LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore)
    let memoryCache = new MemoryCache(new MemoryCacheOptions())
        
    let readAsString (s : Stream)  = 
        use sr = new StreamReader(s)
        sr.ReadToEnd()
    
                 

    
    let toStream (text: string) =
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text))

        
        
    [<Fact>]
    let ``upload``  () =
        let underTest =  new AzureUtils.AzureBlobCache(connectionString,containerName, TimeSpan.FromHours(1), memoryCache, loggerFactory )
        blobClient.DeleteIfExists() |> ignore // setup
        underTest.WriteBlobAsync(testBlobName, testCat1).Result |> ignore
        let blobClient = blobContainerClient.GetBlobClient(testBlobName)
        let retrievedContent = blobClient.Download().Value.Content
        let bd = new BinaryData(readAsString retrievedContent)
        let receivedObj = bd.ToObjectFromJson<Cat>()
        Assert.Equal(testCat1,receivedObj)
        blobClient.DeleteIfExists() |> ignore // tear down

    [<Fact>]
    let ``cache stale`` () =
        blobClient.DeleteIfExists() |> ignore 
        blobClient.Upload(new BinaryData(testCat1)) |> ignore
        let underTest = new AzureUtils.AzureBlobCache(connectionString,containerName,TimeSpan.FromSeconds(10), memoryCache, loggerFactory)
        let r1 = underTest.GetBlob(testBlobName).Result.ToObjectFromJson<Cat>()
        blobClient.DeleteIfExists() |> ignore 
        let r2 = underTest.GetBlob(testBlobName).Result.ToObjectFromJson<Cat>()
        // should not have updated, cache should be stale
        Assert.Equal(r1,r2)
    
    [<Fact>]
    let ``cache not stale`` () =
        blobClient.DeleteIfExists() |> ignore 
        blobClient.Upload(new BinaryData(testCat1)) |> ignore
        let underTest = new AzureUtils.AzureBlobCache(connectionString,containerName, TimeSpan.FromMilliseconds(10), memoryCache, loggerFactory)
        let r1 = underTest.GetBlob(testBlobName).Result.ToObjectFromJson<Cat>()
        blobClient.DeleteIfExists() |> ignore 
        blobClient.Upload(new BinaryData(testCat2)) |> ignore
        Thread.Sleep(11)
        let r2 = underTest.GetBlob(testBlobName).Result.ToObjectFromJson<Cat>()
        // should have updated
        Assert.NotEqual(r1,r2)
        

    [<Fact>]
    let ``get blob`` () =

        blobClient.DeleteIfExists() |> ignore 
        let underTest = new AzureUtils.AzureBlobCache(connectionString,containerName, TimeSpan.FromHours(1), memoryCache, loggerFactory)
        blobClient.Upload(new BinaryData(testCat1)) |> ignore
        let r = underTest.GetBlob(testBlobName).Result
        let expectedData = new BinaryData(testCat1)
        let cat = expectedData.ToObjectFromJson<Cat>()
        Assert.Equal(cat,testCat1)
    
    [<Fact>]
    let ``getting blob that does not exist should return null`` () =
        
        let underTest = new AzureUtils.AzureBlobCache(connectionString,containerName, TimeSpan.FromHours(1), memoryCache, loggerFactory)
        let result = underTest.GetBlob("doesNotExist.json").Result
        Assert.Null(result)

    [<Fact>]
    let ``different blobs`` () = 
        
        let underTest = new AzureUtils.AzureBlobCache(connectionString,containerName, TimeSpan.FromHours(1), memoryCache, loggerFactory)
        underTest.WriteBlobAsync("cat1",testCat1).Result |> ignore
        underTest.WriteBlobAsync("cat2",testCat2).Result |> ignore
        let c1 = underTest.GetBlob("cat1").Result.ToObjectFromJson<Cat>()
        let c2 = underTest.GetBlob("cat2").Result.ToObjectFromJson<Cat>()
        Assert.Equal(testCat1,c1)
        Assert.Equal(testCat2,c2)

