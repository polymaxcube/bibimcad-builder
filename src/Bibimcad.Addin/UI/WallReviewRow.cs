using System;
using System.ComponentModel;
using Bibimcad.Models;
using Bibimcad.Services;

namespace Bibimcad.UI
{
    // Grid row over an edited copy of a candidate. The stored candidates change only when the window is saved.
    public sealed class WallReviewRow : INotifyPropertyChanged
    {
        public WallReviewRow(WallCandidate candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException("candidate");

            Candidate = candidate;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public WallCandidate Candidate { get; private set; }

        public string Id
        {
            get { return Candidate.Id; }
        }

        public string Layer
        {
            get { return Candidate.Layer; }
        }

        public double LengthMm
        {
            get { return Candidate.LengthMm; }
        }

        public double ThicknessMm
        {
            get { return Candidate.ThicknessMm; }
        }

        public string Orientation
        {
            get { return Candidate.Orientation; }
        }

        public string Status
        {
            get { return Candidate.Status; }
        }

        public double? HeightMm
        {
            get { return Candidate.HeightMm; }
            set
            {
                if (value.HasValue && !WallReviewService.IsValidHeight(value))
                    throw new ArgumentException("Height must be a positive number of millimeters.");

                Candidate.HeightMm = value;
                OnPropertyChanged("HeightMm");
            }
        }

        public string Notes
        {
            get { return Candidate.Notes; }
            set
            {
                Candidate.Notes = value;
                OnPropertyChanged("Notes");
            }
        }

        public bool HasRevitWall
        {
            get { return Candidate.RevitElementId.HasValue; }
        }

        public void Refresh()
        {
            OnPropertyChanged(string.Empty);
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
