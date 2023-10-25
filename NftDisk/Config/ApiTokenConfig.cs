using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liuguang.NftDisk.Config;

public class ApiTokenConfig
{
    public const string DEFAULT_PATH = "./data/nft_token.json";
    
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
    public static ApiTokenConfig Instance = new();

    public static async Task LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        using var fileStream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<ApiTokenConfig>(fileStream);
        if (config is not null)
        {
            Instance.Token = config.Token;
        }
    }
    public static async Task SaveAsync(string path)
    {
        using var fileStream = File.Create(path);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await JsonSerializer.SerializeAsync(fileStream, Instance, options);
    }
}