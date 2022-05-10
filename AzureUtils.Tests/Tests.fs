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

    [<TestMethodAttribute>]
    member this.TestUpload () =
        let testJson = """ {"test": "value"} """
        let r = BlobUtils.writeJsonBlobAsync(connectionString,containerName,blobName, testJson).Result
        match r with
        | Ok() -> Assert.IsNotNull r
        | _ -> Assert.Fail()

    [<TestMethod>]
    member this.TestBlobRetrieval () =
        
        let readAsString (s : Stream)  = 
            use sr = new StreamReader(s)
            sr.ReadToEnd()

        try 
            let r = BlobUtils.getBlobAsync(connectionString,containerName, blobName).Result
            match r with 
            | Ok(c) -> printfn "%s" (readAsString c.Content)
            | _ -> Assert.Fail()
            
        with
            | ex -> Assert.Fail("Make sure local storage emulator or Azurite is running with -skipApiVersionCheck flag in correct working folder.")

        
    [<TestMethod>]
    member this.TestLastModified () =
        try
            let r = BlobUtils.getLastModified(connectionString, containerName, blobName).Result
            match r with
            | Ok(r) -> printfn "%A" r
            | _ -> Assert.Fail()
        with
            | ex -> Assert.Fail()

    
   
