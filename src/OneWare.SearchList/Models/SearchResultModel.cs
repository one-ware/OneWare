using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared;

namespace OneWare.SearchList.Models
{
    public class SearchResultModel : ObservableObject
    {
        public SearchResultModel(string description, string descriptionL, string descriptionM, string descriptionR,
            string search, IProjectRoot? project, IFile? file = null, int line = 0, int startOffset = 0,
            int length = 0)
        {
            Description = description;
            DescriptionLeft = descriptionL;
            DescriptionMid = descriptionM;
            DescriptionRight = descriptionR;
            Project = project;
            File = file;
            Line = line;
            StartOffset = startOffset;
            EndOffset = startOffset + length;
        }

        public string Description { get; set; }
        public string DescriptionLeft { get; set; }
        public string DescriptionMid { get; set; }
        public string DescriptionRight { get; set; }

        public IFile? File { get; set; }

        public IProjectRoot? Project { get; set; }

        public int Line { get; set; }

        public int StartOffset { get; set; }

        public int EndOffset { get; set; }

        public string LineString
        {
            get
            {
                if (Line <= 0) return "";
                return Line + "";
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is SearchResultModel e && e.Description == Description && e.File == File && e.Line == Line &&
                   e.StartOffset == StartOffset && e.EndOffset == EndOffset;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}