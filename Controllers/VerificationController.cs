using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ProofEasySDK;
using ProofEasySDK.Models;

[Route("api/[controller]")]
[ApiController]
public class VerificationController : ControllerBase
{
    private readonly string apiKey = "1F880173ECD0515EC7030344A40C4619864EB98F8865699EAA223F3B11C99F5E";
    private readonly string secretKey = "RUe91ep53AhCVmm6w1BF1a/aFRVGA4OFb3PPrp3MdQpBO6yr6uxe8AmmJiCNIr8xEmU=";

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyDocument([FromForm] IFormFile pdfFile)
    {
        try
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var sdk = new ProofEasyPROC(apiKey, secretKey);

            // Generate and upload QR code
            var generateAndUploadQrCode = await sdk.GenerateUniqueIDAsync();
            var uniqueId = generateAndUploadQrCode.uniqueId;
            var qrImagePath = generateAndUploadQrCode.qrImagePath;

            //For Desktop - // Save the uploaded PDF file to a temporary location
            /*var tempFilePath = Path.GetTempFileName();
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }

            // Compute document hash
            var originalDocumentHash = sdk.ComputeHash(tempFilePath);*/
            string originalDocumentHash;
            using (var streamData = new MemoryStream())
            {
                await pdfFile.CopyToAsync(streamData);
                streamData.Position = 0; // Reset the stream position
                originalDocumentHash = sdk.ComputeHash(streamData);
            }

            // Submit document
            var documentSubmitRequest = new DocumentSubmitRequest
            {
                uniqueId = uniqueId,
                fileurl = qrImagePath,
                isfileurlpublic = "1",
                metadata = "Name: Name1 || Title: Title1 || Email: email1@test.com",
                parent_delimiter = "||",
                child_delimiter = ":",
                Ispublic = "1",
                authorizedusers = "",
                Redirecturl = "",
                isredirecturlprivate = "0",
                tokenactiveduration = "120",
                sendmetadatatoblockchain = "true",
                metadataforblockchain = originalDocumentHash,
                isparent = "1",
                parentid = ""
            };

            var documentSubmit = await sdk.SubmitDocumentAsync(documentSubmitRequest);

            // Introduce a delay of 5 seconds
            await Task.Delay(5000);

            // Get blockchain status
            var blockchainStatus = await sdk.GetBlockchainStatusAsync(uniqueId);

            // Verify document hash
            string valueDocumentBlockchain = "";
            if (blockchainStatus != null && blockchainStatus.blockchaindata != null)
            {
                string[] parts = blockchainStatus.blockchaindata.Split(new string[] { "||" }, StringSplitOptions.None);
                if (parts.Length >= 2)
                {
                    valueDocumentBlockchain = parts[1];
                }
            }
            var isVerified = sdk.VerifyDocumentHash(valueDocumentBlockchain, originalDocumentHash);

            var result = new
            {
                DocumentSubmit = documentSubmit,
                BlockchainStatus = blockchainStatus,
                IsVerified = isVerified
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"An error occurred: {ex.Message}");
            //Trace.WriteLine(ex.StackTrace);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
