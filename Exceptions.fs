namespace AzureUtils
open System

type AzureBlobCacheException(message:string, innerException:Exception) =
    inherit Exception(message, innerException)


