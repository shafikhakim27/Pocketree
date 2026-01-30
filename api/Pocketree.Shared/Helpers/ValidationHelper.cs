using System.Text.RegularExpressions;

namespace Pocketree.Shared.Helpers;

/// <summary>
/// Validation helper methods for common validation scenarios
/// </summary>
public static class ValidationHelper
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    
    private static readonly Regex UsernameRegex = new(
        @"^[a-zA-Z0-9_]{3,20}$",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Validates an email address format
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
            
        return EmailRegex.IsMatch(email);
    }

    /// <summary>
    /// Validates a username (3-20 alphanumeric characters and underscores)
    /// </summary>
    public static bool IsValidUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;
            
        return UsernameRegex.IsMatch(username);
    }

    /// <summary>
    /// Validates password strength (minimum 8 characters, at least one letter and one number)
    /// </summary>
    public static bool IsValidPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return false;
            
        bool hasLetter = password.Any(char.IsLetter);
        bool hasDigit = password.Any(char.IsDigit);
        
        return hasLetter && hasDigit;
    }

    /// <summary>
    /// Validates coin amount is non-negative
    /// </summary>
    public static bool IsValidCoinAmount(int coins)
    {
        return coins >= 0;
    }

    /// <summary>
    /// Validates coordinates are within valid range (0-100)
    /// </summary>
    public static bool AreValidCoordinates(double x, double y)
    {
        return x >= 0 && x <= 100 && y >= 0 && y <= 100;
    }
}
