using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Avalonia.Media;
using DiffPlex;
using Dock.Model.Mvvm.Controls;
using LibGit2Sharp;
using OneWare.Shared;
using OneWare.SourceControl.EditorExtensions;
using OneWare.SourceControl.Models;

namespace OneWare.SourceControl.ViewModels
{
    public class CompareFileViewModel : Document, IWaitForContent
    {
        private readonly SourceControlViewModel _sourceControlViewModel;
        
        private List<DiffSectionViewModel> _chunks = new();

        private Dictionary<IBrush, int[]> _scrollInfoLeft = new();

        private Dictionary<IBrush, int[]> _scrollInfoRight = new();
        
        private Patch? _patchFile;
        public Patch? PatchFile
        {
            get => _patchFile;
            set
            {
                SetProperty(ref _patchFile, value);
                _ = ParsePatchFileAsync();
            }
        }

        [DataMember]
        public string Path { get; init; }

        public List<DiffSectionViewModel> Chunks
        {
            get => _chunks;
            set => this.SetProperty(ref _chunks, value);
        }

        public Dictionary<IBrush, int[]> ScrollInfoRight
        {
            get => _scrollInfoRight;
            set => this.SetProperty(ref _scrollInfoRight, value);
        }

        public Dictionary<IBrush, int[]> ScrollInfoLeft
        {
            get => _scrollInfoLeft;
            set => this.SetProperty(ref _scrollInfoLeft, value);
        }
        
        public static IBrush AddBrush { get; }
        public static IBrush DeleteBrush { get; }
        public static IBrush WarningBrush { get; }
        
        static CompareFileViewModel()
        {
            AddBrush = new SolidColorBrush(Color.FromArgb(150, 150, 200, 100));
            DeleteBrush = new SolidColorBrush(Color.FromArgb(150, 175, 50, 50));
            WarningBrush = new SolidColorBrush(Color.FromArgb(150, 155, 155, 0));
        }

        public CompareFileViewModel(string path, SourceControlViewModel sourceControlViewModel)
        {
            Path = path;
            _sourceControlViewModel = sourceControlViewModel;
            PatchFile = sourceControlViewModel.GetPatch(path, 10000);
        }

        public void OnContentLoaded()
        {
            async void WaitUntilFree()
            {
                await _sourceControlViewModel.WaitUntilFreeAsync();
                PatchFile = _sourceControlViewModel.GetPatch(Path, 10000);
            }
            WaitUntilFree();
        }

        public override bool OnClose()
        {
            //.Factory.OpenComparisons.Remove(Path);
            return base.OnClose();
        }

        public async Task ParsePatchFileAsync()
        {
            if (PatchFile == null) return;
            
            var diffContents = PatchFile.Content;

            var result = await Task.Run(() =>
            {
                var scrollInfoLeft = new Dictionary<IBrush, int[]>();
                var scrollInfoRight = new Dictionary<IBrush, int[]>();
                var chunks = new List<DiffSectionViewModel>();

                var allLines = diffContents.Split(new[] { "\n" }, StringSplitOptions.None).ToList();

                var arrayOfIndexes = Enumerable.Range(0, allLines.Count);

                var diffSectionHeaders = allLines.Zip(arrayOfIndexes,
                        (x, index) => new { Item = x, Index = index })
                    .Where(x => x.Item.StartsWith("diff --git a"))
                    .ToList();

                foreach (var header in diffSectionHeaders)
                {
                    var hunkElements = allLines
                        .Skip(header.Index + 1)
                        .TakeWhile(x => !x.StartsWith("diff --git a"))
                        .ToList();

                    chunks = ResolveDiffSections(hunkElements);

                    var addIndexLeft = new List<int>();
                    var addIndexRight = new List<int>();
                    var deleteIndexLeft = new List<int>();
                    var deleteIndexRight = new List<int>();
                    foreach (var chunk in chunks)
                    {
                        foreach (var right in chunk.RightDiff)
                        {
                            if (right.Style == DiffContext.Added)
                                addIndexRight.Add(chunk.RightDiff.IndexOf(right)+1);
                            
                            if (right.Style == DiffContext.Deleted)
                                deleteIndexRight.Add(chunk.RightDiff.IndexOf(right)+1);
                        }

                        foreach (var left in chunk.LeftDiff)
                        {
                            if (left.Style == DiffContext.Added)
                                addIndexLeft.Add(chunk.LeftDiff.IndexOf(left)+1);
                            
                            if (left.Style == DiffContext.Deleted)
                                deleteIndexLeft.Add(chunk.LeftDiff.IndexOf(left)+1);
                        }

                        //Generate line differences
                        for (var i = 0; i < chunk.RightDiff.Count && i < chunk.LeftDiff.Count; i++)
                        {
                            var right = chunk.RightDiff[i];
                            var left = chunk.LeftDiff[i];

                            var differences = Differ.Instance.CreateCharacterDiffs(left.Text, right.Text, false);

                            foreach (var difference in differences.DiffBlocks)
                            {
                                left.LineDiffs.Add(new LineDifferenceOffset(difference.DeleteStartA, difference.DeleteCountA));
                                right.LineDiffs.Add(new LineDifferenceOffset(difference.InsertStartB, difference.InsertCountB));
                            }
                        }
                    }
                    
                    scrollInfoLeft.Add(AddBrush, addIndexLeft.ToArray());
                    scrollInfoLeft.Add(DeleteBrush, deleteIndexLeft.ToArray());
                    
                    scrollInfoRight.Add(AddBrush, addIndexRight.ToArray());
                    scrollInfoRight.Add(DeleteBrush, deleteIndexRight.ToArray());
                }

                return (chunks, scrollInfoLeft, scrollInfoRight);
            });

            ScrollInfoLeft = result.scrollInfoLeft;
            ScrollInfoRight = result.scrollInfoRight;
            Chunks = result.chunks;
        }

        private static List<DiffSectionViewModel> ResolveDiffSections(IEnumerable<string> hunkElements)
        {
            var regex = new Regex(
                @"\-(?<leftStart>\d{1,})(\,(?<leftCount>\d{1,})){0,1}\s\+(?<rightStart>\d{1,})(\,(?<rightCount>\d{1,}){0,1})",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var diffContents = hunkElements.Skip(3).Where(x => !x.StartsWith(@"\ No newline at end of file")).ToList();
            var sectionHeaders = diffContents.Where(x => x.StartsWith("@@ ")).ToList();

            var sections = new List<DiffSectionViewModel>();

            foreach (var header in sectionHeaders)
            {
                var section = new DiffSectionViewModel
                {
                    DiffSectionHeader = header,
                    LeftDiff = new List<DiffLineModel>(),
                    RightDiff = new List<DiffLineModel>()
                };

                var lineNumbers = regex.Match(header);
                var startIndex = diffContents.IndexOf(header);
                var innerDiffContents = diffContents.Skip(startIndex + 1).ToList();

                if (!lineNumbers.Groups["leftStart"].Success || !lineNumbers.Groups["rightStart"].Success) continue;

                var leftStart = int.Parse(lineNumbers.Groups["leftStart"].Value);
                var leftDiffSize = lineNumbers.Groups["leftCount"].Success ? int.Parse(lineNumbers.Groups["leftCount"].Value) : 1;
                var rightStart = int.Parse(lineNumbers.Groups["rightStart"].Value);
                var rightDiffSize = lineNumbers.Groups["rightCount"].Success ? int.Parse(lineNumbers.Groups["rightCount"].Value) : 1;


                var removeCounter = 0;

                int leftLineCounter = leftStart, rightLineCounter = rightStart;
                for (var i = 0;
                    i < innerDiffContents.Count && leftLineCounter - leftStart + rightLineCounter - rightStart <
                    rightDiffSize + leftDiffSize;
                    i++)
                {
                    var line = innerDiffContents[i];
                    var leftLineNumberString = leftLineCounter.ToString();
                    var rightLineNumberString = rightLineCounter.ToString();

                    if (line.StartsWith("-"))
                    {
                        section.LeftDiff.Add(DiffLineModel.Create(leftLineNumberString, line));
                        leftLineCounter++;
                        removeCounter++;
                    }
                    else if (line.StartsWith("+"))
                    {
                        section.RightDiff.Add(DiffLineModel.Create(rightLineNumberString, line));
                        if (removeCounter == 0) section.LeftDiff.Add(DiffLineModel.CreateBlank());
                        else removeCounter--;
                        rightLineCounter++;
                    }
                    else
                    {
                        for (; removeCounter > 0; removeCounter--) section.RightDiff.Add(DiffLineModel.CreateBlank());

                        section.LeftDiff.Add(DiffLineModel.Create(leftLineNumberString, line));
                        section.RightDiff.Add(DiffLineModel.Create(rightLineNumberString, line));
                        leftLineCounter++;
                        rightLineCounter++;
                    }
                }

                for (; removeCounter > 0; removeCounter--) section.RightDiff.Add(DiffLineModel.CreateBlank());

                sections.Add(section);
            }

            return sections;
        }
    }

    public class DiffSectionViewModel
    {
        public string DiffSectionHeader { get; set; }
        public List<DiffLineModel> LeftDiff { get; set; }
        public List<DiffLineModel> RightDiff { get; set; }
    }
}