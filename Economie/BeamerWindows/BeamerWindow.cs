using System;
using System.Collections.Generic;
using System.Windows;

namespace Nobel.Economie.BeamerWindows {
    public abstract class BeamerWindow : Window, IBeamerWindow {
        bool allowClosing = false;

        public bool AllowClosing {
            get {
                return allowClosing;
            }
            set {
                if (allowClosing != value)
                    allowClosing = value;
            }
        }


        public abstract void updateDisplay(ref Dictionary<string, long> points);
        public abstract void updateTimeslots(ref List<Timeslot> timeslots, ref Dictionary<string, int> pointsources, DateTime ecoStart, TimeSpan timeslotLen);
    }
}
