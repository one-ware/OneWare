﻿using OneWare.Core.ViewModels.Windows;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class StringConverterWindow : FlexibleWindow
    {
        public StringConverterWindow()
        {
            InitializeComponent();
            DataContext = new StringConverterWindowViewModel();
            
            ResultBox.SyntaxHighlighting = SourceBox.SyntaxHighlighting;
        }


    }
}