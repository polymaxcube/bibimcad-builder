using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Bibimcad.Models;
using Bibimcad.Services;
using Microsoft.Win32;

namespace Bibimcad.UI
{
    public partial class WallReviewWindow : Window
    {
        private const string AllItems = "(all)";

        private readonly List<WallCandidate> _candidates;
        private readonly List<WallReviewRow> _rows;
        private readonly ICollectionView _view;
        private readonly WallReviewService _review;
        private readonly string _csvPath;

        public WallReviewWindow(IEnumerable<WallCandidate> candidates, double? defaultHeightMm, string csvPath)
        {
            if (candidates == null)
                throw new ArgumentNullException("candidates");

            InitializeComponent();

            _candidates = candidates.Select(c => c.Clone()).ToList();
            _rows = _candidates.Select(c => new WallReviewRow(c)).ToList();
            _review = new WallReviewService(defaultHeightMm);
            _csvPath = csvPath;

            _view = CollectionViewSource.GetDefaultView(_rows);
            _view.Filter = MatchesFilter;
            CandidateGrid.ItemsSource = _view;

            List<string> layers = new List<string> { AllItems };
            layers.AddRange(_candidates.Select(c => c.Layer ?? string.Empty).Distinct().OrderBy(l => l, StringComparer.OrdinalIgnoreCase));
            LayerFilter.ItemsSource = layers;
            LayerFilter.SelectedIndex = 0;

            List<string> statuses = new List<string> { AllItems };
            statuses.AddRange(WallCandidateStatus.All);
            StatusFilter.ItemsSource = statuses;
            StatusFilter.SelectedIndex = 0;

            DefaultHeightText.Text = _review.DefaultHeightMm.HasValue
                ? "Approving a row with no height uses project.defaultWallHeightMm = "
                    + _review.DefaultHeightMm.Value.ToString("0.#", CultureInfo.InvariantCulture)
                    + " mm."
                : "Rows need a height before they can be approved. Set project.defaultWallHeightMm to fill it automatically.";
            if (_review.DefaultHeightMm.HasValue)
                HeightInput.Text = _review.DefaultHeightMm.Value.ToString("0.#", CultureInfo.InvariantCulture);

            UpdateCounts();
        }

        // The edited copies. Read after ShowDialog returns true.
        public IReadOnlyList<WallCandidate> EditedCandidates
        {
            get { return _candidates; }
        }

        private bool MatchesFilter(object item)
        {
            WallReviewRow row = (WallReviewRow)item;
            string layer = LayerFilter.SelectedItem as string;
            string status = StatusFilter.SelectedItem as string;
            if (layer != null && layer != AllItems && !string.Equals(row.Layer ?? string.Empty, layer, StringComparison.OrdinalIgnoreCase))
                return false;
            if (status != null && status != AllItems && !string.Equals(row.Status, status, StringComparison.Ordinal))
                return false;
            return true;
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_view == null)
                return;

            _view.Refresh();
            UpdateCounts();
        }

        private List<WallReviewRow> SelectedRows()
        {
            return CandidateGrid.SelectedItems.Cast<WallReviewRow>().ToList();
        }

        private bool CommitGridEdits()
        {
            return CandidateGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void OnApprove(object sender, RoutedEventArgs e)
        {
            List<WallReviewRow> rows = RequireSelection();
            if (rows == null)
                return;

            List<WallCandidate> skipped = _review.Approve(rows.Select(r => r.Candidate));
            AfterChange(rows);
            if (skipped.Count > 0)
            {
                MessageBox.Show(
                    this,
                    skipped.Count.ToString(CultureInfo.InvariantCulture)
                        + " selected rows have no height and were not approved. Set a height first.",
                    "Bibimcad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void OnReject(object sender, RoutedEventArgs e)
        {
            List<WallReviewRow> rows = RequireSelection();
            if (rows == null)
                return;

            _review.Reject(rows.Select(r => r.Candidate));
            AfterChange(rows);
        }

        private void OnUnverify(object sender, RoutedEventArgs e)
        {
            List<WallReviewRow> rows = RequireSelection();
            if (rows == null)
                return;

            _review.MarkUnverified(rows.Select(r => r.Candidate));
            AfterChange(rows);
        }

        private void OnSetHeight(object sender, RoutedEventArgs e)
        {
            List<WallReviewRow> rows = RequireSelection();
            if (rows == null)
                return;

            double height;
            if (!double.TryParse(HeightInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out height)
                || !WallReviewService.IsValidHeight(height))
            {
                MessageBox.Show(this, "Enter a positive height in millimeters.", "Bibimcad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _review.SetHeight(rows.Select(r => r.Candidate), height);
            AfterChange(rows);
        }

        private void OnExportCsv(object sender, RoutedEventArgs e)
        {
            CommitGridEdits();
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "Export wall review CSV";
            dialog.Filter = "CSV files (*.csv)|*.csv";
            SetInitialPath(dialog);
            if (dialog.ShowDialog(this) != true)
                return;

            new WallReviewCsv().Export(_candidates, dialog.FileName);
            MessageBox.Show(
                this,
                "Saved " + _candidates.Count.ToString(CultureInfo.InvariantCulture) + " rows to " + dialog.FileName
                    + ".\n\nEdit approve (YES, NO, REJECT), wall_height_mm, and notes, then use Import CSV.",
                "Bibimcad",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OnImportCsv(object sender, RoutedEventArgs e)
        {
            CommitGridEdits();
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Import wall review CSV";
            dialog.Filter = "CSV files (*.csv)|*.csv";
            SetInitialPath(dialog);
            if (dialog.ShowDialog(this) != true)
                return;

            WallReviewCsvImport result;
            try
            {
                result = new WallReviewCsv().Import(dialog.FileName, _candidates, _review);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Bibimcad", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AfterChange(_rows);
            List<string> lines = new List<string>();
            lines.Add("Updated rows: " + result.UpdatedCount.ToString(CultureInfo.InvariantCulture));
            if (result.ApprovalsWithoutHeight.Count > 0)
                lines.Add("Approved without height, left unverified: " + result.ApprovalsWithoutHeight.Count.ToString(CultureInfo.InvariantCulture));
            if (result.UnknownIds.Count > 0)
                lines.Add("Ids not in the current candidates: " + result.UnknownIds.Count.ToString(CultureInfo.InvariantCulture));
            foreach (string error in result.Errors.Take(10))
                lines.Add(error);
            if (result.Errors.Count > 10)
                lines.Add("... and " + (result.Errors.Count - 10).ToString(CultureInfo.InvariantCulture) + " more errors.");
            lines.Add(string.Empty);
            lines.Add("Nothing is saved until you press Save.");
            MessageBox.Show(this, string.Join("\n", lines.ToArray()), "Bibimcad", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (!CommitGridEdits())
                return;

            List<WallCandidate> missing = WallReviewService.ApprovedWithoutHeight(_candidates);
            if (missing.Count > 0)
            {
                MessageBox.Show(
                    this,
                    missing.Count.ToString(CultureInfo.InvariantCulture)
                        + " approved rows have no height. Set a height or change their status before saving.",
                    "Bibimcad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private List<WallReviewRow> RequireSelection()
        {
            CommitGridEdits();
            List<WallReviewRow> rows = SelectedRows();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "Select one or more rows first.", "Bibimcad", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            return rows;
        }

        private void AfterChange(IEnumerable<WallReviewRow> rows)
        {
            foreach (WallReviewRow row in rows)
                row.Refresh();

            _view.Refresh();
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            int shown = _view.Cast<object>().Count();
            int approved = _candidates.Count(c => c.Status == WallCandidateStatus.Approved);
            int rejected = _candidates.Count(c => c.Status == WallCandidateStatus.Rejected);
            int unverified = _candidates.Count - approved - rejected;
            CountsText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Showing {0} of {1}    Approved {2}    Rejected {3}    Unverified {4}",
                shown,
                _candidates.Count,
                approved,
                rejected,
                unverified);
        }

        private void SetInitialPath(FileDialog dialog)
        {
            if (string.IsNullOrWhiteSpace(_csvPath))
                return;

            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(_csvPath);
            dialog.FileName = System.IO.Path.GetFileName(_csvPath);
        }
    }
}
