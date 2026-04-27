namespace Peak;

public enum CompareCategory
{
    Age,
    Profession
}

public sealed record CompareSelection(CompareCategory Category, string Label);

public sealed record CompareBenchmark(
    string Label,
    int Memory,
    int ProblemSolving,
    int Language,
    int MentalAgility,
    int Focus);

public static class CompareBenchmarkService
{
    private const string CompareCategoryKey = "compare_category";
    private const string CompareLabelKey = "compare_label";
    private const string CompareEnabledKey = "compare_enabled";

    private static readonly string[] AgeGroups =
    {
        "13-17",
        "18-24",
        "25-34",
        "35-44",
        "45-54",
        "55-64",
        "65+"
    };

    private static readonly string[] Professions =
    {
        "Actor",
        "Administration",
        "Analyst",
        "Architect",
        "Army",
        "Artist",
        "Athlete",
        "Chef",
        "Construction",
        "Designer",
        "Doctor",
        "Engineer",
        "Farmer",
        "Lawyer",
        "Manager",
        "Marketing",
        "Musician",
        "Nurse",
        "Parent at home",
        "Pilot",
        "Police",
        "Retail",
        "Retired",
        "Software Developer",
        "Student",
        "Teacher",
        "Waiter",
        "Writer"
    };

    public static IReadOnlyList<string> GetAgeGroups() => AgeGroups;

    public static IReadOnlyList<string> GetProfessions() => Professions;

    public static CompareSelection GetSelection()
    {
        var savedCategory = Preferences.Default.Get(CompareCategoryKey, CompareCategory.Profession.ToString());
        var savedLabel = Preferences.Default.Get(CompareLabelKey, string.Empty);

        if (Enum.TryParse<CompareCategory>(savedCategory, out var parsedCategory))
        {
            if (parsedCategory == CompareCategory.Age && AgeGroups.Contains(savedLabel, StringComparer.Ordinal))
            {
                return new CompareSelection(parsedCategory, savedLabel);
            }

            if (parsedCategory == CompareCategory.Profession && Professions.Contains(savedLabel, StringComparer.Ordinal))
            {
                return new CompareSelection(parsedCategory, savedLabel);
            }
        }

        return BuildDefaultSelection();
    }

    public static bool HasActiveComparison()
    {
        return Preferences.Default.Get(CompareEnabledKey, false);
    }

    public static void SaveSelection(CompareCategory category, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        Preferences.Default.Set(CompareCategoryKey, category.ToString());
        Preferences.Default.Set(CompareLabelKey, label.Trim());
        Preferences.Default.Set(CompareEnabledKey, true);
    }

    public static void ClearActiveComparison()
    {
        Preferences.Default.Set(CompareEnabledKey, false);
    }

    public static CompareBenchmark GetBenchmark(CompareSelection selection)
    {
        return selection.Category switch
        {
            CompareCategory.Age => GetAgeBenchmark(selection.Label),
            _ => GetProfessionBenchmark(selection.Label)
        };
    }

    private static CompareSelection BuildDefaultSelection()
    {
        if (LocalAccountStore.TryGetProfile(out var profile))
        {
            var ageGroup = ResolveAgeGroup(profile.Age);
            return new CompareSelection(CompareCategory.Age, ageGroup);
        }

        return new CompareSelection(CompareCategory.Profession, Professions[0]);
    }

    private static string ResolveAgeGroup(int age)
    {
        return age switch
        {
            <= 17 => "13-17",
            <= 24 => "18-24",
            <= 34 => "25-34",
            <= 44 => "35-44",
            <= 54 => "45-54",
            <= 64 => "55-64",
            _ => "65+"
        };
    }

    private static CompareBenchmark GetAgeBenchmark(string label)
    {
        return label switch
        {
            "13-17" => new CompareBenchmark(label, 154, 160, 156, 162, 158),
            "18-24" => new CompareBenchmark(label, 160, 168, 162, 170, 164),
            "25-34" => new CompareBenchmark(label, 166, 172, 168, 174, 168),
            "35-44" => new CompareBenchmark(label, 170, 171, 170, 172, 167),
            "45-54" => new CompareBenchmark(label, 167, 166, 168, 165, 163),
            "55-64" => new CompareBenchmark(label, 162, 160, 165, 158, 158),
            _ => new CompareBenchmark(label, 156, 153, 161, 150, 153)
        };
    }

    private static CompareBenchmark GetProfessionBenchmark(string label)
    {
        var lower = label.Trim().ToLowerInvariant();
        if (ContainsAny(lower, "analyst", "architect", "engineer", "software", "doctor"))
        {
            return new CompareBenchmark(label, 162, 191, 164, 178, 160);
        }

        if (ContainsAny(lower, "lawyer", "writer", "marketing", "artist", "musician", "actor", "designer"))
        {
            return new CompareBenchmark(label, 158, 166, 194, 171, 156);
        }

        if (ContainsAny(lower, "athlete", "army", "police", "pilot", "construction"))
        {
            return new CompareBenchmark(label, 154, 162, 145, 192, 189);
        }

        if (ContainsAny(lower, "chef", "manager", "administration", "retail"))
        {
            return new CompareBenchmark(label, 168, 174, 158, 169, 180);
        }

        if (ContainsAny(lower, "teacher", "student", "parent", "nurse"))
        {
            return new CompareBenchmark(label, 187, 163, 176, 160, 174);
        }

        if (ContainsAny(lower, "farmer", "retired", "waiter"))
        {
            return new CompareBenchmark(label, 170, 155, 150, 176, 183);
        }

        return new CompareBenchmark(label, 165, 168, 162, 167, 166);
    }

    private static bool ContainsAny(string value, params string[] parts)
    {
        foreach (var part in parts)
        {
            if (value.Contains(part, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
