using Microsoft.Maui.Graphics;

namespace Peak;

public class WordFreshBoardDrawable : IDrawable
{
    private List<List<char>> _board = new();
    private List<(int row, int col)> _selected = new();
    private WordFreshSelectionState _state;
    private Dictionary<(int row, int col), float> _fallOffsets = new();
    private Dictionary<(int row, int col), float> _fadeOpacities = new();

    private RectF _rect;

    const float Tile = 58;
    const float Gap = 8;

    public void SetBoard(List<List<char>> board)
    {
        _board = board ?? new();
    }

    public void SetSelection(List<(int row, int col)> sel, WordFreshSelectionState state)
    {
        _selected = sel ?? new();
        _state = state;
    }

    public void SetFallOffsets(Dictionary<(int row, int col), float> offsets)
    {
        _fallOffsets = offsets ?? new();
    }

    public void SetFadeOpacities(Dictionary<(int row, int col), float> opacities)
    {
        _fadeOpacities = opacities ?? new();
    }

    public (int row, int col)? HitTest(PointF p)
    {
        if (_board.Count == 0)
            return null;

        int rows = _board.Count;
        int cols = _board[0].Count;

        float totalW = cols * Tile + (cols - 1) * Gap;
        float totalH = rows * Tile + (rows - 1) * Gap;

        float sx = _rect.Center.X - totalW / 2;
        float sy = _rect.Center.Y - totalH / 2;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x = sx + c * (Tile + Gap);
                float y = sy + r * (Tile + Gap);

                float offset = 0f;
                if (_fallOffsets.TryGetValue((r, c), out float v))
                    offset = v;

                RectF rect = new(x, y + offset, Tile, Tile);

                if (rect.Contains(p))
                    return (r, c);
            }
        }

        return null;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _rect = dirtyRect;

        int rows = _board.Count;
        if (rows == 0)
            return;

        int cols = _board[0].Count;

        float totalW = cols * Tile + (cols - 1) * Gap;
        float totalH = rows * Tile + (rows - 1) * Gap;

        float sx = dirtyRect.Center.X - totalW / 2;
        float sy = dirtyRect.Center.Y - totalH / 2;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x = sx + c * (Tile + Gap);
                float y = sy + r * (Tile + Gap);

                float offset = 0f;
                if (_fallOffsets.TryGetValue((r, c), out float v))
                    offset = v;

                RectF rect = new(x, y + offset, Tile, Tile);

                canvas.Alpha = 1f;
                if (_fadeOpacities.TryGetValue((r, c), out float a))
                    canvas.Alpha = a;

                bool sel = _selected.Contains((r, c));

                Color fill = sel
                    ? _state switch
                    {
                        WordFreshSelectionState.Success => Colors.Green,
                        WordFreshSelectionState.Error => Colors.Red,
                        _ => Colors.Cyan
                    }
                    : Color.FromArgb("#B8AED0");

                canvas.FillColor = fill;
                canvas.FillRoundedRectangle(rect, 6);

                canvas.StrokeColor = Color.FromArgb("#2A2138");
                canvas.StrokeSize = 2;
                canvas.DrawRoundedRectangle(rect, 6);

                canvas.FontColor = Colors.White;
                canvas.FontSize = 28;
                canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;

                canvas.DrawString(
                    _board[r][c].ToString(),
                    rect,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
                
                canvas.Alpha = 1f;
            }
        }
    }
}