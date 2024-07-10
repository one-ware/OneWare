using AvaloniaEdit.Rendering;
using Pair = System.Collections.Generic.KeyValuePair<int, Avalonia.Controls.Control?>;

namespace OneWare.Essentials.EditorExtensions
{
    public class ElementGenerator : VisualLineElementGenerator, IComparer<Pair>
    {
        public List<Pair> Controls = new();

        int IComparer<Pair>.Compare(Pair x, Pair y)
        {
            return x.Key.CompareTo(y.Key);
        }

        /// <summary>
        ///     Gets the first interested offset using binary search
        /// </summary>
        /// <returns>The first interested offset.</returns>
        /// <param name="startOffset">Start offset.</param>
        public override int GetFirstInterestedOffset(int startOffset)
        {
            var pos = Controls.BinarySearch(new Pair(startOffset, null), this);
            if (pos < 0)
                pos = ~pos;
            if (pos < Controls.Count)
                return Controls[pos].Key;
            return -1;
        }

        public override VisualLineElement? ConstructElement(int offset)
        {
            var pos = Controls.BinarySearch(new Pair(offset, null), this);
            if (pos >= 0)
                if(Controls[pos].Value is { } val)
                    return new InlineObjectElement(0, val);
            return null;
        }
    }
}