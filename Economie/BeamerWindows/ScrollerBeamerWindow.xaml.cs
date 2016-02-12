using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nobel.Economie.BeamerWindows {
    /// <summary>
    /// Interaction logic for ScrollerBeamerWindow.xaml
    /// </summary>
    public partial class ScrollerBeamerWindow : BeamerWindow {
		public long lastCount = 0;
		public ScrollerBeamerWindow()
		{
			InitializeComponent();
		}
        #region BeamerWindow Override Methods       
        public override void updateTimeslots(ref List<Timeslot> timeslots, ref Dictionary<string,int> pointsources, DateTime ecoStart, TimeSpan timeslotLen)
		{			
			int timeslot = 0;
			TimeSpan ts = DateTime.Now - ecoStart;
            TimeslotsStackPanel.Children.Clear();
            TimeslotsStackPanel.Children.Add(new Label() { Content = "Permanent", FontWeight = FontWeights.Bold });
            foreach (KeyValuePair<String, int> kvp in pointsources)
			{                   
                TimeslotsStackPanel.Children.Add(new Label() { Content = String.Format("{0} ({1})", kvp.Key.Trim('%').UppercaseFirst(), kvp.Value) });
            }
           
			if (ts > TimeSpan.Zero)
			{
				timeslot = (int)(ts.Ticks / timeslotLen.Ticks);
			}
			if (timeslot >= 0 && timeslot < timeslots.Count && ts > TimeSpan.Zero)
			{	
			    TimeslotsStackPanel.Children.Add(new Label() { Content = "Tijdelijk", FontWeight = FontWeights.Bold });
				foreach (KeyValuePair<String, int> kvp in timeslots[timeslot].Items)
				{                   
                    TimeslotsStackPanel.Children.Add(new Label() { Content = String.Format("{0} ({1})", kvp.Key.Trim('%').UppercaseFirst(), kvp.Value) });
                }
			}			
			if (ts <= TimeSpan.Zero)
			{
				timeslot = -1;
			}
            TimeSpan timeToNext = ecoStart.Add(timeslotLen.Multiply(timeslot + 1)).Subtract(DateTime.Now);
            string minleft = timeToNext.TotalMinutes < 0.5 ? "<1" : timeToNext.TotalMinutes.ToString("F0");
			if (timeslot+1 >= 0 && timeslot+1 < timeslots.Count)
			{
                TimeslotsStackPanel.Children.Add(new Label() { Content = String.Format("Straks (nog {0} min)", minleft), FontWeight = FontWeights.Bold });				
				foreach (KeyValuePair<String, int> kvp in timeslots[timeslot + 1].Items)
				{
                    TimeslotsStackPanel.Children.Add(new Label() { Content = String.Format("{0} ({1})", kvp.Key.Trim('%').UppercaseFirst(), kvp.Value) });
				}
			}
		}

		public override void updateDisplay(ref Dictionary<String, long> points)
		{
			upScroller.Children.Clear();
			if (points.Count > 0)
			{
				int i = 1;
				foreach (KeyValuePair<String, long> kvp in points)
				{
                    Grid grid = new Grid() { Width = canUp.ActualWidth};
                    Label tbPos = new Label()
                    {
                        Content = i.ToString(),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                        HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right,
                        Width = 50,
                        Style = (Style)(this.Resources["BeamerText"])
                    };
                    Label tbPoints = new Label()
                    {
                        Content = kvp.Value.ToString(),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right,
                        Width = 75,
                        Style = (Style)(this.Resources["BeamerText"])
                    };
                    String name = kvp.Key;
                    if (name.Contains(","))
                    {
                        string [] name_strs = name.Split(new char[] { ',' }, 2);
                        Array.Reverse(name_strs);
                        name = String.Join(" ", name_strs);
                    }
                    Label tbName = new Label()
                    {
                        Content = name.Trim(),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                        Margin = new Thickness(tbPos.Width+5, 0, tbPoints.Width+5, 0),
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(2,0,2,0),
                        Padding = new Thickness(5,0,5,0),
                        Style = (Style)(this.Resources["BeamerText"])
                    };                    
                    grid.Children.Add(tbPos);
                    grid.Children.Add(tbName);
                    grid.Children.Add(tbPoints);
                    upScroller.Children.Add(grid);
					i++;
				}
				upScroller.UpdateLayout();				
			}
			else
			{
				TextBlock tb = new TextBlock();
				tb.Text = "Er is nog geen data.";
				upScroller.Children.Add(tb);
			}
            if (lastCount != points.Count)
            {
                checkAnimation();
            }
			lastCount = points.Count;
		}
        #endregion
        #region Event Handlers
        private void Window_Loaded(object sender, RoutedEventArgs e)
		{
            checkAnimation();		
		}

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
            checkAnimation();
		}
        #endregion
        #region Animation Methods
        public void updateAnimation() {
            double from = 0.0;
            double to = Math.Min(0, canUp.ActualHeight - upScroller.ActualHeight);
            DoubleAnimationUsingKeyFrames DA = new DoubleAnimationUsingKeyFrames();
            DA.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromPercent(0)));
            DA.KeyFrames.Add(new LinearDoubleKeyFrame(to, KeyTime.FromPercent(.37)));
            DA.KeyFrames.Add(new LinearDoubleKeyFrame(to, KeyTime.FromPercent(.43)));
            DA.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromPercent(.94)));
            DA.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromPercent(1)));
            DA.RepeatBehavior = RepeatBehavior.Forever;
            DA.Duration = new Duration(TimeSpan.FromSeconds(Math.Abs(to - from) / 15));
            upScroller.BeginAnimation(Canvas.TopProperty, DA);
        }
        public void stopAnimation() {
            upScroller.BeginAnimation(Canvas.TopProperty, null);
        }
        private void checkAnimation()
        {
            canUp.UpdateLayout();
            upScroller.UpdateLayout();
            if (upScroller.ActualHeight > canUp.ActualHeight)
            {
                updateAnimation();
            }
            else
            {
                stopAnimation();
            }
        }
        #endregion
    }
}
