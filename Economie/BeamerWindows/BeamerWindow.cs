﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

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
        #region Event Handlers
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            e.Cancel = !this.AllowClosing;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.F11) {
                if (WindowState != System.Windows.WindowState.Maximized) {
                    WindowStyle = System.Windows.WindowStyle.None;
					ResizeMode = System.Windows.ResizeMode.NoResize;
                    WindowState = System.Windows.WindowState.Maximized;
                } else {
                    WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
					ResizeMode = System.Windows.ResizeMode.CanResize;
                    WindowState = System.Windows.WindowState.Normal;                    
                }
            }
        }
        #endregion

        public abstract void updateDisplay(ref Dictionary<string, long> points);
		public abstract void updateTimeslots(ref List<Timeslot> timeslots, ref Dictionary<string, int> pointsources, DateTime ecoStart, TimeSpan timeslotLen, double pointMP = 1);

    }
}