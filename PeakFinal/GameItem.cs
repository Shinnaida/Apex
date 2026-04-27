namespace Peak;

public record GameItem(
    string Id,
    GameCategory Category,
    string Prompt,
    string? ImageSource,
    string[] Options,
    int CorrectIndex,
    string Explanation
);
