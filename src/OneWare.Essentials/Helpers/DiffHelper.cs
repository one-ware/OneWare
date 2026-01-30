using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.SourceControl.EditorExtensions;

namespace OneWare.Essentials.Helpers;

public class DiffHelper
{
    public static List<ComparisonControlSection> BuildDiff(string leftContent, string rightContent)
    {
        var diffBuilder = new SideBySideDiffBuilder(new Differ());
        var diffModel = diffBuilder.BuildDiffModel(leftContent, rightContent);

        var leftDiff = new List<DiffLineModel>();
        var rightDiff = new List<DiffLineModel>();

        var leftLineNumber = 1;
        var rightLineNumber = 1;

        var lineCount = Math.Max(diffModel.OldText.Lines.Count, diffModel.NewText.Lines.Count);
        for (var i = 0; i < lineCount; i++)
        {
            var leftPiece = i < diffModel.OldText.Lines.Count ? diffModel.OldText.Lines[i] : null;
            var rightPiece = i < diffModel.NewText.Lines.Count ? diffModel.NewText.Lines[i] : null;

            leftDiff.Add(BuildLine(leftPiece, true, ref leftLineNumber));
            rightDiff.Add(BuildLine(rightPiece, false, ref rightLineNumber));
        }

        // Generate line differences
        for (var i = 0; i < leftDiff.Count && i < rightDiff.Count; i++)
        {
            var left = leftDiff[i];
            var right = rightDiff[i];
            var differences = Differ.Instance.CreateCharacterDiffs(left.Text, right.Text, false);

            foreach (var difference in differences.DiffBlocks)
            {
                left.LineDiffs.Add(new LineDifferenceOffset(difference.DeleteStartA, difference.DeleteCountA));
                right.LineDiffs.Add(new LineDifferenceOffset(difference.InsertStartB, difference.InsertCountB));
            }
        }

        var chunk = new ComparisonControlSection
        {
            DiffSectionHeader =
                $"@@ -1,{Math.Max(leftLineNumber - 1, 0)} +1,{Math.Max(rightLineNumber - 1, 0)} @@",
            LeftDiff = leftDiff,
            RightDiff = rightDiff
        };

        return ([chunk]);
    }

    private static DiffLineModel BuildLine(DiffPiece? piece, bool isLeft, ref int lineNumber)
    {
        if (piece == null || piece.Type == ChangeType.Imaginary)
        {
            return DiffLineModel.CreateBlank();
        }

        var text = piece.Text ?? string.Empty;

        switch (piece.Type)
        {
            case ChangeType.Unchanged:
            {
                var line = new DiffLineModel(text, DiffContext.Context, lineNumber, "");
                lineNumber++;
                return line;
            }
            case ChangeType.Deleted:
            {
                if (!isLeft) return DiffLineModel.CreateBlank();
                var line = new DiffLineModel(text, DiffContext.Deleted, lineNumber, "-");
                lineNumber++;
                return line;
            }
            case ChangeType.Inserted:
            {
                if (isLeft) return DiffLineModel.CreateBlank();
                var line = new DiffLineModel(text, DiffContext.Added, lineNumber, "+");
                lineNumber++;
                return line;
            }
            case ChangeType.Modified:
            {
                var style = isLeft ? DiffContext.Deleted : DiffContext.Added;
                var prefix = isLeft ? "-" : "+";
                var line = new DiffLineModel(text, style, lineNumber, prefix);
                lineNumber++;
                return line;
            }
            default:
            {
                var line = new DiffLineModel(text, DiffContext.Context, lineNumber, "");
                lineNumber++;
                return line;
            }
        }
    }
}