using System;
using System.Collections.Generic;
using System.Linq;

namespace PDFOrtnerSorter.Infrastructure;

/// <summary>
/// Provides fuzzy matching and string similarity detection
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>
    /// Finds similar strings from a collection based on input
    /// </summary>
    public static List<string> FindSimilar(string input, IEnumerable<string> candidates, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return candidates.Distinct().OrderByDescending(x => x).Take(maxResults).ToList();
        }

        var scored = candidates
            .Distinct()
            .Select(c => new { Candidate = c, Score = CalculateSimilarity(input, c) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Candidate)
            .Take(maxResults)
            .Select(x => x.Candidate)
            .ToList();

        return scored;
    }

    /// <summary>
    /// Calculates similarity score between two strings (0-100)
    /// Uses a combination of fuzzy matching and prefix matching
    /// </summary>
    private static double CalculateSimilarity(string input, string candidate)
    {
        if (string.IsNullOrEmpty(candidate))
            return 0;

        var inputLower = input.ToLowerInvariant();
        var candidateLower = candidate.ToLowerInvariant();

        // Exact match
        if (inputLower == candidateLower)
            return 100;

        // Prefix match (highest priority)
        if (candidateLower.StartsWith(inputLower))
            return 90 + ((double)inputLower.Length / candidateLower.Length * 10);

        // Contains match
        if (candidateLower.Contains(inputLower))
            return 70 + ((double)inputLower.Length / candidateLower.Length * 10);

        // Fuzzy match (all characters in order)
        if (IsFuzzyMatch(inputLower, candidateLower))
        {
            var matchDistance = CalculateMatchDistance(inputLower, candidateLower);
            var score = 50 - (matchDistance * 0.5);
            return Math.Max(1, score);
        }

        // Levenshtein distance for very similar strings
        var distance = LevenshteinDistance(inputLower, candidateLower);
        var maxLength = Math.Max(inputLower.Length, candidateLower.Length);
        var similarity = 1 - ((double)distance / maxLength);
        
        return Math.Max(0, similarity * 40);
    }

    /// <summary>
    /// Checks if all characters in pattern appear in source in the same order
    /// </summary>
    private static bool IsFuzzyMatch(string pattern, string source)
    {
        var patternIndex = 0;
        foreach (var c in source)
        {
            if (patternIndex < pattern.Length && c == pattern[patternIndex])
            {
                patternIndex++;
            }
        }
        return patternIndex == pattern.Length;
    }

    /// <summary>
    /// Calculates the distance between pattern and source for fuzzy matching
    /// </summary>
    private static int CalculateMatchDistance(string pattern, string source)
    {
        var patternIndex = 0;
        var distance = 0;

        foreach (var c in source)
        {
            if (patternIndex < pattern.Length && c == pattern[patternIndex])
            {
                patternIndex++;
            }
            else if (patternIndex < pattern.Length)
            {
                distance++;
            }
        }

        distance += pattern.Length - patternIndex;
        return distance;
    }

    /// <summary>
    /// Calculates Levenshtein distance between two strings
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        var m = s1.Length;
        var n = s2.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++)
            dp[i, 0] = i;

        for (var j = 0; j <= n; j++)
            dp[0, j] = j;

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                if (s1[i - 1] == s2[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1];
                }
                else
                {
                    dp[i, j] = 1 + Math.Min(
                        Math.Min(dp[i - 1, j], dp[i, j - 1]),
                        dp[i - 1, j - 1]);
                }
            }
        }

        return dp[m, n];
    }
}
