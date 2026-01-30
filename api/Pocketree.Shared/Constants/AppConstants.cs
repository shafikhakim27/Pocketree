namespace Pocketree.Shared.Constants;

/// <summary>
/// Application-wide constants for the Pocketree application
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Task difficulty levels
    /// </summary>
    public static class Difficulty
    {
        public const string Easy = "Easy";
        public const string Normal = "Normal";
        public const string Hard = "Hard";
        
        public static readonly string[] All = { Easy, Normal, Hard };
        
        public static bool IsValid(string difficulty) => 
            All.Contains(difficulty, StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Task categories
    /// </summary>
    public static class Categories
    {
        public const string EnergySaving = "Energy Saving";
        public const string Recycling = "Recycling";
        public const string WaterSaving = "Water Saving";
        public const string Nature = "Nature";
        
        public static readonly string[] All = 
        { 
            EnergySaving, 
            Recycling, 
            WaterSaving, 
            Nature 
        };
        
        public static bool IsValid(string category) => 
            All.Contains(category, StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Coin reward values by difficulty
    /// </summary>
    public static class CoinRewards
    {
        public const int Easy = 100;
        public const int Normal = 200;
        public const int Hard = 300;
        
        public static int GetRewardForDifficulty(string difficulty) => difficulty switch
        {
            "Easy" => Easy,
            "Normal" => Normal,
            "Hard" => Hard,
            _ => throw new ArgumentException($"Invalid difficulty: {difficulty}", nameof(difficulty))
        };
    }
    
    /// <summary>
    /// Level configuration
    /// </summary>
    public static class Levels
    {
        public const int SeedlingMinCoins = 0;
        public const int SaplingMinCoins = 250;
        public const int MightyOakMinCoins = 500;
        
        public const string SeedlingName = "Seedling";
        public const string SaplingName = "Sapling";
        public const string MightyOakName = "Mighty Oak";
    }
    
    /// <summary>
    /// Badge criteria types
    /// </summary>
    public static class BadgeCriteria
    {
        public const string LevelUp = "LevelUp";
        public const string TaskCount = "TaskCount";
        public const string Any = "Any";
    }
    
    /// <summary>
    /// Global mission configuration
    /// </summary>
    public static class GlobalMission
    {
        public const int TotalRequiredTrees = 1000;
        public const int PlantingFrequency = 1;
        public const string DefaultMissionName = "Greenify Sahara";
    }
    
    /// <summary>
    /// Session and timeout configuration
    /// </summary>
    public static class Session
    {
        public const int IdleTimeoutMinutes = 30;
    }
    
    /// <summary>
    /// Database configuration
    /// </summary>
    public static class Database
    {
        public const int MaxRetryCount = 10;
        public const int MaxRetryDelaySeconds = 5;
    }
}
