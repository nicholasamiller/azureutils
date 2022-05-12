namespace AzureUtils.Tests
open AzureUtils

open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open System.IO


[<TestClass>]
type TestClass () =
    
    let connectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
    let containerName = "test"
    let blobName = "test.json"
    let cacheCheckInterval = TimeSpan.FromSeconds(10)

    let testJson = """ {"test": "value"} """
    let underTest = new AzureUtils.AzureJsonBlobCache(connectionString,containerName,blobName, cacheCheckInterval)

    [<TestMethodAttribute>]
    member this.TestUpload () =
        underTest.WriteJsonBlobAsync(testJson).Result |> ignore

    [<TestMethod>]
    member this.TestBlobRetrieval () =
        
        try 
            let r = underTest.GetBlobJsonContentAsString().Result
            Assert.AreEqual(r,testJson)
            
        with
            | ex -> Assert.Fail("Make sure local storage emulator or Azurite is running with -skipApiVersionCheck flag in correct working folder.")

       
   
    
   
