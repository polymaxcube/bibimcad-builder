using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Bibimcad.Services;

namespace Bibimcad.UI
{
    public partial class HelpWindow : Window
    {
        private readonly string _statePath;
        private readonly string _logPath;

        public HelpWindow(IList<HelpTopic> current, IList<HelpTopic> common, string statePath, string logPath)
        {
            if (current == null)
                throw new ArgumentNullException("current");
            if (common == null)
                throw new ArgumentNullException("common");

            InitializeComponent();
            _statePath = statePath;
            _logPath = logPath;

            CurrentList.ItemsSource = current.Select(t => new HelpTopicView(t, t.Severity != HelpSeverity.Ok)).ToList();
            CommonList.ItemsSource = common.Select(t => new HelpTopicView(t, false)).ToList();

            int problems = current.Count(t => t.Severity == HelpSeverity.Problem);
            int warnings = current.Count(t => t.Severity == HelpSeverity.Warning);
            SummaryText.Text = "Checked " + statePath + " at " + DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture) + ". "
                + problems.ToString(CultureInfo.InvariantCulture) + " problem(s), "
                + warnings.ToString(CultureInfo.InvariantCulture) + " warning(s). Fix problems from the top down.";
        }

        private void OnOpenState(object sender, RoutedEventArgs e)
        {
            OpenInNotepad(_statePath);
        }

        private void OnOpenLog(object sender, RoutedEventArgs e)
        {
            OpenInNotepad(_logPath);
        }

        private void OpenInNotepad(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "File not found: " + path, "Bibimcad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start("notepad.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open " + path + ": " + ex.Message, "Bibimcad", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public class HelpTopicView
    {
        public HelpTopicView(HelpTopic topic, bool isExpanded)
        {
            Title = topic.Title;
            Detail = topic.Detail;
            Badge = topic.Severity;
            BadgeBrush = BrushFor(topic.Severity);
            IsExpanded = isExpanded;
            Steps = topic.Steps
                .Select((step, i) => new HelpStepView((i + 1).ToString(CultureInfo.InvariantCulture) + ".", step))
                .ToList();
            StepsVisibility = topic.Steps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public string Title { get; private set; }

        public string Detail { get; private set; }

        public string Badge { get; private set; }

        public Brush BadgeBrush { get; private set; }

        public bool IsExpanded { get; private set; }

        public List<HelpStepView> Steps { get; private set; }

        public Visibility StepsVisibility { get; private set; }

        private static Brush BrushFor(string severity)
        {
            switch (severity)
            {
                case HelpSeverity.Problem:
                    return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                case HelpSeverity.Warning:
                    return new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x00));
                case HelpSeverity.Ok:
                    return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                default:
                    return new SolidColorBrush(Color.FromRgb(0x45, 0x6E, 0x9C));
            }
        }
    }

    public class HelpStepView
    {
        public HelpStepView(string number, string text)
        {
            Number = number;
            Text = text;
        }

        public string Number { get; private set; }

        public string Text { get; private set; }
    }
}
