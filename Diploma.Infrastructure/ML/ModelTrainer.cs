using Microsoft.ML;
using Diploma.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.ML;

/// <summary>
/// Offline Model Trainer utility for creating the ML.NET classification model.
/// </summary>
public static class ModelTrainer
{
    /// <summary>
    /// Trains an intent classification model using the CLINC150 dataset and saves it to a zip file.
    /// </summary>
    /// <param name="sourceJsonPath">Path to the CLINC150 JSON dataset.</param>
    /// <param name="modelOutputPath">Path where the trained model .zip will be saved.</param>
    public static void TrainAndSaveModel(string sourceJsonPath, string modelOutputPath)
    {
        Console.WriteLine($"[ML.NET] Starting offline training session...");
        Console.WriteLine($"[ML.NET] Source: {sourceJsonPath}");
        Console.WriteLine($"[ML.NET] Destination: {modelOutputPath}");

        try
        {
            var mlContext = new MLContext(seed: 42);

            // 1. Load Data
            var rawData = DatasetManager.LoadAndMapClincData(sourceJsonPath);
            
            // 2. Class Balancing (Down-sampling RESEARCH to handle majority bias)
            // RESEARCH samples (~15,000) vastly outnumber GENERAL (~400-500)
            var generalData = rawData.Where(d => d.Label == "GENERAL").ToList();
            var researchData = rawData.Where(d => d.Label == "RESEARCH").ToList();
            
            Console.WriteLine($"[ML.NET] Raw distribution: GENERAL={generalData.Count}, RESEARCH={researchData.Count}");
            
            // We take all GENERAL and a proportional amount of RESEARCH (1.5x)
            var random = new Random(42);
            var balancedResearchData = researchData
                .OrderBy(x => random.Next())
                .Take((int)(generalData.Count * 1.5))
                .ToList();
            
            var balancedData = generalData
                .Concat(balancedResearchData)
                .OrderBy(x => random.Next())
                .ToList();
            
            Console.WriteLine($"[ML.NET] Balanced distribution: GENERAL={generalData.Count}, RESEARCH={balancedResearchData.Count}");

            IDataView fullDataView = mlContext.Data.LoadFromEnumerable(balancedData);

            // 3. Train/Test Split (80% Train, 20% Test)
            Console.WriteLine("[ML.NET] Splitting dataset (80/20)...");
            var split = mlContext.Data.TrainTestSplit(fullDataView, testFraction: 0.2, seed: 42);

            // 4. Build Pipeline
            var pipeline = mlContext.Transforms.Text.FeaturizeText(
                    outputColumnName: "Features", 
                    inputColumnName: nameof(IntentData.Text))
                .Append(mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "Label", 
                    inputColumnName: nameof(IntentData.Label)))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "Label", 
                    featureColumnName: "Features"))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue(
                    outputColumnName: "PredictedLabel", 
                    inputColumnName: "PredictedLabel"));

            // 5. Train (Fit) on TrainSet
            Console.WriteLine("[ML.NET] Training model on TrainSet...");
            var model = pipeline.Fit(split.TrainSet);

            // 6. Evaluate on TestSet
            Console.WriteLine("[ML.NET] Evaluating model on TestSet...");
            var predictions = model.Transform(split.TestSet);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "Label");

            // 7. Print Benchmarks
            Console.WriteLine("\n--- ML.NET Model Metrics ---");
            Console.WriteLine($"Micro Accuracy:    {metrics.MicroAccuracy:P2}");
            Console.WriteLine($"Macro Accuracy:    {metrics.MacroAccuracy:P2}");
            Console.WriteLine($"Log Loss:          {metrics.LogLoss:F4}");
            Console.WriteLine($"Log Loss Reduction: {metrics.LogLossReduction:F4}");
            Console.WriteLine("----------------------------\n");

            // 8. Serialize and Save
            var directory = Path.GetDirectoryName(modelOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            mlContext.Model.Save(model, fullDataView.Schema, modelOutputPath);
            Console.WriteLine($"[ML.NET] Model successfully trained and saved to: {modelOutputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ML.NET] Critical Error during model training: {ex.Message}");
            throw;
        }
    }
}
