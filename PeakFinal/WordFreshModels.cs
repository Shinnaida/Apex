namespace Peak;

public class WordFreshRound
{
    public List<string> Rows { get; set; } = new();
}

public record WordFreshTile(int Row, int Col, char Letter);

public enum WordFreshSelectionState
{
    None,
    Selected,
    Success,
    Error
}