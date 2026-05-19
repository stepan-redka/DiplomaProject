using Diploma.Infrastructure.ML;
using Diploma.Application.DTOs;
using Microsoft.ML;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var jsonPath = Path.Combine(root, "Diploma.Web", "Data", "clinc150", "data", "data_full.json");
var outputPath = Path.Combine(root, "Diploma.Web", "Data", "intent_model.zip");

if (args.Length > 0 && args[0] == "inspect")
{
    InspectModel(outputPath);
}
else
{
    TrainModel(jsonPath, outputPath);
}

void TrainModel(string json, string output)
{
    Console.WriteLine("--- Manual Model Training Utility ---");
    if (!File.Exists(json))
    {
        Console.WriteLine($"Error: Dataset not found at {json}");
        return;
    }

    try
    {
        ModelTrainer.TrainAndSaveModel(json, output);
        Console.WriteLine("--- Success: Model generated successfully ---");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--- Critical Failure: {ex.Message} ---");
    }
}

void InspectModel(string modelPath)
{
    Console.WriteLine("--- ML.NET Model Inspector ---");
    if (!File.Exists(modelPath))
    {
        Console.WriteLine($"Error: Model not found at {modelPath}. Run training first.");
        return;
    }

    try
    {
        var mlContext = new MLContext(seed: 42);
        ITransformer trainedModel = mlContext.Model.Load(modelPath, out var modelSchema);
        var predictionEngine = mlContext.Model.CreatePredictionEngine<IntentData, IntentPrediction>(trainedModel);

        Console.WriteLine("Model loaded successfully. Type a query to test intent (or 'exit' to quit):");

        while (true)
        {
            Console.Write("\nQuery > ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

            var prediction = predictionEngine.Predict(new IntentData { Text = input });

            Console.WriteLine($"Result: [Intent: {prediction.PredictedLabel}]");

            // Note: In Multi-class SDCA, Score is the raw logits or probabilities depending on the output layer.
            // For SDCA Maximum Entropy, it's usually probabilities.
            if (prediction.Score != null && prediction.Score.Length > 0)
            {
                var maxScore = prediction.Score.Max();
                Console.WriteLine($"Confidence: {maxScore:P2}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--- Inspector Error: {ex.Message} ---");
    }
}
