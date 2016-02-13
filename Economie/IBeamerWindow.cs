using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Nobel.Economie {
    interface IBeamerWindow {
        bool AllowClosing {
            get; set;
        }
        void updateDisplay(ref Dictionary<String, long> points);
		void updateTimeslots(ref List<Timeslot> timeslots, ref Dictionary<string, int> pointsources, DateTime ecoStart, TimeSpan timeslotLen, double pointMP = 1);
    }
}
