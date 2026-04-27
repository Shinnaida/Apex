namespace Peak;

public enum GameCategory
{
    Memory,
    Language,
    ProblemSolving,
    Focus,
    Emotion
}

public record GameDefinition(
    string Id,
    string Title,
    GameCategory Category,
    Func<ContentPage> CreatePage
);

public static class GameCatalog
{
    public static readonly List<GameDefinition> All = new()
    {
        // ======================
        // MEMORY (4)
        // ======================
        new("mem_match", "Memory Match", GameCategory.Memory, () => new MemoryGamePage()),
        new("mem_number_echo", "Number Echo", GameCategory.Memory, () => new ComingSoonPage("Number Echo")),
        new("mem_sequence_recall", "Sequence Recall", GameCategory.Memory, () => new ComingSoonPage("Sequence Recall")),
        new("mem_visual_recall", "Visual Recall", GameCategory.Memory, () => new ComingSoonPage("Visual Recall")),

        // ======================
        // LANGUAGE (4)
        // ======================
        new("lang_synonyms", "Synonym Snap", GameCategory.Language, () => new ComingSoonPage("Synonym Snap")),
        new("lang_antonyms", "Opposites", GameCategory.Language, () => new ComingSoonPage("Opposites")),
        new("lang_analogy", "Word Analogy", GameCategory.Language, () => new ComingSoonPage("Word Analogy")),
        new("lang_odd_word", "Odd Word Out", GameCategory.Language, () => new ComingSoonPage("Odd Word Out")),

        // ======================
        // PROBLEM SOLVING (4)
        // ======================
        new("ps_number_series", "Number Series", GameCategory.ProblemSolving, () => new ComingSoonPage("Number Series")),
        new("moving_math", "Moving Math", GameCategory.ProblemSolving, () => new MovingMathPage()),
        new("ps_logic_scale", "Scale Logic", GameCategory.ProblemSolving, () => new ComingSoonPage("Scale Logic")),
        new("ps_matrix_reasoning", "Matrix Reasoning", GameCategory.ProblemSolving, () => new ComingSoonPage("Matrix Reasoning")),

        // ======================
        // FOCUS
        // ======================
        new("tap_trap", "Tap Trap", GameCategory.Focus, () => new TapTrapPage()),
        new("decoder", "Decoder", GameCategory.Focus, () => new DecoderPage()),
        new("must_sort", "Must Sort", GameCategory.Focus, () => new MustSortPage()),
        new("true_color", "True Color", GameCategory.Focus, () => new TrueColorPage()),
        new("unique", "Unique", GameCategory.Focus, () => new UniquePage()),

        // ======================
        // EMOTION (4)
        // ======================
        new("emo_best_response", "Best Response", GameCategory.Emotion, () => new ComingSoonPage("Best Response")),
        new("emo_positive_focus", "Positive Focus", GameCategory.Emotion, () => new ComingSoonPage("Positive Focus")),
        new("emo_reframe", "Reframe It", GameCategory.Emotion, () => new ComingSoonPage("Reframe It")),
        new("emo_empathy", "Empathy Choice", GameCategory.Emotion, () => new ComingSoonPage("Empathy Choice")),
    };

    public static List<GameDefinition> ForCategory(GameCategory category)
        => All.Where(g => g.Category == category).ToList();
}
