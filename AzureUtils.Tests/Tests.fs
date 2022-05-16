namespace AzureUtils.Tests
open AzureUtils

open System
open System.IO
open Azure.Storage.Blobs
open System.Threading
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Xunit


module Tests =
    
    //Make sure local storage emulator or Azurite is running with -skipApiVersionCheck flag in correct working folder.

    let connectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
    let containerName = "test"
    let blobName = "test.json"

    let testJson1 = """ {"test": "value"} """
    let testJson2 = """ {"test2": "value2"} """
    
    let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
    let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
    let blobClient = blobContainerClient.GetBlobClient(blobName)
    let loggerFactory = LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore)


    
    let readAsString (s : Stream)  = 
        use sr = new StreamReader(s)
        sr.ReadToEnd()
    
    let toStream (text: string) =
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text))

        
        
        
    [<Fact>]
    let ``upload``  () =
        let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, TimeSpan.FromHours(1),loggerFactory )
        blobClient.DeleteIfExists() |> ignore // setup
        underTest.WriteJsonBlobAsync(testJson1).Result |> ignore
        let retrievedContent = blobClient.Download().Value.Content
        Assert.Equal(testJson1,readAsString retrievedContent)
        blobClient.DeleteIfExists() |> ignore // tear down

    [<Fact>]
    let ``cache stale`` () =
        blobClient.DeleteIfExists() |> ignore 
        blobClient.Upload(toStream testJson1) |> ignore
        let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, TimeSpan.FromSeconds(10), loggerFactory)
        let r1 = underTest.GetBlobJsonContentAsString().Result
        blobClient.DeleteIfExists() |> ignore 
        let r2 = underTest.GetBlobJsonContentAsString().Result
        // should not have updated, cache should be stale
        Assert.Equal(r1,r2)
    
    [<Fact>]
    let ``cache not stale`` () =
        blobClient.DeleteIfExists() |> ignore 
        blobClient.Upload(toStream testJson1) |> ignore
        let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, TimeSpan.FromMilliseconds(10), loggerFactory)
        let r1 = underTest.GetBlobJsonContentAsString().Result
        blobClient.DeleteIfExists() |> ignore 
        Thread.Sleep(1000)
        blobClient.Upload(toStream testJson2) |> ignore
        Thread.Sleep(1000)
        let r2 = underTest.GetBlobJsonContentAsString().Result
        // should have updated
        Assert.NotEqual<string>(r1,r2)
        

    [<Fact>]
    let ``get blob`` () =
        blobClient.DeleteIfExists() |> ignore 
        let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, TimeSpan.FromHours(1), loggerFactory)
        blobClient.Upload(toStream testJson1) |> ignore
        Thread.Sleep(1000)
        let r = underTest.GetBlobJsonContentAsString().Result
        Assert.Equal(r,testJson1)
            
           
   
    
   
