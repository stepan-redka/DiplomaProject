using System.Text.Json;
using System.Text;
using Diploma.Application.DTOs;

namespace Diploma.Infrastructure.ML;

/// <summary>
/// Handles dataset preparation and conversion for the ML.NET pipeline.
/// </summary>
public static class DatasetManager
{
    public static List<IntentData> LoadAndMapClincData(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("CLINC150 dataset not found", jsonPath);

        var data = new List<IntentData>();
        try
        {
            var jsonString = File.ReadAllText(jsonPath);
            using var document = JsonDocument.Parse(jsonString);
            var root = document.RootElement;

            // Combine "train" and "oos_train" keys
            if (root.TryGetProperty("train", out var trainArray))
                ProcessJsonArray(trainArray, data);

            if (root.TryGetProperty("oos_train", out var oosArray))
                ProcessJsonArray(oosArray, data);

            Console.WriteLine($"[ML.NET] Loaded {data.Count} samples from CLINC150 dataset.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ML.NET] Failed to load dataset: {ex.Message}");
            throw;
        }

        return data;
    }

    private static void ProcessJsonArray(JsonElement array, List<IntentData> dataList)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.GetArrayLength() >= 2)
            {
                string text = item[0].GetString() ?? "";
                string rawLabel = item[1].GetString() ?? "";

                // Semantic Binary Mapping
                string mappedLabel = MapToSystemIntent(rawLabel);

                dataList.Add(new IntentData
                {
                    Text = text,
                    Label = mappedLabel
                });
            }
        }
    }

    private static string MapToSystemIntent(string rawLabel)
    {
        // Define "GENERAL" intents based on CLINC150 exact intent names
        // Note: The system should strictly prioritize smalltalk/meta categories here.
        var generalIntents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "greeting",     // CLINC150 uses singular 'greeting'
            "thank_you",    // CLINC150 uses 'thank_you'
            "goodbye",      // CLINC150 uses 'goodbye'
            "oos"           // Out-of-scope (casual or unrelated)
        };

        // If it's in the strict general list, mark as GENERAL
        if (generalIntents.Contains(rawLabel))
            return "GENERAL";

        // Everything else (banking, travel, work, utility, factual QA, meta questions) maps to RESEARCH
        // to ensure the RAG system handles any operational query.
        return "RESEARCH";
    }

    public static void EnsureCsvDataset(string jsonPath, string csvPath)
    {
        if (File.Exists(csvPath)) return;
        if (!File.Exists(jsonPath)) return;

        Console.WriteLine($"[ML.NET] CSV dataset not found. Converting {jsonPath} to CSV...");

        try
        {
            var jsonString = File.ReadAllText(jsonPath);
            using var document = JsonDocument.Parse(jsonString);
            var root = document.RootElement;

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Text,Label");

            // CLINC150 JSON structure has keys for "train", "val", "test", "oos_train", etc.
            // Each contains an array of arrays: [["text", "intent"], ...]
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.GetArrayLength() >= 2)
                        {
                            string text = item[0].GetString() ?? "";
                            string label = item[1].GetString() ?? "";

                            // Basic CSV escaping: quote everything and escape quotes
                            string escapedText = text.Replace("\"", "\"\"");
                            csvBuilder.AppendLine($"\"{escapedText}\",\"{label}\"");
                        }
                    }
                }
            }

            File.WriteAllText(csvPath, csvBuilder.ToString());
            Console.WriteLine($"[ML.NET] Successfully converted {jsonPath} to {csvPath} ({csvBuilder.Length} bytes).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ML.NET] Dataset conversion failed: {ex.Message}");
            throw;
        }
    }
}
