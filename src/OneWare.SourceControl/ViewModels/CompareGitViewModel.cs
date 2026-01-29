using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DiffPlex;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.EditorExtensions;
using OneWare.SourceControl.Models;

namespace OneWare.SourceControl.ViewModels;

public class CompareGitViewModel : Document, IWaitForContent
{
    private readonly IDisposable? _fileWatcher;
    private readonly SourceControlViewModel _sourceControlViewModel;
    
    public CompareGitViewModel(string fullPath, SourceControlViewModel sourceControlViewModel)
    {
        FullPath = fullPath;
        _sourceControlViewModel = sourceControlViewModel;
        
        LanguageExtension = Path.GetExtension(FullPath);

        _fileWatcher = FileSystemWatcherHelper.WatchFile(FullPath, () => Dispatcher.UIThread.Post(InitializeContent));
    }
    
    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    }
    
    public string LanguageExtension { get; }

    public ICollection<ComparisonControlSection>? Chunks
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    [DataMember] public string FullPath { get; set; }

    public override bool OnClose()
    {
        _fileWatcher?.Dispose();
        return base.OnClose();
    }
    
    public void InitializeContent()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var patch = _sourceControlViewModel.GetPatch(FullPath, 10000);
            if (patch != null)
            {
                await ParsePatchFileAsync(patch);
            }
            else
            {
                Chunks = null;
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }

        IsLoading = false;
    }
    
    private async Task ParsePatchFileAsync(Patch patchFile)
    {
        var diffContents = patchFile.Content;

        var result = await Task.Run(() =>
        {
            var chunks = new List<ComparisonControlSection>();

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
                            addIndexRight.Add(chunk.RightDiff.IndexOf(right) + 1);

                        if (right.Style == DiffContext.Deleted)
                            deleteIndexRight.Add(chunk.RightDiff.IndexOf(right) + 1);
                    }

                    foreach (var left in chunk.LeftDiff)
                    {
                        if (left.Style == DiffContext.Added)
                            addIndexLeft.Add(chunk.LeftDiff.IndexOf(left) + 1);

                        if (left.Style == DiffContext.Deleted)
                            deleteIndexLeft.Add(chunk.LeftDiff.IndexOf(left) + 1);
                    }

                    //Generate line differences
                    for (var i = 0; i < chunk.RightDiff.Count && i < chunk.LeftDiff.Count; i++)
                    {
                        var right = chunk.RightDiff[i];
                        var left = chunk.LeftDiff[i];

                        var differences = Differ.Instance.CreateCharacterDiffs(left.Text, right.Text, false);
                        
                        foreach (var difference in differences.DiffBlocks)
                        {
                            left.LineDiffs.Add(new LineDifferenceOffset(difference.DeleteStartA,
                                difference.DeleteCountA));
                            right.LineDiffs.Add(new LineDifferenceOffset(difference.InsertStartB,
                                difference.InsertCountB));
                        }
                    }
                }
            }

            return chunks;
        });
        
        Chunks = result;
    }

    private static List<ComparisonControlSection> ResolveDiffSections(IEnumerable<string> hunkElements)
    {
        var regex = new Regex(
            @"\-(?<leftStart>\d{1,})(\,(?<leftCount>\d{1,})){0,1}\s\+(?<rightStart>\d{1,})(\,(?<rightCount>\d{1,}){0,1})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var diffContents = hunkElements.Skip(3).Where(x => !x.StartsWith(@"\ No newline at end of file")).ToList();
        var sectionHeaders = diffContents.Where(x => x.StartsWith("@@ ")).ToList();

        var sections = new List<ComparisonControlSection>();

        foreach (var header in sectionHeaders)
        {
            var section = new ComparisonControlSection
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
            var leftDiffSize = lineNumbers.Groups["leftCount"].Success
                ? int.Parse(lineNumbers.Groups["leftCount"].Value)
                : 1;
            var rightStart = int.Parse(lineNumbers.Groups["rightStart"].Value);
            var rightDiffSize = lineNumbers.Groups["rightCount"].Success
                ? int.Parse(lineNumbers.Groups["rightCount"].Value)
                : 1;


            var removeCounter = 0;

            int leftLineCounter = leftStart, rightLineCounter = rightStart;
            for (var i = 0;
                 i < innerDiffContents.Count && leftLineCounter - leftStart + rightLineCounter - rightStart <
                 rightDiffSize + leftDiffSize;
                 i++)
            {
                var line = innerDiffContents[i];

                if (line.StartsWith("-"))
                {
                    section.LeftDiff.Add(DiffLineModel.Create(leftLineCounter, line));
                    leftLineCounter++;
                    removeCounter++;
                }
                else if (line.StartsWith("+"))
                {
                    section.RightDiff.Add(DiffLineModel.Create(rightLineCounter, line));
                    if (removeCounter == 0) section.LeftDiff.Add(DiffLineModel.CreateBlank());
                    else removeCounter--;
                    rightLineCounter++;
                }
                else
                {
                    for (; removeCounter > 0; removeCounter--) section.RightDiff.Add(DiffLineModel.CreateBlank());

                    section.LeftDiff.Add(DiffLineModel.Create(leftLineCounter, line));
                    section.RightDiff.Add(DiffLineModel.Create(rightLineCounter, line));
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