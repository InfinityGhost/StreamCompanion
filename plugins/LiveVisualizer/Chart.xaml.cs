﻿using LiveCharts;
using LiveCharts.Helpers;
using LiveCharts.Wpf;
using LiveVisualizer.Annotations;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Separator = LiveCharts.Wpf.Separator;

namespace LiveVisualizer
{
    /// <summary>
    /// Interaction logic for Chart.xaml
    /// </summary>
    public partial class Chart : UserControl
    {
        public Chart(IWpfVisualizerData data, string bindingName, bool isProgressChart)
        {
            InitializeComponent();

            var binding = new Binding(bindingName);
            binding.Source = data;

            var solidColorBrush = new SolidColorBrush();
            BindingOperations.SetBinding(solidColorBrush, SolidColorBrush.ColorProperty, binding);
            LineSeries.Fill = solidColorBrush;

            if (isProgressChart)
            {   //Disable drawing of AxisY separator
                binding = new Binding();
                AxisYSeparator.SetBinding(Separator.IsEnabledProperty, binding);
                AxisYSeparator.SetValue(Separator.StrokeThicknessProperty, 0d);
            }

            DataContext = data;
        }
        
    }
}
