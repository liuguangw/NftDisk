using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Liuguang.NftDisk.Config;
using Liuguang.Storage;

namespace Liuguang.NftDisk.Common;

public static class DirectoryUploadTool
{
    private static HttpClient CreateHttpClient(string token)
    {
        var client = new HttpClient()
        {
            BaseAddress = new Uri("https://api.nft.storage"),
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    public static async Task UploadDirectoryCarAsync(DirectoryCar directoryCar)
    {
        var apiToken = ApiTokenConfig.Instance.Token;
        var httpClient = CreateHttpClient(apiToken);

        byte[] cidData;
        byte[] carData;
        using (var memoryStream = new MemoryStream())
        {
            cidData = await directoryCar.WriteCarAsync(memoryStream);
            carData = memoryStream.ToArray();
        }
        using var content = new ByteArrayContent(carData);
        content.Headers.ContentType = new("application/car");

        var response = await httpClient.PostAsync("/upload", content);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception("http error, " + response.StatusCode.ToString());
        }
    }
}