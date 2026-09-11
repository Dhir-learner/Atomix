using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How much help the lab gives on an experiment. Chosen per reaction on the book's LEARN/PERFORM
/// page and remembered in <see cref="AtomixSettings"/>.
///
/// Standard is 0 on purpose: it is what every attempt recorded before levels existed was played
/// at, and a history file written by an older build deserialises the missing field as 0.
/// </summary>
public enum LabDifficulty
{
    /// <summary>Today's settings: the recipe's own tolerance, targets on show.</summary>
    Standard = 0,

    /// <summary>A wider tolerance, targets on show. For a first go.</summary>
    Guided = 1,

    /// <summary>A tighter tolerance and no targets on show. Unlocked by passing Standard.</summary>
    Expert = 2
}

/// <summary>
/// Turns a finished experiment into an accuracy percentage and a one-to-three star rating.
///
/// Until now a titration that landed on the mark and one that scraped in at the edge of the
/// tolerance produced exactly the same result. The engine already knows how far off each reagent
/// was, so the rating costs nothing to work out - and because every stored attempt keeps the
/// amounts used and (in the lab) the targets, attempts made before scoring existed can be rated
/// too; see <see cref="TryGetScore"/>.
///
/// <b>How it is scored.</b> Each reagent scores 1 when it is exactly on target and 0 when it is
/// right at the edge of the accepted range, and the experiment scores its <i>least</i> accurate
/// reagent. The worst reagent rather than the average, because a perfect measurement cannot make
/// up for a careless one - and so that a reagent that arrives as one fixed lump, like the sodium
/// block, cannot pad the score just by always being exact.
///
/// The accepted range used here is always the recipe's own Standard tolerance, whatever level the
/// experiment was played at. That keeps stars comparable: three stars means the same precision on
/// Guided as on Expert, and Guided's wider window lets a student pass, not earn stars they did not.
/// </summary>
public static class ExperimentScoring
{
    /// <summary>Lowest accuracy that earns three stars: every reagent within 40% of the allowed deviation.</summary>
    public const float ThreeStarAccuracy = 60.0f;

    /// <summary>Lowest accuracy that earns two stars: every reagent within 75% of the allowed deviation.</summary>
    public const float TwoStarAccuracy = 25.0f;

    public const int MaxStars = 3;

    /// <summary>Tolerance multiplier for a level. Standard is exactly 1, so today's balance is unchanged.</summary>
    public static float ToleranceScale(LabDifficulty level)
    {
        switch (level)
        {
            case LabDifficulty.Guided: return 1.5f;
            case LabDifficulty.Expert: return 0.6f;
            default: return 1.0f;
        }
    }

    public static string Label(LabDifficulty level)
    {
        switch (level)
        {
            case LabDifficulty.Guided: return "Guided";
            case LabDifficulty.Expert: return "Expert";
            default: return "Standard";
        }
    }

    /// <summary>One line describing what a level changes, for the level picker.</summary>
    public static string Describe(LabDifficulty level)
    {
        switch (level)
        {
            case LabDifficulty.Guided:
                return "Guided: a wider margin for error, with the targets on show.";
            case LabDifficulty.Expert:
                return "Expert: a tighter margin and no targets - you measure from what you know.";
            default:
                return "Standard: the usual margin for error, with the targets on show.";
        }
    }

    /// <summary>
    /// 0..1 for one reagent: 1 exactly on target, falling to 0 at the edge of the accepted range
    /// and staying there beyond it.
    /// </summary>
    public static float ReagentAccuracy(float used, float target, float tolerancePercent)
    {
        if (target <= 0.0f || tolerancePercent <= 0.0f)
        {
            return used <= 0.0f ? 1.0f : 0.0f;
        }

        float allowedDeviation = target * tolerancePercent / 100.0f;
        return Mathf.Clamp01(1.0f - Mathf.Abs(used - target) / allowedDeviation);
    }

    /// <summary>
    /// 0..100, the least accurate reagent. -1 when there is nothing to score, so a caller can tell
    /// "no score" from "scored zero".
    /// </summary>
    public static float Accuracy(IDictionary<string, float> used, IDictionary<string, float> targets,
                                 Func<string, float> tolerancePercentFor)
    {
        if (targets == null || targets.Count == 0 || tolerancePercentFor == null)
        {
            return -1.0f;
        }

        float worst = 1.0f;
        bool any = false;

        foreach (KeyValuePair<string, float> pair in targets)
        {
            if (pair.Value <= 0.0f)
            {
                continue;
            }

            float amount = 0.0f;
            if (used != null)
            {
                used.TryGetValue(pair.Key, out amount);
            }

            worst = Mathf.Min(worst, ReagentAccuracy(amount, pair.Value, tolerancePercentFor(pair.Key)));
            any = true;
        }

        return any ? worst * 100.0f : -1.0f;
    }

    /// <summary>Stars for a verdict. A failure is always 0; a success is always at least 1.</summary>
    public static int Stars(float accuracyPercent, bool succeeded)
    {
        if (!succeeded)
        {
            return 0;
        }

        if (accuracyPercent >= ThreeStarAccuracy)
        {
            return 3;
        }

        return accuracyPercent >= TwoStarAccuracy ? 2 : 1;
    }

    /// <summary>
    /// The score for any attempt with a verdict. Attempts closed since scoring was added carry it;
    /// older ones are worked out here from the amounts they recorded, against the recipe in
    /// <see cref="ReactionDefinition"/> - which also supplies the targets for testing-scene
    /// attempts, whose targets were deliberately never written to the history.
    ///
    /// Nothing is written back: an old attempt is rated each time it is read, so the history file
    /// itself is never rewritten behind the student's back.
    /// </summary>
    public static bool TryGetScore(ExperimentAttempt attempt, out float accuracyPercent, out int stars)
    {
        accuracyPercent = -1.0f;
        stars = 0;

        if (attempt == null)
        {
            return false;
        }

        bool succeeded = attempt.outcome == ExperimentOutcome.Success;
        if (!succeeded && !attempt.IsFailure)
        {
            return false;   // abandoned or still running - no verdict to rate
        }

        if (attempt.scored)
        {
            accuracyPercent = attempt.accuracyPercent;
            stars = attempt.stars;
            return accuracyPercent >= 0.0f || stars > 0;
        }

        // A stoichiometry challenge scales every target, so the standard recipe cannot stand in
        // for targets that were never stored. Challenges are always scored when they close, so
        // this only guards against a damaged file.
        if (attempt.IsChallenge)
        {
            return false;
        }

        ReactionDefinition definition = ReactionDefinition.Load(attempt.reactionId);
        if (definition == null)
        {
            return false;
        }

        IDictionary<string, float> targets = attempt.targetQuantities;
        if (targets == null || targets.Count == 0)
        {
            targets = definition.StandardTargets();
        }

        accuracyPercent = Accuracy(attempt.quantitiesUsed, targets, definition.ToleranceFor);
        if (accuracyPercent < 0.0f)
        {
            return false;
        }

        stars = Stars(accuracyPercent, succeeded);
        return true;
    }
}

/// <summary>
/// A five-pointed star built in code, for the result card and the history list.
///
/// Drawn as a sprite rather than typed as a glyph: the TMP fonts in this project are only known to
/// carry basic Latin and a little punctuation, and a missing glyph renders as an empty box - the
/// worst possible way to show someone the result they just earned.
/// </summary>
public static class StarSprite
{
    private static Sprite cached;

    public static Sprite Get()
    {
        if (cached != null)
        {
            return cached;
        }

        const int size = 64;
        const int samples = 4;   // 4x4 supersampling per pixel for a clean edge
        Vector2[] outline = BuildOutline(size * 0.5f, size * 0.47f, size * 0.20f);

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        Vector2 point = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                        if (Contains(outline, point))
                        {
                            inside++;
                        }
                    }
                }

                pixels[(y * size) + x] = new Color(1.0f, 1.0f, 1.0f, inside / (float)(samples * samples));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        cached = Sprite.Create(texture, new Rect(0.0f, 0.0f, size, size), new Vector2(0.5f, 0.5f));
        return cached;
    }

    /// <summary>Ten points alternating between the outer and inner radius, first point straight up.</summary>
    private static Vector2[] BuildOutline(float centre, float outer, float inner)
    {
        Vector2[] points = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.Deg2Rad * (90.0f + i * 36.0f);
            float radius = (i % 2 == 0) ? outer : inner;
            points[i] = new Vector2(centre + Mathf.Cos(angle) * radius, centre + Mathf.Sin(angle) * radius);
        }
        return points;
    }

    /// <summary>Even-odd point-in-polygon test.</summary>
    private static bool Contains(Vector2[] polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                          (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
