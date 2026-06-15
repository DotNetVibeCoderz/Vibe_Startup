using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using SoccerWizard.Data;
using SoccerWizard.Models;

namespace SoccerWizard.Services;

/// <summary>
/// Service Machine Learning untuk prediksi pertandingan menggunakan ML.NET
/// </summary>
public class MLPredictionService
{
    private readonly MLContext _mlContext;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private ITransformer? _classifierModel;
    private ITransformer? _scoreRegressorModel;
    private ModelEvaluation? _lastEvaluation;
    
    public MLPredictionService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _mlContext = new MLContext(seed: 42);
        _dbFactory = dbFactory;
    }
    
    /// <summary>
    /// Menyiapkan data training dari database
    /// </summary>
    public async Task<List<MatchData>> PrepareTrainingDataAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        
        var finishedMatches = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Status == "FINISHED" && m.HomeScore.HasValue && m.AwayScore.HasValue)
            .ToListAsync();
        
        var data = new List<MatchData>();
        
        foreach (var match in finishedMatches)
        {
            data.Add(new MatchData
            {
                HomeElo = (float)match.HomeTeam.EloRating,
                AwayElo = (float)match.AwayTeam.EloRating,
                HomeAttackStrength = (float)match.HomeTeam.AttackStrength,
                AwayAttackStrength = (float)match.AwayTeam.AttackStrength,
                HomeDefenseStrength = (float)match.HomeTeam.DefenseStrength,
                AwayDefenseStrength = (float)match.AwayTeam.DefenseStrength,
                HomeMomentum = (float)match.HomeTeam.Momentum,
                AwayMomentum = (float)match.AwayTeam.Momentum,
                HomeAvgGoals = (float)match.HomeTeam.AvgGoalsScored,
                AwayAvgGoals = (float)match.AwayTeam.AvgGoalsScored,
                HomeAvgConceded = (float)match.HomeTeam.AvgGoalsConceded,
                AwayAvgConceded = (float)match.AwayTeam.AvgGoalsConceded,
                HomeWinRate = match.HomeTeam.MatchesPlayed > 0 ? (float)match.HomeTeam.Wins / match.HomeTeam.MatchesPlayed : 0.5f,
                AwayWinRate = match.AwayTeam.MatchesPlayed > 0 ? (float)match.AwayTeam.Wins / match.AwayTeam.MatchesPlayed : 0.5f,
                H2HHomeWins = match.HomeWinsH2H,
                H2HAwayWins = match.AwayWinsH2H,
                H2HDraws = match.DrawsH2H,
                HomeXG = (float)(match.HomeXG ?? 1.5),
                AwayXG = (float)(match.AwayXG ?? 1.2),
                Temperature = (float)(match.Temperature ?? 18),
                Humidity = (float)(match.Humidity ?? 50),
                HomeGoals = (float)(match.HomeScore ?? 0),
                AwayGoals = (float)(match.AwayScore ?? 0),
                HomeWin = match.HomeScore > match.AwayScore,
                IsDraw = match.HomeScore == match.AwayScore
            });
        }
        
        return data;
    }
    
    /// <summary>
    /// Training model klasifikasi untuk prediksi hasil (Win/Draw/Lose)
    /// </summary>
    public async Task<ModelEvaluation> TrainClassifierAsync()
    {
        var trainingData = await PrepareTrainingDataAsync();
        
        if (trainingData.Count < 10)
            throw new InvalidOperationException("Insufficient training data. Need at least 10 completed matches.");
        
        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
        var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);
        
        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(MatchData.HomeElo),
                nameof(MatchData.AwayElo),
                nameof(MatchData.HomeAttackStrength),
                nameof(MatchData.AwayAttackStrength),
                nameof(MatchData.HomeDefenseStrength),
                nameof(MatchData.AwayDefenseStrength),
                nameof(MatchData.HomeMomentum),
                nameof(MatchData.AwayMomentum),
                nameof(MatchData.HomeAvgGoals),
                nameof(MatchData.AwayAvgGoals),
                nameof(MatchData.HomeAvgConceded),
                nameof(MatchData.AwayAvgConceded),
                nameof(MatchData.HomeWinRate),
                nameof(MatchData.AwayWinRate),
                nameof(MatchData.H2HHomeWins),
                nameof(MatchData.H2HAwayWins),
                nameof(MatchData.H2HDraws),
                nameof(MatchData.HomeXG),
                nameof(MatchData.AwayXG)
            )
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(_mlContext.BinaryClassification.Trainers.FastForest(
                labelColumnName: nameof(MatchData.HomeWin),
                numberOfTrees: 100,
                numberOfLeaves: 20));
        
        _classifierModel = pipeline.Fit(split.TrainSet);
        
        var predictions = _classifierModel.Transform(split.TestSet);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: nameof(MatchData.HomeWin));
        
        _lastEvaluation = new ModelEvaluation
        {
            Accuracy = metrics.Accuracy,
            Precision = metrics.PositivePrecision,
            Recall = metrics.PositiveRecall,
            F1Score = metrics.F1Score,
            AUC = metrics.AreaUnderRocCurve,
            TotalSamples = trainingData.Count,
            ConfusionMatrix = $"Acc: {metrics.Accuracy:P2} | Prec: {metrics.PositivePrecision:P2} | Rec: {metrics.PositiveRecall:P2} | AUC: {metrics.AreaUnderRocCurve:P2}"
        };
        
        return _lastEvaluation;
    }
    
    /// <summary>
    /// Training model regresi untuk prediksi skor
    /// </summary>
    public async Task<ModelEvaluation> TrainScoreRegressorAsync()
    {
        var trainingData = await PrepareTrainingDataAsync();
        
        if (trainingData.Count < 10)
            throw new InvalidOperationException("Insufficient training data.");
        
        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
        var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);
        
        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(MatchData.HomeAttackStrength),
                nameof(MatchData.AwayDefenseStrength),
                nameof(MatchData.HomeAvgGoals),
                nameof(MatchData.AwayAvgConceded),
                nameof(MatchData.HomeMomentum),
                nameof(MatchData.HomeElo),
                nameof(MatchData.HomeXG),
                nameof(MatchData.H2HHomeWins)
            )
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(_mlContext.Regression.Trainers.FastForest(
                labelColumnName: nameof(MatchData.HomeGoals),
                numberOfTrees: 100,
                numberOfLeaves: 20));
        
        _scoreRegressorModel = pipeline.Fit(split.TrainSet);
        
        var predictions = _scoreRegressorModel.Transform(split.TestSet);
        var metrics = _mlContext.Regression.Evaluate(predictions, labelColumnName: nameof(MatchData.HomeGoals));
        
        return new ModelEvaluation
        {
            Accuracy = 1.0 - metrics.RootMeanSquaredError / 5.0,
            F1Score = 1.0 - metrics.MeanAbsoluteError / 5.0,
            TotalSamples = trainingData.Count,
            ConfusionMatrix = $"RSquared: {metrics.RSquared:F3} | RMSE: {metrics.RootMeanSquaredError:F3} | MAE: {metrics.MeanAbsoluteError:F3}"
        };
    }
    
    /// <summary>
    /// Memprediksi hasil pertandingan
    /// </summary>
    public async Task<Prediction> PredictMatchAsync(Match match)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        
        var homeTeam = await db.Teams.FindAsync(match.HomeTeamId);
        var awayTeam = await db.Teams.FindAsync(match.AwayTeamId);
        
        if (homeTeam == null || awayTeam == null)
            throw new ArgumentException("Teams not found");
        
        if (_classifierModel == null)
        {
            try { await TrainClassifierAsync(); }
            catch { /* fallback */ }
        }
        
        var poissonResult = CalculatePoissonProbabilities(homeTeam, awayTeam);
        double predictedHomeScore = poissonResult.Item1;
        double predictedAwayScore = poissonResult.Item2;
        double homeWinProb = poissonResult.Item3;
        double awayWinProb = poissonResult.Item4;
        double drawProb = Math.Max(0, 1.0 - homeWinProb - awayWinProb);
        double confidence = 0.5;
        
        if (_classifierModel != null)
        {
            var matchData = CreateMatchData(homeTeam, awayTeam, match);
            var engine = _mlContext.Model.CreatePredictionEngine<MatchData, MatchPrediction>(_classifierModel);
            var prediction = engine.Predict(matchData);
            
            double mlHomeWin = prediction.Probability;
            double mlAwayWin = 1.0 - prediction.Probability;
            
            homeWinProb = (mlHomeWin * 0.6) + (homeWinProb * 0.4);
            awayWinProb = (mlAwayWin * 0.6) + (awayWinProb * 0.4);
            drawProb = Math.Max(0, 1.0 - homeWinProb - awayWinProb);
            confidence = Math.Abs(prediction.Score) / (Math.Abs(prediction.Score) + 1.0);
        }
        
        double total = homeWinProb + drawProb + awayWinProb;
        homeWinProb = Math.Round(homeWinProb / total, 2);
        drawProb = Math.Round(drawProb / total, 2);
        awayWinProb = Math.Round(awayWinProb / total, 2);
        
        string outcome = homeWinProb > awayWinProb ? 
            (homeWinProb > drawProb ? "HOME_WIN" : "DRAW") : 
            (awayWinProb > drawProb ? "AWAY_WIN" : "DRAW");
        
        return new Prediction
        {
            MatchId = match.Id,
            PredictionType = "HYBRID",
            PredictedOutcome = outcome,
            HomeWinProbability = homeWinProb,
            DrawProbability = drawProb,
            AwayWinProbability = awayWinProb,
            PredictedHomeScore = Math.Round(predictedHomeScore, 1),
            PredictedAwayScore = Math.Round(predictedAwayScore, 1),
            Confidence = Math.Round(confidence, 2),
            KeyFactors = GenerateKeyFactors(homeTeam, awayTeam, match),
            LLMExplanation = $"ML Analysis: {homeTeam.Name} (ELO: {homeTeam.EloRating:F0}) vs {awayTeam.Name} (ELO: {awayTeam.EloRating:F0}). " +
                             $"Attack: {homeTeam.AttackStrength:F1} vs Defense: {awayTeam.DefenseStrength:F1}. " +
                             $"Predicted: {predictedHomeScore:F1} - {predictedAwayScore:F1}.",
            ScoreDistribution = System.Text.Json.JsonSerializer.Serialize(poissonResult.Item5),
            CreatedAt = DateTime.UtcNow
        };
    }
    
    private MatchData CreateMatchData(Team homeTeam, Team awayTeam, Match match)
    {
        return new MatchData
        {
            HomeElo = (float)homeTeam.EloRating,
            AwayElo = (float)awayTeam.EloRating,
            HomeAttackStrength = (float)homeTeam.AttackStrength,
            AwayAttackStrength = (float)awayTeam.AttackStrength,
            HomeDefenseStrength = (float)homeTeam.DefenseStrength,
            AwayDefenseStrength = (float)awayTeam.DefenseStrength,
            HomeMomentum = (float)homeTeam.Momentum,
            AwayMomentum = (float)awayTeam.Momentum,
            HomeAvgGoals = (float)homeTeam.AvgGoalsScored,
            AwayAvgGoals = (float)awayTeam.AvgGoalsScored,
            HomeAvgConceded = (float)homeTeam.AvgGoalsConceded,
            AwayAvgConceded = (float)awayTeam.AvgGoalsConceded,
            HomeWinRate = homeTeam.MatchesPlayed > 0 ? (float)homeTeam.Wins / homeTeam.MatchesPlayed : 0.5f,
            AwayWinRate = awayTeam.MatchesPlayed > 0 ? (float)awayTeam.Wins / awayTeam.MatchesPlayed : 0.5f,
            H2HHomeWins = match.HomeWinsH2H,
            H2HAwayWins = match.AwayWinsH2H,
            H2HDraws = match.DrawsH2H,
            HomeXG = (float)(match.HomeXG ?? homeTeam.AvgGoalsScored),
            AwayXG = (float)(match.AwayXG ?? awayTeam.AvgGoalsScored),
            Temperature = (float)(match.Temperature ?? 18),
            Humidity = (float)(match.Humidity ?? 50),
        };
    }
    
    private (double, double, double, double, Dictionary<string, double>) CalculatePoissonProbabilities(Team home, Team away)
    {
        double homeLambda = (home.AttackStrength * 1.5 + away.DefenseStrength * 0.3) * 
                            (home.Momentum * 0.3 + 0.7) * 1.1;
        double awayLambda = (away.AttackStrength * 1.3 + home.DefenseStrength * 0.3) * 
                            (away.Momentum * 0.3 + 0.7) * 0.9;
        
        homeLambda = Math.Max(0.3, homeLambda);
        awayLambda = Math.Max(0.2, awayLambda);
        
        var scoreDist = new Dictionary<string, double>();
        for (int h = 0; h <= 5; h++)
            for (int a = 0; a <= 5; a++)
                scoreDist[$"{h}-{a}"] = Math.Round(PoissonPmf(h, homeLambda) * PoissonPmf(a, awayLambda), 4);
        
        double homeWin = 0, awayWin = 0;
        foreach (var kvp in scoreDist)
        {
            var parts = kvp.Key.Split('-');
            int h = int.Parse(parts[0]), a = int.Parse(parts[1]);
            if (h > a) homeWin += kvp.Value;
            else if (a > h) awayWin += kvp.Value;
        }
        
        return (Math.Round(homeLambda, 1), Math.Round(awayLambda, 1), homeWin, awayWin, scoreDist);
    }
    
    private static double PoissonPmf(int k, double lambda) => Math.Pow(lambda, k) * Math.Exp(-lambda) / Factorial(k);
    
    private static double Factorial(int n)
    {
        if (n <= 1) return 1;
        double result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return result;
    }
    
    private static string GenerateKeyFactors(Team home, Team away, Match match)
    {
        var factors = new List<string>();
        
        if (home.EloRating > away.EloRating + 50)
            factors.Add($"{home.Name} higher ELO ({home.EloRating:F0} vs {away.EloRating:F0})");
        if (home.AttackStrength > away.DefenseStrength + 0.5)
            factors.Add($"{home.Name} attack superior");
        if (match.HomeWinsH2H > match.AwayWinsH2H + 5)
            factors.Add($"{home.Name} leads H2H ({match.HomeWinsH2H}W)");
        if (home.Momentum > 0.75)
            factors.Add($"{home.Name} strong momentum");
        if (factors.Count == 0)
            factors.Add("Very balanced match predicted");
        
        return string.Join("; ", factors);
    }
    
    public ModelEvaluation? GetLastEvaluation() => _lastEvaluation;
}
