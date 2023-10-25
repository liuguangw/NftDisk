using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Liuguang.NftDisk.Config;

public class GatewayConfig
{
    public const string DEFAULT_PATH = "./data/gateway.json";
    
    [JsonPropertyName("address_list")]
    public List<string> AddressList { get; set; } = new();
    public static GatewayConfig Instance = new();

    public static async Task LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        using var fileStream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<GatewayConfig>(fileStream);
        if (config is not null)
        {
            Instance.AddressList = config.AddressList;
        }
    }
    public static async Task SaveAsync(string path)
    {
        using var fileStream = File.Create(path);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await JsonSerializer.SerializeAsync(fileStream, Instance, options);
    }
}