﻿using OneWare.Core.ViewModels.Windows;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class WaitCalculatorWindow : FlexibleWindow
    {
        private readonly WaitCalculatorWindowViewModel _windowViewModel;

        public WaitCalculatorWindow()
        {
            InitializeComponent();
            _windowViewModel = new WaitCalculatorWindowViewModel();
            DataContext = _windowViewModel;
        }


    }
}