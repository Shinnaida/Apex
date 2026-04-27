namespace Peak;

public static class IQQuestionBank
{
    public static readonly List<IQQuestion> All = new()
    {
        // Math & Logic
        new("logic_01", IQCategory.LogicMath, IQDifficulty.Easy,
            "What is the next number in the sequence?\n3, 6, 12, 24, ?",
            null,
            new[] { "30", "36", "42", "48" },
            3,
            "Each number doubles, so 24 becomes 48."),

        new("logic_02", IQCategory.LogicMath, IQDifficulty.Easy,
            "What is 25% of 240?",
            null,
            new[] { "40", "50", "60", "70" },
            2,
            "A quarter of 240 is 60."),

        new("logic_03", IQCategory.LogicMath, IQDifficulty.Medium,
            "A shirt costs 450 pesos and is marked down by 20%. What is the sale price?",
            null,
            new[] { "350", "360", "370", "380" },
            1,
            "Twenty percent of 450 is 90, so 450 minus 90 is 360."),

        new("logic_04", IQCategory.LogicMath, IQDifficulty.Medium,
            "If 4 workers can pack 40 boxes in 5 minutes, how many boxes can 8 workers pack in the same time?",
            null,
            new[] { "60", "70", "80", "90" },
            2,
            "Doubling the workers doubles the output, so 40 becomes 80."),

        new("logic_05", IQCategory.LogicMath, IQDifficulty.Hard,
            "Which number should replace the question mark?\n7, 10, 16, 28, ?",
            null,
            new[] { "44", "46", "48", "52" },
            3,
            "The gaps are +3, +6, +12, so the next gap is +24. That makes 52."),

        // Language
        new("verbal_01", IQCategory.Verbal, IQDifficulty.Easy,
            "Which word is closest in meaning to \"rapid\"?",
            null,
            new[] { "Slow", "Fast", "Quiet", "Careful" },
            1,
            "Rapid means fast."),

        new("verbal_02", IQCategory.Verbal, IQDifficulty.Easy,
            "Choose the best analogy:\nBook : Read :: Song : ?",
            null,
            new[] { "Listen", "Write", "Paint", "Build" },
            0,
            "You read a book and listen to a song."),

        new("verbal_03", IQCategory.Verbal, IQDifficulty.Medium,
            "Which sentence is grammatically correct?",
            null,
            new[]
            {
                "Each of the players have a jersey.",
                "Each of the players has a jersey.",
                "Each of the player have a jersey.",
                "Each of the players having a jersey."
            },
            1,
            "The subject is 'Each', so the correct verb is 'has'."),

        new("verbal_04", IQCategory.Verbal, IQDifficulty.Medium,
            "Which word does NOT belong with the others?",
            null,
            new[] { "Harvest", "Plant", "Sow", "Water" },
            0,
            "Plant, sow, and water are actions used while growing crops. Harvest happens at the end."),

        new("verbal_05", IQCategory.Verbal, IQDifficulty.Hard,
            "Choose the best completion:\nAlthough the task was difficult, Mia stayed calm and remained ____.",
            null,
            new[] { "careless", "confused", "composed", "restless" },
            2,
            "Someone who stays calm remains composed."),

        // Pattern recognition
        new("abstract_01", IQCategory.Abstract, IQDifficulty.Easy,
            "Find the next number in the pattern:\n1, 4, 9, 16, ?",
            null,
            new[] { "20", "24", "25", "36" },
            2,
            "These are square numbers: 1, 2 squared, 3 squared, 4 squared, then 5 squared."),

        new("abstract_02", IQCategory.Abstract, IQDifficulty.Easy,
            "Which letter comes next?\nB, D, G, K, ?",
            null,
            new[] { "N", "O", "P", "Q" },
            2,
            "The gaps grow by 1 each time: +2, +3, +4, so the next gap is +5, giving P."),

        new("abstract_03", IQCategory.Abstract, IQDifficulty.Medium,
            "Choose the missing number:\n2, 6, 12, 20, ?",
            null,
            new[] { "28", "30", "32", "34" },
            1,
            "The differences are +4, +6, +8, so the next difference is +10. That gives 30."),

        new("abstract_04", IQCategory.Abstract, IQDifficulty.Medium,
            "Which option completes the pattern?\nAB, BCD, CDEF, ?",
            null,
            new[] { "DEFG", "DEFGH", "EFGH", "DEFGHI" },
            1,
            "The groups grow by one letter each time and shift forward: 2 letters, 3 letters, 4 letters, then 5 letters."),

        new("abstract_05", IQCategory.Abstract, IQDifficulty.Hard,
            "Which number completes the rule?\n5 -> 15\n8 -> 24\n11 -> ?",
            null,
            new[] { "30", "32", "33", "36" },
            2,
            "Each number is multiplied by 3, so 11 becomes 33."),

        // Visual reasoning
        new("spatial_01", IQCategory.Spatial, IQDifficulty.Easy,
            "How many faces does a cube have?",
            null,
            new[] { "4", "6", "8", "12" },
            1,
            "A cube has 6 faces."),

        new("spatial_02", IQCategory.Spatial, IQDifficulty.Easy,
            "If you rotate a triangle 180 degrees, what stays the same?",
            null,
            new[] { "Its color only", "Its shape and size", "Its direction only", "Its number of sides only" },
            1,
            "Rotation changes orientation, but the triangle keeps the same shape and size."),

        new("spatial_03", IQCategory.Spatial, IQDifficulty.Medium,
            "Which shape is a three-dimensional object?",
            null,
            new[] { "Circle", "Rectangle", "Sphere", "Triangle" },
            2,
            "A sphere has depth, height, and width, so it is 3D."),

        new("spatial_04", IQCategory.Spatial, IQDifficulty.Medium,
            "A square is folded once diagonally. What shape is formed?",
            null,
            new[] { "Rectangle", "Triangle", "Circle", "Pentagon" },
            1,
            "Folding a square on its diagonal creates a triangle shape."),

        new("spatial_05", IQCategory.Spatial, IQDifficulty.Hard,
            "A box has length 4, width 3, and height 2. What is its volume?",
            null,
            new[] { "9", "18", "24", "36" },
            2,
            "Volume is length times width times height: 4 x 3 x 2 = 24."),

        // Science
        new("science_01", IQCategory.Science, IQDifficulty.Easy,
            "Which planet is known as the Red Planet?",
            null,
            new[] { "Venus", "Mars", "Jupiter", "Mercury" },
            1,
            "Mars is commonly called the Red Planet."),

        new("science_02", IQCategory.Science, IQDifficulty.Easy,
            "Which organ pumps blood through the body?",
            null,
            new[] { "Brain", "Lungs", "Heart", "Liver" },
            2,
            "The heart pumps blood."),

        new("science_03", IQCategory.Science, IQDifficulty.Medium,
            "What change of state happens when liquid water becomes water vapor?",
            null,
            new[] { "Freezing", "Condensation", "Evaporation", "Melting" },
            2,
            "Liquid turning into gas is evaporation."),

        new("science_04", IQCategory.Science, IQDifficulty.Medium,
            "Which force pulls objects toward the Earth?",
            null,
            new[] { "Magnetism", "Gravity", "Friction", "Electricity" },
            1,
            "Gravity pulls objects toward the Earth."),

        new("science_05", IQCategory.Science, IQDifficulty.Hard,
            "If a plant does not get enough sunlight, which process is most directly affected?",
            null,
            new[] { "Respiration", "Photosynthesis", "Digestion", "Fermentation" },
            1,
            "Plants need sunlight for photosynthesis."),

        // Philippine history
        new("ph_01", IQCategory.PhilippineHistory, IQDifficulty.Easy,
            "On what date is Philippine Independence Day celebrated?",
            null,
            new[] { "June 12", "August 21", "November 30", "December 30" },
            0,
            "Philippine Independence Day is celebrated on June 12."),

        new("ph_02", IQCategory.PhilippineHistory, IQDifficulty.Easy,
            "Who is widely recognized as the national hero of the Philippines?",
            null,
            new[] { "Andres Bonifacio", "Jose Rizal", "Apolinario Mabini", "Emilio Aguinaldo" },
            1,
            "Jose Rizal is widely recognized as the national hero."),

        new("ph_03", IQCategory.PhilippineHistory, IQDifficulty.Medium,
            "Who became the first President of the Philippines?",
            null,
            new[] { "Manuel L. Quezon", "Sergio Osmena", "Emilio Aguinaldo", "Ferdinand Marcos" },
            2,
            "Emilio Aguinaldo became the first President of the Philippines."),

        new("ph_04", IQCategory.PhilippineHistory, IQDifficulty.Medium,
            "What secret revolutionary society did Andres Bonifacio help found?",
            null,
            new[] { "La Liga Filipina", "Katipunan", "Malolos Congress", "Propaganda Movement" },
            1,
            "Bonifacio helped found the Katipunan."),

        new("ph_05", IQCategory.PhilippineHistory, IQDifficulty.Hard,
            "The 1986 People Power Revolution is also known as the ____ Revolution.",
            null,
            new[] { "Mactan", "Balangiga", "EDSA", "Cry of Pugad Lawin" },
            2,
            "The 1986 People Power Revolution is also called the EDSA Revolution."),
    };

    public static IReadOnlyList<IQQuestion> BuildSessionQuestions(IQTestDefinition definition, Random rng)
    {
        var selected = new List<IQQuestion>();

        foreach (var category in definition.Categories)
        {
            var categoryPool = All
                .Where(question => question.Category == category)
                .ToList();

            if (categoryPool.Count == 0)
            {
                continue;
            }

            var picks = PickBalancedQuestions(categoryPool, definition.QuestionsPerCategory, rng);
            selected.AddRange(picks);
        }

        return selected
            .OrderBy(_ => rng.Next())
            .Take(definition.QuestionCount)
            .ToList();
    }

    static List<IQQuestion> PickBalancedQuestions(List<IQQuestion> pool, int count, Random rng)
    {
        var selected = new List<IQQuestion>();

        foreach (var difficulty in new[] { IQDifficulty.Easy, IQDifficulty.Medium, IQDifficulty.Hard })
        {
            var matching = pool
                .Where(question => question.Difficulty == difficulty && !selected.Contains(question))
                .OrderBy(_ => rng.Next())
                .FirstOrDefault();

            if (matching is not null && selected.Count < count)
            {
                selected.Add(matching);
            }
        }

        while (selected.Count < count && selected.Count < pool.Count)
        {
            var question = pool
                .Where(candidate => !selected.Contains(candidate))
                .OrderBy(_ => rng.Next())
                .First();

            selected.Add(question);
        }

        return selected;
    }
}
