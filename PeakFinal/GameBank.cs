namespace Peak;

public static class GameBank
{
    public static readonly List<GameItem> All = new()
    {
        // MEMORY (4)
        new("mem_01", GameCategory.Memory, "Remember: 5271\nWhat was the number?", null,
            new[] { "5271", "5721", "5277", "5270" }, 0, "The correct answer is the exact number shown."),

        new("mem_02", GameCategory.Memory, "Which pair did you see before? (placeholder)", null,
            new[] { "AB", "AC", "AD", "AE" }, 1, "Placeholder explanation."),

        new("mem_03", GameCategory.Memory, "Which sequence matches? 3-8-2", null,
            new[] { "3-8-2", "3-2-8", "8-3-2", "2-8-3" }, 0, "Pick the exact same order."),

        new("mem_04", GameCategory.Memory, "Pick the item that was NOT shown (placeholder)", null,
            new[] { "🍎", "🍐", "🍌", "🍇" }, 2, "Placeholder explanation."),

        // LANGUAGE (4)
        new("lang_01", GameCategory.Language, "Which is a synonym of “wary”?", null,
            new[] { "Careful", "Plump", "Envious", "Flustered" }, 0, "Wary means cautious."),

        new("lang_02", GameCategory.Language, "Cat : Kitten :: Dog : ?", null,
            new[] { "Puppy", "Cub", "Foal", "Calf" }, 0, "A baby dog is a puppy."),

        new("lang_03", GameCategory.Language, "Which word does NOT belong?", null,
            new[] { "Triangle", "Square", "Circle", "Cube" }, 3, "Cube is 3D; others are 2D."),

        new("lang_04", GameCategory.Language, "Opposite of “scarce”:", null,
            new[] { "Rare", "Plentiful", "Small", "Empty" }, 1, "Scarce → plentiful."),

        // PROBLEM SOLVING (4)
        new("ps_01", GameCategory.ProblemSolving, "Next number:\n2, 5, 11, 23, ?", null,
            new[] { "45", "46", "47", "48" }, 2, "×2 + 1 → 23×2+1=47"),

        new("ps_02", GameCategory.ProblemSolving, "Choose the correct result:\n12 + 3 × 2 = ?", null,
            new[] { "30", "18", "24", "15" }, 1, "3×2=6 then 12+6=18."),

        new("ps_03", GameCategory.ProblemSolving, "Solve: 10 ? 5 = 2", null,
            new[] { "+", "−", "×", "÷" }, 3, "10 ÷ 5 = 2."),

        new("ps_04", GameCategory.ProblemSolving, "Odd one out:", null,
            new[] { "9", "16", "25", "27" }, 3, "First three are perfect squares."),

        // FOCUS (4)
        new("focus_01", GameCategory.Focus, "Pick the DIFFERENT symbol:", null,
            new[] { "●", "●", "●", "○" }, 3, "Only one is hollow."),

        new("focus_02", GameCategory.Focus, "Which is a VOWEL?", null,
            new[] { "B", "E", "T", "K" }, 1, "E is a vowel."),

        new("focus_03", GameCategory.Focus, "Select the number 7", null,
            new[] { "1", "7", "9", "4" }, 1, "Tap 7."),

        new("focus_04", GameCategory.Focus, "Which word is in ALL CAPS?", null,
            new[] { "Hello", "WORLD", "nice", "maui" }, 1, "WORLD is all caps."),

        // EMOTION (4)
        new("emo_01", GameCategory.Emotion, "Best response when upset:", null,
            new[] {
                "I’ll never talk to you again.",
                "I’m upset — can we talk calmly later?",
                "You always ruin everything.",
                "I don’t care."
            }, 1, "Healthy response: pause + communicate calmly."),

        new("emo_02", GameCategory.Emotion, "Which is a helpful self-talk?", null,
            new[] {
                "I always fail.",
                "I can improve with practice.",
                "Everyone hates me.",
                "I’m not good at anything."
            }, 1, "Growth mindset improves resilience."),

        new("emo_03", GameCategory.Emotion, "What’s the best next step after making a mistake?", null,
            new[] {
                "Hide it.",
                "Blame others.",
                "Learn what happened and try again.",
                "Quit immediately."
            }, 2, "Learn + adjust is best."),

        new("emo_04", GameCategory.Emotion, "You feel anxious. What helps most?", null,
            new[] {
                "Slow breathing and grounding",
                "Ignoring it forever",
                "More caffeine",
                "Scroll endlessly"
            }, 0, "Breathing + grounding reduces stress response.")
    };
}
