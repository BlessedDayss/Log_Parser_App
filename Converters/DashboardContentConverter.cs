using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Log_Parser_App.Services.Dashboard;

namespace Log_Parser_App.Converters
{
    public class DashboardContentConverter : IMultiValueConverter
    {
        public static readonly DashboardContentConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2 || values[0] is not DashboardType dashboardType || values[1] is not DashboardData dashboardData)
            {
                return CreateEmptyDashboard();
            }

            return CreateDashboardContent(dashboardType, dashboardData);
        }

        private static Control CreateDashboardContent(DashboardType dashboardType, DashboardData dashboardData)
        {
            var container = new StackPanel
            {
                Spacing = 20,
                Orientation = Orientation.Vertical
            };

            // Dashboard title
            var titleBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#2D2F33")),
                BorderBrush = new SolidColorBrush(Color.Parse("#3E3F45")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(10),
                Padding = new Avalonia.Thickness(20)
            };

            var titleStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 5
            };

            titleStack.Children.Add(new TextBlock
            {
                Text = dashboardData.Title,
                FontSize = 20,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            });

            titleStack.Children.Add(new TextBlock
            {
                Text = dashboardData.Subtitle,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#AAAAAA"))
            });

            titleBorder.Child = titleStack;
            container.Children.Add(titleBorder);

            // Simple placeholder for now
            var contentBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#2D2F33")),
                BorderBrush = new SolidColorBrush(Color.Parse("#3E3F45")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(10),
                Padding = new Avalonia.Thickness(20)
            };

            contentBorder.Child = new TextBlock
            {
                Text = $"Dashboard content for {dashboardType} - {dashboardData.Metrics?.Count ?? 0} metrics, {dashboardData.Charts?.Count ?? 0} charts",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#AAAAAA"))
            };

            container.Children.Add(contentBorder);

            return container;
        }

        private static Control CreateEmptyDashboard()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#2D2F33")),
                BorderBrush = new SolidColorBrush(Color.Parse("#3E3F45")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(10),
                Padding = new Avalonia.Thickness(40),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10
            };

            stack.Children.Add(new TextBlock
            {
                Text = "📊",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.5
            });

            stack.Children.Add(new TextBlock
            {
                Text = "No Dashboard Data",
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White)
            });

            border.Child = stack;
            return border;
        }
    }
} 