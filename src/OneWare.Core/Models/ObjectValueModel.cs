using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Core.Models
{
    public class ObjectValueModel : ObservableObject
    {
        private ObservableCollection<ObjectValueModel> _children = new();
        
        public ObservableCollection<ObjectValueModel> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }
        
        private string? _displayName;
        public string? DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }
        
        private string? _value;
        public string? Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        private string? _typeName;
        public string? TypeName
        {
            get => _typeName;
            set => SetProperty(ref _typeName, value);
        }

        private bool? _isExpanded;
        public bool? IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public void DisplayFormat(string format)
        {
            //TODO
        }

        public void AddWatchpoint()
        {
            //TODO
        }

        public void Delete()
        {
            //TODO
        }

        //{name="test",value="{buffer = 0x817c40 \"abc\", capacity = 3, len = 3}"},{name="r",value="8483160"}
        //{buffer = 0x817c40 \"abc\", capacity = 3, len = 3}
        //123213
        public static ObjectValueModel? ParseValue(string name, string value, bool expand = false)
        {
            var stack = new Stack<ObjectValueModel>();

            ObjectValueModel? ret = null;

            var insideString = false;

            var sb = new StringBuilder();

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var lastc = i > 0 ? value[i - 1] : '\n';

                //detect strings
                if (c == '"') insideString = !insideString;

                if (!insideString)
                {
                    if (c == '{')
                    {
                        var newChild = new ObjectValueModel();
                        if (stack.Count > 0)
                        {
                            newChild.DisplayName = sb.ToString().Split('=')[0];
                            sb.Clear();
                            stack.Peek().Children ??= new ObservableCollection<ObjectValueModel>();
                            stack.Peek().Children.Add(newChild);
                            if (expand) stack.Peek().IsExpanded = true;
                        }
                        else
                        {
                            newChild.DisplayName = name;
                        }

                        stack.Push(newChild);
                        continue;
                    }

                    if (c == ',' || c == '}')
                    {
                        if (sb.Length == 0 || stack.Count == 0) continue;

                        stack.Peek().Children ??= new ObservableCollection<ObjectValueModel>();
                        var newChild = new ObjectValueModel();
                        stack.Peek().Children.Add(newChild);
                        if (expand) stack.Peek().IsExpanded = true;

                        FillValue(newChild, sb.ToString());
                        sb.Clear();
                        if (c == '}') ret = stack.Pop();
                        continue;
                    }
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
            {
                ret = new ObjectValueModel();
                FillValue(ret, name + " = " + sb);
            }

            return ret;
        }

        private static void FillValue(ObjectValueModel vm, string vs)
        {
            var pair = vs.Split(" = ");
            vm.DisplayName = pair[0].TrimStart();
            vm.Value = pair.Length > 1 ? pair[1] : null;
        }
    }
}