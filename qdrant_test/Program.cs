using System;
using System.Threading.Tasks;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Text.Json;

class Program {
    static async Task Main() {
        var client = new QdrantClient("localhost", 6334);
        var res = await client.SearchAsync("documents", new float[768], limit: 1);
        foreach (var r in res) {
            foreach (var kvp in r.Payload) {
                Console.WriteLine($"Payload[{kvp.Key}]: Kind={kvp.Value.KindCase}, StringValue={kvp.Value.StringValue}");
            }
        }
    }
}
