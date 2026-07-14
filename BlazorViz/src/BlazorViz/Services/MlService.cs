using BlazorViz.Models;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace BlazorViz.Services;

/// <summary>Predictive analytics with ML.NET: SSA forecasting, SDCA regression, K-Means clustering.</summary>
public sealed class MlService
{
    private readonly MLContext _ml = new(seed: 42);

    public sealed class TimePoint { public float Value { get; set; } }

    public sealed class SsaOutput
    {
        public float[] Forecast { get; set; } = [];
        public float[] LowerBound { get; set; } = [];
        public float[] UpperBound { get; set; } = [];
    }

    /// <summary>Forecasts the next `horizon` points of a numeric series ordered by a time column.</summary>
    public (List<string> Labels, List<double> History, List<double> Forecast, List<double> Lower, List<double> Upper)
        Forecast(TableData data, string timeColumn, string valueColumn, int horizon)
    {
        var sorted = EtlService.Sort(data, timeColumn, desc: false);
        var ti = sorted.IndexOf(timeColumn);
        var vi = sorted.IndexOf(valueColumn);
        if (ti < 0 || vi < 0) throw new InvalidOperationException("Time or value column not found.");

        var labels = sorted.Rows.Select(r => TableData.Format(r[ti])).ToList();
        var series = sorted.Rows.Select(r => (float)(sorted.NumericValue(r[vi]) ?? 0)).ToList();
        if (series.Count < 12) throw new InvalidOperationException("Need at least 12 data points to forecast.");

        var windowSize = Math.Max(2, Math.Min(series.Count / 3, 30));
        var dataView = _ml.Data.LoadFromEnumerable(series.Select(v => new TimePoint { Value = v }));
        var pipeline = _ml.Forecasting.ForecastBySsa(
            outputColumnName: nameof(SsaOutput.Forecast),
            inputColumnName: nameof(TimePoint.Value),
            windowSize: windowSize,
            seriesLength: Math.Min(series.Count, windowSize * 3),
            trainSize: series.Count,
            horizon: horizon,
            confidenceLevel: 0.9f,
            confidenceLowerBoundColumn: nameof(SsaOutput.LowerBound),
            confidenceUpperBoundColumn: nameof(SsaOutput.UpperBound));

        var model = pipeline.Fit(dataView);
        using var engine = model.CreateTimeSeriesEngine<TimePoint, SsaOutput>(_ml);
        var prediction = engine.Predict();

        return (labels,
            series.Select(v => Math.Round((double)v, 2)).ToList(),
            prediction.Forecast.Select(v => Math.Round((double)v, 2)).ToList(),
            prediction.LowerBound.Select(v => Math.Round((double)v, 2)).ToList(),
            prediction.UpperBound.Select(v => Math.Round((double)v, 2)).ToList());
    }

    private sealed class NumericRow
    {
        public float Label { get; set; }
        [VectorType(1)] public float[] Features { get; set; } = [];
    }

    private sealed class RegressionPrediction
    {
        public float Score { get; set; }
    }

    /// <summary>Trains an SDCA regression; returns per-row predictions plus R² and RMSE.</summary>
    public (List<double> Actual, List<double> Predicted, double R2, double Rmse)
        Regression(TableData data, string labelColumn, List<string> featureColumns)
    {
        var li = data.IndexOf(labelColumn);
        var fi = featureColumns.Select(data.IndexOf).Where(i => i >= 0).ToArray();
        if (li < 0 || fi.Length == 0) throw new InvalidOperationException("Label or feature columns not found.");

        var rows = data.Rows
            .Select(r => new NumericRow
            {
                Label = (float)(data.NumericValue(r[li]) ?? 0),
                Features = fi.Select(i => (float)(data.NumericValue(r[i]) ?? 0)).ToArray()
            })
            .ToList();
        if (rows.Count < 10) throw new InvalidOperationException("Need at least 10 rows for regression.");

        var schema = SchemaDefinition.Create(typeof(NumericRow));
        schema[nameof(NumericRow.Features)].ColumnType = new VectorDataViewType(NumberDataViewType.Single, fi.Length);
        var dataView = _ml.Data.LoadFromEnumerable(rows, schema);

        var pipeline = _ml.Transforms.NormalizeMinMax("Features")
            .Append(_ml.Regression.Trainers.Sdca(maximumNumberOfIterations: 100));
        var model = pipeline.Fit(dataView);
        var predictions = model.Transform(dataView);
        var metrics = _ml.Regression.Evaluate(predictions);
        var scores = predictions.GetColumn<float>("Score").ToList();

        return (rows.Select(r => Math.Round((double)r.Label, 2)).ToList(),
                scores.Select(s => Math.Round((double)s, 2)).ToList(),
                Math.Round(metrics.RSquared, 4),
                Math.Round(metrics.RootMeanSquaredError, 4));
    }

    private sealed class BinaryRow
    {
        public bool Label { get; set; }
        [VectorType(1)] public float[] Features { get; set; } = [];
    }

    /// <summary>
    /// Binary classification (SDCA logistic regression). The label column must have exactly two
    /// distinct values (or be boolean); the second value in sort order is treated as positive.
    /// Returns the table with Predicted/Probability columns plus accuracy, AUC and F1.
    /// </summary>
    public (TableData Result, double Accuracy, double Auc, double F1, string PositiveClass)
        BinaryClassify(TableData data, string labelColumn, List<string> featureColumns)
    {
        var li = data.IndexOf(labelColumn);
        var fi = featureColumns.Select(data.IndexOf).Where(i => i >= 0).ToArray();
        if (li < 0 || fi.Length == 0) throw new InvalidOperationException("Label or feature columns not found.");

        var distinct = data.Rows.Select(r => TableData.Format(r[li]))
            .Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count != 2)
            throw new InvalidOperationException($"Binary classification needs exactly 2 label values (found {distinct.Count}: {string.Join(", ", distinct.Take(5))}).");
        var positive = distinct[1];

        var rows = data.Rows
            .Where(r => TableData.Format(r[li]).Length > 0)
            .Select(r => new BinaryRow
            {
                Label = string.Equals(TableData.Format(r[li]), positive, StringComparison.OrdinalIgnoreCase),
                Features = fi.Select(i => (float)(data.NumericValue(r[i]) ?? 0)).ToArray()
            }).ToList();
        if (rows.Count < 20) throw new InvalidOperationException("Need at least 20 labeled rows.");

        var schema = SchemaDefinition.Create(typeof(BinaryRow));
        schema[nameof(BinaryRow.Features)].ColumnType = new VectorDataViewType(NumberDataViewType.Single, fi.Length);
        var dataView = _ml.Data.LoadFromEnumerable(rows, schema);

        var pipeline = _ml.Transforms.NormalizeMinMax("Features")
            .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression(maximumNumberOfIterations: 100));
        var model = pipeline.Fit(dataView);
        var predictions = model.Transform(dataView);
        var metrics = _ml.BinaryClassification.Evaluate(predictions);

        var predicted = predictions.GetColumn<bool>("PredictedLabel").ToList();
        var probability = predictions.GetColumn<float>("Probability").ToList();

        var result = data.Clone();
        result.Columns.Add(new ColumnDef { Name = "Predicted", Type = "string" });
        result.Columns.Add(new ColumnDef { Name = "Probability", Type = "number" });
        var k = 0;
        for (var i = 0; i < result.Rows.Count; i++)
        {
            if (TableData.Format(data.Rows[i][li]).Length == 0)
            {
                result.Rows[i] = [.. result.Rows[i], null, null];
                continue;
            }
            result.Rows[i] = [.. result.Rows[i], predicted[k] ? positive : distinct[0], Math.Round((double)probability[k], 4)];
            k++;
        }
        return (result, Math.Round(metrics.Accuracy, 4), Math.Round(metrics.AreaUnderRocCurve, 4), Math.Round(metrics.F1Score, 4), positive);
    }

    private sealed class MultiRow
    {
        public string Label { get; set; } = "";
        [VectorType(1)] public float[] Features { get; set; } = [];
    }

    /// <summary>Multi-class classification (SDCA maximum entropy). Returns table + PredictedLabel column and accuracy metrics.</summary>
    public (TableData Result, double MicroAccuracy, double MacroAccuracy, int Classes)
        MultiClassify(TableData data, string labelColumn, List<string> featureColumns)
    {
        var li = data.IndexOf(labelColumn);
        var fi = featureColumns.Select(data.IndexOf).Where(i => i >= 0).ToArray();
        if (li < 0 || fi.Length == 0) throw new InvalidOperationException("Label or feature columns not found.");

        var rows = data.Rows
            .Where(r => TableData.Format(r[li]).Length > 0)
            .Select(r => new MultiRow
            {
                Label = TableData.Format(r[li]),
                Features = fi.Select(i => (float)(data.NumericValue(r[i]) ?? 0)).ToArray()
            }).ToList();
        var classes = rows.Select(r => r.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (classes < 2) throw new InvalidOperationException("Need at least 2 label classes.");
        if (rows.Count < 20) throw new InvalidOperationException("Need at least 20 labeled rows.");

        var schema = SchemaDefinition.Create(typeof(MultiRow));
        schema[nameof(MultiRow.Features)].ColumnType = new VectorDataViewType(NumberDataViewType.Single, fi.Length);
        var dataView = _ml.Data.LoadFromEnumerable(rows, schema);

        var pipeline = _ml.Transforms.Conversion.MapValueToKey("LabelKey", nameof(MultiRow.Label))
            .Append(_ml.Transforms.NormalizeMinMax("Features"))
            .Append(_ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "LabelKey"))
            .Append(_ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
        var model = pipeline.Fit(dataView);
        var predictions = model.Transform(dataView);
        var metrics = _ml.MulticlassClassification.Evaluate(predictions, labelColumnName: "LabelKey");
        var predicted = predictions.GetColumn<string>("PredictedLabel").ToList();

        var result = data.Clone();
        result.Columns.Add(new ColumnDef { Name = "PredictedLabel", Type = "string" });
        var k = 0;
        for (var i = 0; i < result.Rows.Count; i++)
        {
            var hasLabel = TableData.Format(data.Rows[i][li]).Length > 0;
            result.Rows[i] = [.. result.Rows[i], hasLabel ? predicted[k++] : null];
        }
        return (result, Math.Round(metrics.MicroAccuracy, 4), Math.Round(metrics.MacroAccuracy, 4), classes);
    }

    private sealed class RatingRow
    {
        public string User { get; set; } = "";
        public string Item { get; set; } = "";
        public float Rating { get; set; }
    }

    private sealed class RatingPrediction
    {
        public float Score { get; set; }
    }

    /// <summary>
    /// Recommendation via matrix factorization: learns user×item affinities from a ratings/interactions
    /// table and returns the top-N unseen items per user.
    /// </summary>
    public (TableData Recommendations, double Rmse) Recommend(
        TableData data, string userColumn, string itemColumn, string ratingColumn, int topN)
    {
        var ui = data.IndexOf(userColumn);
        var ii = data.IndexOf(itemColumn);
        var ri = data.IndexOf(ratingColumn);
        if (ui < 0 || ii < 0 || ri < 0) throw new InvalidOperationException("User, item or rating column not found.");

        var rows = data.Rows
            .Select(r => new RatingRow
            {
                User = TableData.Format(r[ui]),
                Item = TableData.Format(r[ii]),
                Rating = (float)(data.NumericValue(r[ri]) ?? 0)
            })
            .Where(r => r.User.Length > 0 && r.Item.Length > 0)
            .ToList();
        if (rows.Count < 20) throw new InvalidOperationException("Need at least 20 user-item interactions.");

        var dataView = _ml.Data.LoadFromEnumerable(rows);
        var pipeline = _ml.Transforms.Conversion.MapValueToKey("UserKey", nameof(RatingRow.User))
            .Append(_ml.Transforms.Conversion.MapValueToKey("ItemKey", nameof(RatingRow.Item)))
            .Append(_ml.Recommendation().Trainers.MatrixFactorization(new Microsoft.ML.Trainers.MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UserKey",
                MatrixRowIndexColumnName = "ItemKey",
                LabelColumnName = nameof(RatingRow.Rating),
                NumberOfIterations = 30,
                ApproximationRank = 32,
                Quiet = true
            }));
        var model = pipeline.Fit(dataView);
        var metrics = _ml.Regression.Evaluate(model.Transform(dataView), labelColumnName: nameof(RatingRow.Rating));

        var users = rows.Select(r => r.User).Distinct(StringComparer.Ordinal).ToList();
        var items = rows.Select(r => r.Item).Distinct(StringComparer.Ordinal).ToList();
        var seen = rows.Select(r => (r.User, r.Item)).ToHashSet();

        using var engine = _ml.Model.CreatePredictionEngine<RatingRow, RatingPrediction>(model);
        var result = new TableData
        {
            Columns =
            [
                new ColumnDef { Name = userColumn, Type = "string" },
                new ColumnDef { Name = "Rank", Type = "integer" },
                new ColumnDef { Name = "Recommended" + itemColumn, Type = "string" },
                new ColumnDef { Name = "PredictedScore", Type = "number" }
            ]
        };
        foreach (var user in users.Take(200))
        {
            var scored = items
                .Where(item => !seen.Contains((user, item)))
                .Select(item => (Item: item, Score: engine.Predict(new RatingRow { User = user, Item = item }).Score))
                .Where(s => !float.IsNaN(s.Score))
                .OrderByDescending(s => s.Score)
                .Take(Math.Clamp(topN, 1, 20))
                .ToList();
            var rank = 1;
            foreach (var (item, score) in scored)
                result.Rows.Add([user, (long)rank++, item, Math.Round((double)score, 3)]);
        }
        return (result, Math.Round(metrics.RootMeanSquaredError, 4));
    }

    private sealed class ClusterPrediction
    {
        [ColumnName("PredictedLabel")] public uint ClusterId { get; set; }
    }

    /// <summary>K-Means clustering; returns the source table with an appended Cluster column.</summary>
    public TableData Cluster(TableData data, List<string> featureColumns, int k)
    {
        var fi = featureColumns.Select(data.IndexOf).Where(i => i >= 0).ToArray();
        if (fi.Length == 0) throw new InvalidOperationException("Feature columns not found.");
        if (data.RowCount < k) throw new InvalidOperationException("Fewer rows than clusters.");

        var rows = data.Rows
            .Select(r => new NumericRow { Features = fi.Select(i => (float)(data.NumericValue(r[i]) ?? 0)).ToArray() })
            .ToList();

        var schema = SchemaDefinition.Create(typeof(NumericRow));
        schema[nameof(NumericRow.Features)].ColumnType = new VectorDataViewType(NumberDataViewType.Single, fi.Length);
        var dataView = _ml.Data.LoadFromEnumerable(rows, schema);

        var pipeline = _ml.Transforms.NormalizeMinMax("Features")
            .Append(_ml.Clustering.Trainers.KMeans(numberOfClusters: Math.Clamp(k, 2, 20)));
        var model = pipeline.Fit(dataView);
        var clusterIds = model.Transform(dataView).GetColumn<uint>("PredictedLabel").ToList();

        var result = data.Clone();
        result.Columns.Add(new ColumnDef { Name = "Cluster", Type = "integer" });
        for (var i = 0; i < result.Rows.Count; i++)
            result.Rows[i] = [.. result.Rows[i], (long)clusterIds[i]];
        return result;
    }
}
