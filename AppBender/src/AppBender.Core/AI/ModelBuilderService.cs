using System.Globalization;
using AppBender.Core.Common;
using AppBender.Core.Data;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Core.AI;

public interface IModelBuilderService
{
    Task<List<MlModelDefinition>> GetModelsAsync();
    Task<MlModelDefinition> TrainAsync(string name, string entityName, string targetField,
        List<string> featureFields, string modelType);
    Task<object?> PredictAsync(string modelId, Dictionary<string, object?> features);
    Task DeleteAsync(string modelId);
}

/// <summary>
/// Lightweight native ML: multivariate linear regression (normal equations + ridge)
/// and k-nearest-neighbours classification, trained on Data Hub records.
/// </summary>
public class ModelBuilderService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITenantContext tenant,
    IDataHubService dataHub) : IModelBuilderService
{
    public async Task<List<MlModelDefinition>> GetModelsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.MlModels.AsNoTracking()
            .Where(m => m.TenantId == tenant.TenantId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<MlModelDefinition> TrainAsync(string name, string entityName, string targetField,
        List<string> featureFields, string modelType)
    {
        var data = await dataHub.QueryAsync(entityName, new QueryOptions { PageSize = 500 });
        var rows = data.Records.Select(r => r.Data).ToList();
        if (rows.Count < 5)
            throw new InvalidOperationException("Need at least 5 records to train a model.");

        var model = new MlModelDefinition
        {
            TenantId = tenant.TenantId,
            Name = name,
            EntityName = entityName,
            TargetField = targetField,
            FeatureFields = featureFields,
            ModelType = modelType
        };

        switch (modelType)
        {
            case "linear_regression":
                TrainLinearRegression(model, rows, featureFields, targetField);
                break;
            case "logistic_regression":
                TrainLogisticRegression(model, rows, featureFields, targetField);
                break;
            case "recommender":
                TrainRecommender(model, rows, featureFields, targetField);
                break;
            default: // knn_classifier (multi-class)
                TrainKnn(model, rows, featureFields, targetField);
                break;
        }

        model.TrainedAt = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync();
        db.MlModels.Add(model);
        await db.SaveChangesAsync();
        return model;
    }

    private static double Num(object? v) =>
        double.TryParse(TemplateEngine.ToText(v), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static void TrainLinearRegression(MlModelDefinition model,
        List<Dictionary<string, object?>> rows, List<string> features, string target)
    {
        var samples = rows
            .Where(r => r.ContainsKey(target))
            .Select(r => (X: features.Select(f => Num(r.GetValueOrDefault(f))).ToArray(), Y: Num(r[target])))
            .ToList();
        var n = samples.Count;
        var k = features.Count + 1; // + intercept

        // normal equations with ridge regularization: w = (XᵀX + λI)⁻¹ Xᵀy
        var xtx = new double[k, k];
        var xty = new double[k];
        foreach (var (x, y) in samples)
        {
            var xi = new double[k];
            xi[0] = 1;
            for (var j = 0; j < features.Count; j++) xi[j + 1] = x[j];
            for (var a = 0; a < k; a++)
            {
                xty[a] += xi[a] * y;
                for (var b = 0; b < k; b++) xtx[a, b] += xi[a] * xi[b];
            }
        }
        const double lambda = 1e-6;
        for (var i = 0; i < k; i++) xtx[i, i] += lambda;

        var weights = SolveLinearSystem(xtx, xty, k);

        // metrics: R²
        double meanY = samples.Average(s => s.Y), ssTot = 0, ssRes = 0;
        foreach (var (x, y) in samples)
        {
            var prediction = weights[0];
            for (var j = 0; j < features.Count; j++) prediction += weights[j + 1] * x[j];
            ssRes += (y - prediction) * (y - prediction);
            ssTot += (y - meanY) * (y - meanY);
        }
        var r2 = ssTot == 0 ? 1 : 1 - ssRes / ssTot;

        model.ParametersJson = JsonUtil.Serialize(new Dictionary<string, object?> { ["weights"] = weights });
        model.MetricsJson = JsonUtil.Serialize(new Dictionary<string, object?>
        { ["r2"] = Math.Round(r2, 4), ["samples"] = n });
    }

    private static double[] SolveLinearSystem(double[,] a, double[] b, int n)
    {
        // gaussian elimination with partial pivoting
        var m = new double[n, n + 1];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++) m[i, j] = a[i, j];
            m[i, n] = b[i];
        }
        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            for (var row = col + 1; row < n; row++)
                if (Math.Abs(m[row, col]) > Math.Abs(m[pivot, col])) pivot = row;
            if (Math.Abs(m[pivot, col]) < 1e-12) continue;
            if (pivot != col)
                for (var j = 0; j <= n; j++) (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);
            for (var row = 0; row < n; row++)
            {
                if (row == col) continue;
                var factor = m[row, col] / m[col, col];
                for (var j = 0; j <= n; j++) m[row, j] -= factor * m[col, j];
            }
        }
        var solution = new double[n];
        for (var i = 0; i < n; i++)
            solution[i] = Math.Abs(m[i, i]) < 1e-12 ? 0 : m[i, n] / m[i, i];
        return solution;
    }

    /// <summary>Binary classification via logistic regression (gradient descent, max-abs feature scaling).</summary>
    private static void TrainLogisticRegression(MlModelDefinition model,
        List<Dictionary<string, object?>> rows, List<string> features, string target)
    {
        var samples = rows
            .Where(r => r.ContainsKey(target) && r[target] is not null)
            .Select(r => (X: features.Select(f => Num(r.GetValueOrDefault(f))).ToArray(),
                          Label: TemplateEngine.ToText(r[target])))
            .ToList();

        var classes = samples.Select(s => s.Label).Distinct().OrderBy(l => l).ToList();
        if (classes.Count != 2)
            throw new InvalidOperationException(
                $"Binary classification needs exactly 2 distinct target values; found {classes.Count} ({string.Join(", ", classes.Take(5))}). " +
                "Use kNN Classifier for multi-class targets.");

        var negative = classes[0];
        var positive = classes[1];
        var y = samples.Select(s => s.Label == positive ? 1.0 : 0.0).ToArray();

        // max-abs scaling keeps gradient descent stable across value ranges
        var scales = new double[features.Count];
        for (var j = 0; j < features.Count; j++)
        {
            scales[j] = samples.Max(s => Math.Abs(s.X[j]));
            if (scales[j] < 1e-12) scales[j] = 1;
        }
        var x = samples.Select(s => s.X.Select((v, j) => v / scales[j]).ToArray()).ToList();

        var weights = new double[features.Count + 1]; // [bias, w1..wk]
        const double learningRate = 0.3;
        const int epochs = 800;
        for (var epoch = 0; epoch < epochs; epoch++)
        {
            var gradients = new double[weights.Length];
            for (var i = 0; i < x.Count; i++)
            {
                var z = weights[0];
                for (var j = 0; j < features.Count; j++) z += weights[j + 1] * x[i][j];
                var error = Sigmoid(z) - y[i];
                gradients[0] += error;
                for (var j = 0; j < features.Count; j++) gradients[j + 1] += error * x[i][j];
            }
            for (var j = 0; j < weights.Length; j++)
                weights[j] -= learningRate * gradients[j] / x.Count;
        }

        // metrics on the training set
        int tp = 0, tn = 0, fp = 0, fn = 0;
        for (var i = 0; i < x.Count; i++)
        {
            var z = weights[0];
            for (var j = 0; j < features.Count; j++) z += weights[j + 1] * x[i][j];
            var predictedPositive = Sigmoid(z) >= 0.5;
            if (predictedPositive && y[i] == 1) tp++;
            else if (predictedPositive) fp++;
            else if (y[i] == 1) fn++;
            else tn++;
        }
        var accuracy = (double)(tp + tn) / x.Count;
        var precision = tp + fp == 0 ? 0 : (double)tp / (tp + fp);
        var recall = tp + fn == 0 ? 0 : (double)tp / (tp + fn);

        model.ParametersJson = JsonUtil.Serialize(new Dictionary<string, object?>
        {
            ["weights"] = weights,
            ["scales"] = scales,
            ["positive"] = positive,
            ["negative"] = negative
        });
        model.MetricsJson = JsonUtil.Serialize(new Dictionary<string, object?>
        {
            ["accuracy"] = Math.Round(accuracy, 4),
            ["precision"] = Math.Round(precision, 4),
            ["recall"] = Math.Round(recall, 4),
            ["samples"] = x.Count
        });
    }

    private static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));

    /// <summary>
    /// Collaborative-filtering recommender. Features = [userField, itemField],
    /// optional target = rating field (defaults to 1 per interaction).
    /// Predict input: { "&lt;userField&gt;": "user id", "top": 5 } → ranked item list.
    /// </summary>
    private static void TrainRecommender(MlModelDefinition model,
        List<Dictionary<string, object?>> rows, List<string> features, string target)
    {
        if (features.Count < 2)
            throw new InvalidOperationException("Recommender needs two feature fields: user field and item field.");
        var userField = features[0];
        var itemField = features[1];

        var interactions = rows
            .Select(r => new Dictionary<string, object?>
            {
                ["user"] = TemplateEngine.ToText(r.GetValueOrDefault(userField)),
                ["item"] = TemplateEngine.ToText(r.GetValueOrDefault(itemField)),
                ["rating"] = string.IsNullOrEmpty(target) ? 1.0 : Math.Max(Num(r.GetValueOrDefault(target)), 0)
            })
            .Where(i => TemplateEngine.ToText(i["user"]).Length > 0 && TemplateEngine.ToText(i["item"]).Length > 0)
            .ToList();
        if (interactions.Count < 5)
            throw new InvalidOperationException("Need at least 5 user-item interactions to train a recommender.");

        var users = interactions.Select(i => TemplateEngine.ToText(i["user"])).Distinct().Count();
        var items = interactions.Select(i => TemplateEngine.ToText(i["item"])).Distinct().Count();

        model.ParametersJson = JsonUtil.Serialize(new Dictionary<string, object?>
        {
            ["userField"] = userField,
            ["itemField"] = itemField,
            ["interactions"] = interactions
        });
        model.MetricsJson = JsonUtil.Serialize(new Dictionary<string, object?>
        {
            ["users"] = users, ["items"] = items, ["interactions"] = interactions.Count
        });
    }

    private static void TrainKnn(MlModelDefinition model,
        List<Dictionary<string, object?>> rows, List<string> features, string target)
    {
        var samples = rows
            .Where(r => r.ContainsKey(target) && r[target] is not null)
            .Select(r => new Dictionary<string, object?>
            {
                ["x"] = features.Select(f => Num(r.GetValueOrDefault(f))).ToList(),
                ["label"] = TemplateEngine.ToText(r[target])
            })
            .ToList();

        // leave-one-out accuracy with k=3
        var vectors = samples.Select(s => ((List<object?>)s["x"]!).Select(Num).ToArray()).ToList();
        var labels = samples.Select(s => TemplateEngine.ToText(s["label"])).ToList();
        var correct = 0;
        for (var i = 0; i < vectors.Count; i++)
        {
            var predicted = KnnPredict(vectors, labels, vectors[i], excludeIndex: i);
            if (predicted == labels[i]) correct++;
        }
        var accuracy = vectors.Count == 0 ? 0 : (double)correct / vectors.Count;

        model.ParametersJson = JsonUtil.Serialize(new Dictionary<string, object?> { ["samples"] = samples });
        model.MetricsJson = JsonUtil.Serialize(new Dictionary<string, object?>
        { ["accuracy"] = Math.Round(accuracy, 4), ["samples"] = samples.Count, ["k"] = 3 });
    }

    private static string KnnPredict(List<double[]> vectors, List<string> labels, double[] point, int excludeIndex = -1, int k = 3)
    {
        return vectors
            .Select((v, i) => (Index: i, Distance: Distance(v, point)))
            .Where(x => x.Index != excludeIndex)
            .OrderBy(x => x.Distance)
            .Take(k)
            .GroupBy(x => labels[x.Index])
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "";
    }

    private static double Distance(double[] a, double[] b)
    {
        double sum = 0;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            sum += (a[i] - b[i]) * (a[i] - b[i]);
        return Math.Sqrt(sum);
    }

    public async Task<object?> PredictAsync(string modelId, Dictionary<string, object?> features)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var model = await db.MlModels.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == modelId && m.TenantId == tenant.TenantId)
            ?? throw new KeyNotFoundException("Model not found.");

        var featureValues = model.FeatureFields.Select(f => Num(features.GetValueOrDefault(f))).ToArray();
        var parameters = JsonUtil.ToClrDictionary(model.ParametersJson);

        if (model.ModelType == "linear_regression")
        {
            var weights = (parameters["weights"] as List<object?> ?? []).Select(Num).ToArray();
            if (weights.Length == 0) return null;
            var prediction = weights[0];
            for (var j = 0; j < featureValues.Length && j + 1 < weights.Length; j++)
                prediction += weights[j + 1] * featureValues[j];
            return Math.Round(prediction, 4);
        }

        if (model.ModelType == "logistic_regression")
        {
            var weights = (parameters["weights"] as List<object?> ?? []).Select(Num).ToArray();
            var scales = (parameters["scales"] as List<object?> ?? []).Select(Num).ToArray();
            if (weights.Length == 0) return null;
            var z = weights[0];
            for (var j = 0; j < featureValues.Length && j + 1 < weights.Length; j++)
                z += weights[j + 1] * (featureValues[j] / (j < scales.Length && scales[j] != 0 ? scales[j] : 1));
            var probability = Sigmoid(z);
            var label = probability >= 0.5
                ? TemplateEngine.ToText(parameters.GetValueOrDefault("positive"))
                : TemplateEngine.ToText(parameters.GetValueOrDefault("negative"));
            return $"{label} (p={probability:0.###})";
        }

        if (model.ModelType == "recommender")
            return PredictRecommendations(parameters, features);

        var samples = parameters["samples"] as List<object?> ?? [];
        var vectors = new List<double[]>();
        var labels = new List<string>();
        foreach (var sample in samples.OfType<Dictionary<string, object?>>())
        {
            vectors.Add((sample.GetValueOrDefault("x") as List<object?> ?? []).Select(Num).ToArray());
            labels.Add(TemplateEngine.ToText(sample.GetValueOrDefault("label")));
        }
        return KnnPredict(vectors, labels, featureValues);
    }

    /// <summary>User-based collaborative filtering: score unseen items via similar users; popularity fallback.</summary>
    private static object PredictRecommendations(Dictionary<string, object?> parameters, Dictionary<string, object?> input)
    {
        var userField = TemplateEngine.ToText(parameters.GetValueOrDefault("userField"));
        var targetUser = TemplateEngine.ToText(
            input.GetValueOrDefault(userField) ?? input.GetValueOrDefault("user"));
        var top = int.TryParse(TemplateEngine.ToText(input.GetValueOrDefault("top")), out var t) ? Math.Clamp(t, 1, 20) : 5;

        var interactions = (parameters.GetValueOrDefault("interactions") as List<object?> ?? [])
            .OfType<Dictionary<string, object?>>()
            .Select(i => (User: TemplateEngine.ToText(i.GetValueOrDefault("user")),
                          Item: TemplateEngine.ToText(i.GetValueOrDefault("item")),
                          Rating: Num(i.GetValueOrDefault("rating"))))
            .ToList();

        // user → item → rating
        var byUser = interactions.GroupBy(i => i.User)
            .ToDictionary(g => g.Key, g => g.GroupBy(i => i.Item).ToDictionary(x => x.Key, x => x.Max(i => i.Rating)));

        Dictionary<string, double> scores;
        if (targetUser.Length > 0 && byUser.TryGetValue(targetUser, out var seen))
        {
            scores = new Dictionary<string, double>();
            foreach (var (otherUser, otherItems) in byUser)
            {
                if (otherUser == targetUser) continue;
                // cosine similarity over shared items
                double dot = 0, magA = 0, magB = 0;
                foreach (var (item, rating) in seen)
                {
                    magA += rating * rating;
                    if (otherItems.TryGetValue(item, out var otherRating)) dot += rating * otherRating;
                }
                foreach (var rating in otherItems.Values) magB += rating * rating;
                if (dot <= 0 || magA <= 0 || magB <= 0) continue;
                var similarity = dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
                foreach (var (item, rating) in otherItems)
                    if (!seen.ContainsKey(item))
                        scores[item] = scores.GetValueOrDefault(item) + similarity * rating;
            }
            // fallback to popularity when no similar user overlaps
            if (scores.Count == 0)
                scores = Popularity(interactions, seen.Keys.ToHashSet());
        }
        else
        {
            scores = Popularity(interactions, []);
        }

        return scores.OrderByDescending(s => s.Value).Take(top)
            .Select(s => new Dictionary<string, object?> { ["item"] = s.Key, ["score"] = Math.Round(s.Value, 4) })
            .ToList();
    }

    private static Dictionary<string, double> Popularity(
        List<(string User, string Item, double Rating)> interactions, HashSet<string> exclude)
        => interactions.Where(i => !exclude.Contains(i.Item))
            .GroupBy(i => i.Item)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Max(i.Rating, 1)));

    public async Task DeleteAsync(string modelId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.MlModels.Where(m => m.Id == modelId && m.TenantId == tenant.TenantId).ExecuteDeleteAsync();
    }
}
