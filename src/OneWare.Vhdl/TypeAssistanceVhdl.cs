using System.Reactive.Disposables;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Highlighting;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;
using Prism.Ioc;

namespace OneWare.Vhdl
{
    internal class TypeAssistanceVhdl : TypeAssistanceLsp, ITypeAssistance
    {
        public static string[,] SectionInfo;

        public TypeAssistanceVhdl(TextEditor editor, ProjectFile file, EditViewModelBase editViewModel, LanguageServiceVhdl ls) : base(editor, file, editViewModel, ls)
        {
            CodeBox.TextArea.IndentationStrategy = IndentationStrategy = new VhdlIndentationStrategy(CodeBox.Options);
            FoldingStrategy = new VhdlFoldingStrategy();
            
            SectionInfo = new string[VHDP.SectionInfo.Info.Count, 2];

            for (var i = 0; i < VHDP.SectionInfo.Info.Count; i++)
            {
                SectionInfo[i, 0] = VHDP.SectionInfo.Info.ElementAt(i).Key;
                SectionInfo[i, 1] = VHDP.SectionInfo.Info.ElementAt(i).Value;
            }
        }

        public override void Initialize(CompletionWindow completion, CompositeDisposable disposableReg)
        {
            base.Initialize(completion, disposableReg);
            EditorThemeManager.Instance.Languages["VHDL"].WhenAnyValue(x => x.SelectedTheme).Subscribe(theme =>
            {
                CodeBox.SyntaxHighlighting = theme.Load();
                CustomHighlightManager = new CustomHighlightManager(CodeBox.SyntaxHighlighting);
                HighlightingManager.Instance.RegisterHighlighting(theme.Name, EditorThemeManager.Instance.Languages["VHDL"].SupportedFiles.Select(x => "." + x.ToString().ToLower()).ToArray(), CodeBox.SyntaxHighlighting);
            }).DisposeWith(disposableReg);
        }

        public IImage ConvertTypeIcon => TypeAssistanceIconStore.Instance.CustomIcons["ConvertType"];

        public IImage OperatorIcon => TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Operator];

        public IImage TypeIcon => TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.TypeParameter];
        public string LineCommentSequence => "--";

        public override void CodeUpdated()
        {
            base.CodeUpdated();
            if (CurrentFile.IsValid() && CurrentFile.Root.IsProjectPreloaded)
                _ = (CurrentFile as ProjectFileVhdl)?.AnalyzeAsync(AnalyzerMode.Indexing | AnalyzerMode.Resolve | AnalyzerMode.Check);
        }

        public override Task<List<CompletionData>> GetCustomCompletionItemsAsync()
        {
            var items = new List<CompletionData>();

            //TODO: 1. Erkennen ob vor Zeilenumbruch -> Auswahl eingrenzen 
            //      2. tabs vor carret erkennen und vor code packen (allgemein nach \n)

            var offset = CodeBox.CaretOffset;

            var lastReturnIndex = offset - 2;
            for (;
                lastReturnIndex > -1 && char.IsWhiteSpace(CodeBox.Text[lastReturnIndex]) &&
                !(CodeBox.Text[lastReturnIndex] == '\n');
                lastReturnIndex--) ;

            if (CheckStart(offset, CodeBox, 'l', 'i'))
                items.Add(new CompletionData("library IEEE;\nuse IEEE.std_logic_1164.all;\nuse IEEE.numeric_std.all; ",
                    "ieee", "IEEE Standard Packages",
                    TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Reference], 0, null, offset));

            if (lastReturnIndex > -1 && CodeBox.Text[lastReturnIndex] == '\n')
            {
                if (CheckStart(offset, CodeBox, 'e'))
                    items.Add(new CompletionData(
                        "entity " + Path.GetFileNameWithoutExtension(CurrentFile.Header) +
                        " is\n    port(\n        [I/Os]$0\n    );\nend entity " +
                        Path.GetFileNameWithoutExtension(CurrentFile.Header) + ";", "entity", "Entity Declaration",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 0, null, offset));
                if (CheckStart(offset, CodeBox, 'p'))
                    items.Add(new CompletionData(
                        "package " + Path.GetFileNameWithoutExtension(CurrentFile.Header) +
                        " is\n\n    [declarations]$0\n\nend package " +
                        Path.GetFileNameWithoutExtension(CurrentFile.Header) + ";", "package", "Package Declaration",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 0, null, offset));
                if (CheckStart(offset, CodeBox, 'p'))
                    items.Add(new CompletionData(
                        "package body " + Path.GetFileNameWithoutExtension(CurrentFile.Header) +
                        " is\n\n    [declarations]$0\n\nend package body " +
                        Path.GetFileNameWithoutExtension(CurrentFile.Header) + ";", "package body",
                        "Package Body Declaration", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 0, null, offset));
                if (CheckStart(offset, CodeBox, 'a'))
                    items.Add(new CompletionData(
                        "architecture rtl of " + Path.GetFileNameWithoutExtension(CurrentFile.Header) +
                        " is\n    [signals]$0\nbegin\n\n    [concurrent statements]\n\nend architecture rtl; ",
                        "architecture", "Architecture Declaration",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 0,null, offset));

                if (CheckStart(offset, CodeBox, 'p', 'a'))
                    items.Add(new CompletionData(
                        "process(clk, rst)\nbegin\n    if rst = [rst_val]$0 then\n        \n    elsif rising_edge(clk) then\n        \n    end if;\nend process; ",
                        "process async rst", "Clocked Process (Asynchronous Reset)",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Method], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'p', 'c'))
                    items.Add(new CompletionData(
                        "process([sensitivity list]$0)\nbegin\n    \nend process;",
                        "process", "Combinational Process",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Method], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'p', 's'))
                    items.Add(new CompletionData(
                        "process(clk)\nbegin\n    if rising_edge(clk) then\n        if rst = [rst_val]$0 then\n            \n        else\n            \n        end if;\n    end if;\nend process; ",
                        "process sync rst", "Clocked Process (Synchronous Reset)",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Method], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'p', 't'))
                    items.Add(new CompletionData("process\nbegin\n    $0\nend process;",
                        "process testbench", "Testbench Process (No Sensitivity List)",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Method], 0,null, offset));

                GetComponents(items , offset);

                if (CheckStart(offset, CodeBox, 'i'))
                    items.Add(new CompletionData("if [condition]$0 then\n    \nend if;", "if", "If Statement",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'i', 'e'))
                    items.Add(new CompletionData("elsif [condition]$0 then", "elsif", "Elsif Statement",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'e'))
                    items.Add(new CompletionData("else", "else", "Else Statement",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'c', 's'))
                    items.Add(new CompletionData(
                        "case [expression]$0 is\n    when [choice] =>\n        \n    when others =>\n        \nend case;",
                        "case", "Case Statement", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function],
                        0,null, offset));
                if (CheckStart(offset, CodeBox, 'f', 'l'))
                    items.Add(new CompletionData("for [loop_var]$0 in [range] loop\n    \nend loop; ", "for", "For Loop",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function], 0,null, offset));

                if (CheckStart(offset, CodeBox, 't', 'a'))
                    items.Add(new CompletionData("type [type_name]$0 is array (range) of [element_type];", "type array",
                        "Array type declaration", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Variable],
                        0,null, offset));
                if (CheckStart(offset, CodeBox, 't', 'e'))
                    items.Add(new CompletionData("type [type_name]$0 is ();", "type enum", "Enumeration type declaration",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Variable], 0,null, offset));
                if (CheckStart(offset, CodeBox, 't', 'r'))
                    items.Add(new CompletionData("type [type_name]$0 is record\n    \nend record [type_name];",
                        "type record", "Record type declaration",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Variable], 0,null, offset));
                if (CheckStart(offset, CodeBox, 's', 't'))
                    items.Add(new CompletionData("subtype [subtype_name]$0 is [base_type] range 0 to 7;", "subtype",
                        "Subtype declaration", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Variable], 0,null, offset));

                if (CheckStart(offset, CodeBox, 'g', 'i'))
                    items.Add(new CompletionData(
                        "[generate_label]: if [condition]$0 generate\n    \nend generate [generate_label];",
                        "generate if", "If Generate Statement",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'g', 'f'))
                    items.Add(new CompletionData(
                        "[generate_label]: for [iteration]$0 generate\n    \nend generate [generate_label];",
                        "generate for", "For Generate",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'g', 'c'))
                    items.Add(new CompletionData(
                        "[generate_label]: case [expression]$0 generate\n    when [choice] =>\n        \n    when others =>\n        null;\nend generate [generate_label]; ",
                        "generate case", "Case Generate Statement",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function], 0,null, offset));
                if (CheckStart(offset, CodeBox, 'a'))
                    items.Add(new CompletionData("assert [neg_condition]$0 report [message] severity [note];", "assert",
                        "Assertion", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Function], 0,null, offset));
            }

            DataTypes(items, offset);

            if (CheckStart(offset, CodeBox, 's', 'v'))
                items.Add(new CompletionData("std_logic_vector(7 downto 0)", "std_logic_vector range",
                    "std_logic_vector Type", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.TypeParameter],
                    0,null, offset));
            if (CheckStart(offset, CodeBox, 'i'))
                items.Add(new CompletionData("integer range 0 to 255", "integer range", "Integer (Range Limitation)",
                    TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.TypeParameter], 0,null, offset));
            if (CheckStart(offset, CodeBox, 'n'))
                items.Add(new CompletionData("natural range 0 to 255", "natural range", "Natural (Range Limitation)",
                    TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.TypeParameter], 0,null, offset));
            if (CheckStart(offset, CodeBox, 'u'))
                items.Add(new CompletionData("unsigned(7 downto 0)", "unsigned range", "unsigned Type",
                    TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.TypeParameter], 0,null, offset));
            if (CheckStart(offset, CodeBox, 's'))
                items.Add(new CompletionData("signed(7 downto 0)", "signed range", "signed Type",
                    TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.TypeParameter], 0,null, offset));

            GetConverters(items, "", offset);

            AddOperators(items, offset);

            if (CheckStart(offset, CodeBox, 'o'))
                items.Add(new CompletionData("others => '0'", "others '0'", "Zero Others",
                    TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Value], 0,null, offset));

            var code = new Analyze.Analyze().RemoveComment(CodeBox.Text);
            var wordList = AnalyzerTools.CreateWordList(code, true).Distinct().ToList();
            foreach (var word in wordList)
            {
                var exclude = false;

                foreach (var i in items)
                    if (((string)i.Content).Equals(word, StringComparison.OrdinalIgnoreCase))
                        exclude = true;
                //if (Regex.Match((string)i.Content, @"\b"+ word + @"\b", RegexOptions.IgnoreCase).Success) exclude = true;

                if (!exclude && offset > 0 && word != "" &&
                    char.ToLower(CodeBox.Text[offset - 1]) == char.ToLower(word[0]))
                    items.Add(new CompletionData(word, word, "Used word in document",
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Keyword], 0,null, offset));
            }

            return Task.FromResult(items);
        }

        private bool CheckStart(int offset, TextEditor codeBox, char c, char c2 = ' ')
        {
            return offset > 0 && (char.ToLower(codeBox.Text[offset - 1]) == char.ToLower(c) ||
                                  char.ToLower(codeBox.Text[offset - 1]) == char.ToLower(c2));
        }

        public void GetComponents(List<CompletionData> data, int offset)
        {
            if (CurrentFile is not ProjectFileVhdl vhdlFile) return;
            foreach (var component in vhdlFile.AnalyzerContext.AvailableComponents)
            {
                data.Add(new CompletionData(SegmentInfo.GetComponentPortInsertVhdl(component.Value), 
                    component.Key + " Port Map", "Port map of " + component.Key + " VHDP, VHDL or Verilog component", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 
                    0, offset));
            }

            foreach (var component in vhdlFile.AnalyzerContext.AvailableComponents)
            {
                data.Add(new CompletionData(SegmentInfo.GetComponentInsertVhdl(component.Value),
                    component.Key + " Component", component.Key + " VHDP, VHDL or Verilog component", TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class],
                    0, offset));
            }


            /*for (var j = 0; j < CurrentFile.Root.AnalyzableFiles[i].Attributes.Count; j++)
                if (CurrentFile.Root.AnalyzableFiles[i].Attributes.ElementAt(j).SectionId == 8)
                {
                    var isVhdp = CurrentFile.Root.AnalyzableFiles[i] is ProjectFileVhdp;

                    var myCollection = CurrentFile.Root.AnalyzableFiles[i].Attributes.ElementAt(j);

                    var genericUsed = false;

                    var compGeneric = "generic\n(\n";
                    var compPort = "port\n(\n";

                    var mapGeneric = "generic map\n(\n";
                    var mapPort = "port map\n(\n";

                    var maxLength = 0;
                    for (var k = 0; k < myCollection.SignalList.Count; k++)
                        if (maxLength < myCollection.SignalList[k].Name.Length)
                            maxLength = myCollection.SignalList[k].Name.Length;

                    if (isVhdp)
                    {
                        compPort += RepeatChar(4, " ") + "CLK" + RepeatChar(maxLength - 3, " ") + " : IN STD_LOGIC;\n";
                        mapPort += RepeatChar(4, " ") + "CLK" + RepeatChar(maxLength - 3, " ") + " => \n";
                    }

                    for (var k = 0; k < myCollection.SignalList.Count; k++)
                    {
                        if (myCollection.SignalList[k].Prefix == 3) genericUsed = true;

                        if (myCollection.SignalList[k].Prefix != 3)
                        {
                            compPort += RepeatChar(4, " ") + myCollection.SignalList[k].Name +
                                         RepeatChar(maxLength - myCollection.SignalList[k].Name.Length, " ") + " : " +
                                         myCollection.SignalList[k].Io + " " + myCollection.SignalList[k].Type + " " +
                                         myCollection.SignalList[k].Range + ";\n";
                            if (myCollection.SignalList[k].Default != "")
                                compPort = compPort.Insert(compPort.Length - 2,
                                    " := " + myCollection.SignalList[k].Default);
                        }
                        else
                        {
                            compGeneric += RepeatChar(4, " ") + myCollection.SignalList[k].Name +
                                            RepeatChar(maxLength - myCollection.SignalList[k].Name.Length, " ") +
                                            " : " + myCollection.SignalList[k].Type + " " +
                                            myCollection.SignalList[k].Range + ";\n";
                            if (myCollection.SignalList[k].Default != "")
                                compGeneric = compGeneric.Insert(compGeneric.Length - 2,
                                    " := " + myCollection.SignalList[k].Default);
                        }

                        if (myCollection.SignalList[k].Prefix != 3)
                            mapPort += RepeatChar(4, " ") + myCollection.SignalList[k].Name +
                                        RepeatChar(maxLength - myCollection.SignalList[k].Name.Length, " ") + " => \n";
                        else if (myCollection.SignalList[k].Default != "")
                            mapGeneric += RepeatChar(4, " ") + myCollection.SignalList[k].Name +
                                           RepeatChar(maxLength - myCollection.SignalList[k].Name.Length, " ") +
                                           " => " + myCollection.SignalList[k].Default + ",\n";
                        else
                            mapGeneric += RepeatChar(4, " ") + myCollection.SignalList[k].Name +
                                           RepeatChar(maxLength - myCollection.SignalList[k].Name.Length, " ") +
                                           " => \n";
                    }

                    if (compGeneric.Length > 2 &&
                        compGeneric[(compGeneric.Length - 2)..compGeneric.Length] == ";\n")
                        compGeneric = compGeneric[..(compGeneric.Length - 2)] + "\n";
                    if (compPort.Length > 2 && compPort[(compPort.Length - 2)..compPort.Length] == ";\n")
                        compPort = compPort[..(compPort.Length - 2)] + "\n";

                    if (mapGeneric.Length > 2 && mapGeneric[(mapGeneric.Length - 2)..mapGeneric.Length] == ",\n")
                        mapGeneric = mapGeneric[..(mapGeneric.Length - 2)] + "\n";

                    compGeneric += ");\n";
                    compPort += ");\n";
                    mapGeneric += ")\n";
                    mapPort += ");\n";

                    var compText = "component " + myCollection.InstanceName + " is\n";
                    var mapText = "[instance name]$0: " + myCollection.InstanceName + "\n";

                    if (genericUsed)
                    {
                        compText += compGeneric;
                        mapText += mapGeneric;
                    }

                    compText += compPort + "end component;";
                    mapText += mapPort;

                    data.Add(new CompletionData(compText, "component " + myCollection.InstanceName,
                        "Component declaration of " + myCollection.InstanceName,
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 3,null, offset));
                    data.Add(new CompletionData(mapText, "port map " + myCollection.InstanceName,
                        "Port map of " + myCollection.InstanceName,
                        TypeAssistanceIconStore.Instance.Icons[CompletionItemKind.Class], 3,null, offset));
                }*/
        }

        private void GetConverters(List<CompletionData> data, string lastSignalType, int offset)
        {
            if (lastSignalType == "integer" || lastSignalType == "signed" || lastSignalType == "natural" ||
                lastSignalType == "unsigned" || lastSignalType == "")
            {
                data.Add(new CompletionData("abs", "abs", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Abs, 1],
                    ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("mod", "mod", "Calculates remainder of division: 8 mod 3 = 2 (3*2+2 = 8)",
                    ConvertTypeIcon, 1,null, offset));
            }

            if (lastSignalType == "std_logic_vector" || lastSignalType == "")
            {
                data.Add(new CompletionData("TO_STDLOGICVECTOR([bit_vector]$0)", "STD_LOGIC_VECTOR (BIT_VECTOR)",
                    "BIT_VECTOR -> STD_LOGIC_VECTOR", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("STD_LOGIC_VECTOR(TO_SIGNED([integer]$0, [std_logic_vector]'LENGTH))",
                    "STD_LOGIC_VECTOR (INTEGER)", "INTEGER -> STD_LOGIC_VECTOR", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("STD_LOGIC_VECTOR(TO_UNSIGNED([natural]$0, [std_logic_vector]'LENGTH))",
                    "STD_LOGIC_VECTOR (NATURAL)", "NATURAL -> STD_LOGIC_VECTOR", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("STD_LOGIC_VECTOR([signed]$0)", "STD_LOGIC_VECTOR (SIGNED)",
                    "SIGNED -> STD_LOGIC_VECTOR", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("STD_LOGIC_VECTOR([unsigned]$0)", "STD_LOGIC_VECTOR (UNSIGNED)",
                    "UNSIGNED -> STD_LOGIC_VECTOR", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("std_logic_vector(resize(unsigned([std_logic_vector]$0), new_length))",
                    "resize (STD_LOGIC_VECTOR)", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.ResizeVector, 1],
                    ConvertTypeIcon, 1,null, offset));
            }

            if (lastSignalType == "bit" || lastSignalType == "")
                data.Add(new CompletionData("TO_BIT([std_logic]$0)", "BIT (STD_LOGIC)", "STD_LOGIC -> BIT",
                    ConvertTypeIcon, 1,null, offset));
            if (lastSignalType == "bit_vector" || lastSignalType == "")
                data.Add(new CompletionData("TO_BITVECTOR([std_logic_vector]$0)", "BIT_VECTOR (STD_LOGIC_VECTOR)",
                    "STD_LOGIC_VECTOR -> BIT_VECTOR", ConvertTypeIcon, 1,null, offset));
            if (lastSignalType == "integer" || lastSignalType == "")
            {
                data.Add(new CompletionData("TO_INTEGER(SIGNED([std_logic_vector]$0))", "INTEGER (STD_LOGIC_VECTOR)",
                    "STD_LOGIC_VECTOR -> INTEGER", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("TO_INTEGER([signed]$0)", "INTEGER (SIGNED)", "SIGNED -> INTEGER",
                    ConvertTypeIcon, 1,null, offset));
            }

            if (lastSignalType == "natural" || lastSignalType == "positive" || lastSignalType == "")
            {
                data.Add(new CompletionData("TO_INTEGER(UNSIGNED([std_logic_vector]$0))", "NATURAL (STD_LOGIC_VECTOR)",
                    "STD_LOGIC_VECTOR -> NATURAL", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("TO_INTEGER([unsigned]$0)", "NATURAL (UNSIGNED)", "UNSIGNED -> NATURAL",
                    ConvertTypeIcon, 1,null, offset));
            }

            if (lastSignalType == "unsigned" || lastSignalType == "")
            {
                data.Add(new CompletionData("UNSIGNED([std_logic_vector]$0)", "UNSIGNED (STD_LOGIC_VECTOR)",
                    "STD_LOGIC_VECTOR -> UNSIGNED", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("TO_UNSIGNED([natural]$0, [unsigned]'LENGTH)", "UNSIGNED (NATURAL)",
                    "NATURAL -> UNSIGNED", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("resize([unsigned]$0, [new length])", "resize (UNSIGNED)",
                    SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Resize, 1], ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("shift_right([unsigned]$0,[shifts])", "shift_right (UNSIGNED)",
                    SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.ShiftRight, 1], ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("shift_left([unsigned]$0,[shifts])", "shift_left (UNSIGNED)",
                    SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.ShiftLeft, 1], ConvertTypeIcon, 1,null, offset));
            }

            if (lastSignalType == "signed" || lastSignalType == "")
            {
                data.Add(new CompletionData("SIGNED([std_logic_vector]$0)", "SIGNED (STD_LOGIC_VECTOR)",
                    "STD_LOGIC_VECTOR -> SIGNED", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("TO_SIGNED([integer]$0, [signed]'LENGTH)", "SIGNED (INTEGER)",
                    "INTEGER -> SIGNED", ConvertTypeIcon, 1,null, offset));
                data.Add(new CompletionData("resize([signed]$0, [new length])", "resize (SIGNED)",
                    SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Resize, 1], ConvertTypeIcon, 1,null, offset));
            }
        }

        public void AddOperators(List<CompletionData> data, int offset)
        {
            data.Add(new CompletionData("AND", "AND", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.And, 1], OperatorIcon,
                1,null, offset));
            data.Add(new CompletionData("OR", "OR", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Or, 1], OperatorIcon,
                1,null, offset));
            data.Add(new CompletionData("XOR", "XOR", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Xor, 1], OperatorIcon,
                1,null, offset));
            data.Add(new CompletionData("NAND", "NAND", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Nand, 1],
                OperatorIcon, 1,null, offset));
            data.Add(new CompletionData("NOR", "NOR", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Nor, 1], OperatorIcon,
                1,null, offset));
            data.Add(new CompletionData("XNOR", "XNOR", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Xnor, 1],
                OperatorIcon, 1,null, offset));
        }

        public void DataTypes(List<CompletionData> data, int offset)
        {
            data.Add(new CompletionData("STD_LOGIC", "STD_LOGIC",
                SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.StdLogic, 1], TypeIcon, 1,null, offset));
            data.Add(new CompletionData("STD_LOGIC_VECTOR", "STD_LOGIC_VECTOR",
                SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.StdLogicVector, 1], TypeIcon, 2,null, offset));
            data.Add(new CompletionData("BIT", "BIT", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Bit, 1], TypeIcon, 3,null, offset));
            data.Add(new CompletionData("BIT_VECTOR", "BIT_VECTOR",
                SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.BitVector, 1], TypeIcon, 4,null, offset));
            data.Add(new CompletionData("BOOLEAN", "BOOLEAN", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Boolean, 1],
                TypeIcon, 5,null, offset));
            data.Add(new CompletionData("INTEGER", "INTEGER", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Integer, 1],
                TypeIcon, 6,null, offset));
            data.Add(new CompletionData("NATURAL", "NATURAL", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Natural, 1],
                TypeIcon, 7,null, offset));
            data.Add(new CompletionData("POSITIVE", "POSITIVE", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Positive, 1],
                TypeIcon, 8,null, offset));
            data.Add(new CompletionData("UNSIGNED", "UNSIGNED", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Unsigned, 1],
                TypeIcon, 9,null, offset));
            data.Add(new CompletionData("SIGNED", "SIGNED", SectionInfo[(int)VHDP.SectionInfo.SelectInfoId.Signal, 1],
                TypeIcon, 10,null, offset));
        }

        public string RepeatChar(int number, string c)
        {
            var returnString = "";
            for (var j = 0; j < number; j++) returnString += c;
            return returnString;
        }

        private object VhdpCompletionDataLspFilter()
        {
            throw new NotImplementedException();
        }

        public override void TypeAssistance(TextInputEventArgs e)
        {
            if (e.Text.Contains(';') && Service.IsLanguageServiceReady)
            {
                var line = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
                Format(line, line);
            }
        }
        
        public override async Task<string> GetHoverInfoAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady || !Global.Options.HoverInformation) return null;

            var pos = CodeBox.Document.GetLocation(offset);

            var error = ContainerLocator.Container.Resolve<ErrorListViewModel>().GetErrorsForFile(CurrentFile).OrderBy(x => x.Type)
                .FirstOrDefault(error => pos.Line >= error.StartLine && pos.Column >= error.StartColumn && pos.Line < error.EndLine || pos.Line == error.EndLine && pos.Column <= error.EndColumn);

            var info = "";
            
            if(error != null) info += error.Description + "\n";
            
            var hover = await Service.RequestHoverAsync(CurrentFile,
                new Position(pos.Line - 1, pos.Column - 1));
            if (hover != null && !IsClosed)
            {
                if (hover.Contents.HasMarkedStrings)
                    info += hover.Contents.MarkedStrings!.First().Value.Split('\n')[0]; //TODO what is this?
                if (hover.Contents.HasMarkupContent) info += hover.Contents.MarkupContent?.Value;
            }

            return string.IsNullOrWhiteSpace(info) ? null : info;
        }
    }
}