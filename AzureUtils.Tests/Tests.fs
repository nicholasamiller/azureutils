namespace AzureUtils.Tests
open AzureUtils

open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open System.IO
open Azure.Storage.Blobs
open System.Threading





[<TestClass>]
type TestClass () =
    
    //Make sure local storage emulator or Azurite is running with -skipApiVersionCheck flag in correct working folder.

    let connectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
    let containerName = "test"
    let blobName = "test.json"

    let testJson1 = """ {"test": "value"} """
    let testJson2 = """ {"test2": "value2"} """
    
    let blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions())
    let blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName)
    let blobClient = blobContainerClient.GetBlobClient(blobName)
    
    let readAsString (s : Stream)  = 
        use sr = new StreamReader(s)
        sr.ReadToEnd()
    
    let toStream (text: string) =
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text))

    [<TestMethod>]
    member this.TestUpload () =
        let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, TimeSpan.FromHours(1) )
        blobClient.DeleteIfExists() |> ignore // setup
        underTest.WriteJsonBlobAsync(testJson1).Result |> ignore
        let retrievedContent = blobClient.Download().Value.Content
        Assert.AreEqual(testJson1,readAsString retrievedContent)
        blobClient.DeleteIfExists() |> ignore // tear down

    [<TestMethod>]
    member this.WhenCacheShouldeBeStaleInterval () =
        blobClient.DeleteIfExists() |> ignore 
        blobClient.Upload(toStream testJson1) |> ignore
        let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, TimeSpan.FromSeconds(10))
        let r1 = underTest.GetBlobJsonContentAsString().Result
        blobClient.DeleteIfExists() |> ignore 
        let r2 = underTest.GetBlobJsonContentAsString().Result
        // should not have updated, cache should be stale
        Assert.AreEqual(r1,r2)
    
    [<TestMethod>]
    member this.WhenCacheShouldHaveRefreshed () =
        blobClient.DeleteIfExists() |> ignore 
        blobClient.Upload(toStream testJson1) |> ignore
        let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, TimeSpan.FromMilliseconds(10))
        let r1 = underTest.GetBlobJsonContentAsString().Result
        blobClient.DeleteIfExists() |> ignore 
        Thread.Sleep(1000)
        blobClient.Upload(toStream testJson2) |> ignore
        Thread.Sleep(1000)
        let r2 = underTest.GetBlobJsonContentAsString().Result
        // should have updated
        Assert.AreNotEqual(r1,r2)
        

    [<TestMethod>]
    member this.TestBlobRetrieval () =
        blobClient.DeleteIfExists() |> ignore 
        let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, TimeSpan.FromHours(1))
        blobClient.Upload(toStream testJson1) |> ignore
        let r = underTest.GetBlobJsonContentAsString().Result
        Assert.AreEqual(r,testJson1)
            
   
       
   
    
   
