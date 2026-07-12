using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernFormsNext.Documents;

internal static class DocumentTableLayoutCalculator
{
    public static int[] Calculate(
        int availableWidth,
        IReadOnlyList<int> minimumWidths,
        IReadOnlyList<int> preferredWidths)
    {
        ArgumentNullException.ThrowIfNull(minimumWidths);
        ArgumentNullException.ThrowIfNull(preferredWidths);

        if (minimumWidths.Count != preferredWidths.Count)
            throw new ArgumentException("Minimum and preferred width collections must have the same length.");

        if (minimumWidths.Count == 0)
            return Array.Empty<int>();

        // A positive width for every column is more useful than a mathematically exact but
        // unrenderable zero-width column when the viewport is only a few pixels wide.
        var targetWidth = Math.Max(availableWidth, minimumWidths.Count);
        var minimum = minimumWidths.Select(width => Math.Max(1, width)).ToArray();
        var preferred = preferredWidths
            .Select((width, index) => Math.Max(minimum[index], width))
            .ToArray();
        var minimumTotal = minimum.Sum(width => (long)width);
        var preferredTotal = preferred.Sum(width => (long)width);

        if (targetWidth < minimumTotal)
            return Distribute(targetWidth, minimum);

        if (targetWidth >= preferredTotal)
        {
            var result = (int[])preferred.Clone();
            DistributeExtra(result, targetWidth - (int)Math.Min(targetWidth, preferredTotal), preferred);
            return result;
        }

        var widths = (int[])minimum.Clone();
        var capacity = preferred.Select((width, index) => width - minimum[index]).ToArray();
        DistributeExtra(widths, targetWidth - (int)minimumTotal, capacity);
        return widths;
    }

    private static int[] Distribute(int targetWidth, IReadOnlyList<int> weights)
    {
        var result = Enumerable.Repeat(1, weights.Count).ToArray();
        DistributeExtra(result, targetWidth - result.Length, weights);
        return result;
    }

    private static void DistributeExtra(int[] widths, int extra, IReadOnlyList<int> weights)
    {
        if (extra <= 0)
            return;

        var totalWeight = weights.Sum(weight => (long)Math.Max(0, weight));
        if (totalWeight == 0)
            totalWeight = weights.Count;

        var assigned = 0;
        var fractions = new (int Index, double Fraction)[weights.Count];

        for (var i = 0; i < widths.Length; i++)
        {
            var weight = Math.Max(0, weights[i]);
            var exact = extra * ((totalWeight == weights.Count && weight == 0 ? 1d : weight) / totalWeight);
            var whole = (int)Math.Floor(exact);
            widths[i] += whole;
            assigned += whole;
            fractions[i] = (i, exact - whole);
        }

        foreach (var item in fractions.OrderByDescending(item => item.Fraction).ThenBy(item => item.Index))
        {
            if (assigned >= extra)
                break;

            widths[item.Index]++;
            assigned++;
        }
    }
}
