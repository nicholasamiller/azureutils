namespace AzureUtils.Tests
open AzureUtils

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type TestClass () =

    [<TestMethod>]
    member this.TestBlobRetrieval () =
        
        let connectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
        let r = BlobCache.getBlobAsync(connectionString,"conditions","conditions.json").Result
        match r with 
        | Ok(c) -> Assert.IsTrue(c.Content.Length > 0)
        | _ -> Assert.Fail()

