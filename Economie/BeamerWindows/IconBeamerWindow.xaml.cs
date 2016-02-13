using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Nobel.Economie.BeamerWindows
{
	/// <summary>
	/// Interaction logic for IconBeamerWindow.xaml
	/// </summary>
	public partial class IconBeamerWindow : BeamerWindow
	{

		//A list of tracked groups.
		Dictionary<string, GroupIcon> groups = new Dictionary<string, GroupIcon>();

		public IconBeamerWindow()
		{
			InitializeComponent();

            //Start fullscreen
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = System.Windows.WindowStyle.None;
            ResizeMode = System.Windows.ResizeMode.NoResize;
            WindowState = System.Windows.WindowState.Maximized;

            //Get all resources from GroupIcons.resx and add the Icon to the UI.
            ResourceSet resourceSet = GroupIcons.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
			ImageSourceConverter c = new ImageSourceConverter();

			foreach (DictionaryEntry entry in resourceSet)
			{
				string resourceKey = entry.Key.ToString();
				System.Drawing.Bitmap resource = entry.Value as System.Drawing.Bitmap;
				GroupIcon gi = new GroupIcon(loadBitmap(resource));
				groups.Add(resourceKey.ToLower(), gi);
			}

			foreach (KeyValuePair<string, GroupIcon> group in groups.OrderBy(group => group.Key))
			{
				wrapPanel.Children.Add(group.Value.Wrapper);
			}
		}

		#region BeamerWindow Override Methods
		public override void updateDisplay(ref Dictionary<string, long> points)
		{
			List<string> updatedGroups = new List<string>();
			if (points.Count > 0)
			{

				foreach (KeyValuePair<String, long> kvp in points)
				{
					string resourceKey = kvp.Key.ToLower().Replace(' ', '_');
					if (groups.ContainsKey(resourceKey))
					{
						groups[resourceKey].SetPoints(kvp.Value);
						if (kvp.Value > 0)
						{
							groups[resourceKey].Activate();
						}
						else
						{
							groups[resourceKey].Deactivate();
						}
						updatedGroups.Add(resourceKey);
					}
				}
				foreach (KeyValuePair<String, GroupIcon> kvp in groups)
				{
					if (!updatedGroups.Contains(kvp.Key))
					{
						kvp.Value.Deactivate();
					}
				}
				wrapPanel.Children.Clear();
				foreach (KeyValuePair<string, GroupIcon> group in groups.OrderByDescending(group => group.Value.Points))
				{
					wrapPanel.Children.Add(group.Value.Wrapper);
				}
			}
		}

		public override void updateTimeslots(ref List<Timeslot> timeslots, ref Dictionary<string, int> pointsources, DateTime ecoStart, TimeSpan timeslotLen, double pointMP = 1)
		{
			int timeslot = 0;
			TimeSpan ts = DateTime.Now - ecoStart;
			TimeslotsStackPanel.Children.Clear();
			TimeslotsStackPanel.Children.Add(new Label() { Content = String.Format("Permanent (x{0:F1})", pointMP), FontWeight = FontWeights.Bold });
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
				TimeslotsStackPanel.Children.Add(new Label() { Content = "Bonus", FontWeight = FontWeights.Bold });
				foreach (KeyValuePair<String, int> kvp in timeslots[timeslot].Items)
				{
					TimeslotsStackPanel.Children.Add(new Label() { Content = String.Format("{0} (+{1})", kvp.Key.Trim('%').UppercaseFirst(), kvp.Value) });
				}
			}
			if (ts <= TimeSpan.Zero)
			{
				timeslot = -1;
			}
			TimeSpan timeToNext = ecoStart.Add(timeslotLen.Multiply(timeslot + 1)).Subtract(DateTime.Now);
			string minleft = timeToNext.TotalMinutes < 0.5 ? "<1" : timeToNext.TotalMinutes.ToString("F0");
			if (timeslot + 1 >= 0 && timeslot + 1 < timeslots.Count)
			{
				TimeslotsStackPanel.Children.Add(new Label() { Content = String.Format("Straks (nog {0} min)", minleft), FontWeight = FontWeights.Bold });
				foreach (KeyValuePair<String, int> kvp in timeslots[timeslot + 1].Items)
				{
					TimeslotsStackPanel.Children.Add(new Label() { Content = String.Format("{0} (+{1})", kvp.Key.Trim('%').UppercaseFirst(), kvp.Value) });
				}
			}
		}
		#endregion

		#region External Methods
		[DllImport("gdi32")]
		static extern int DeleteObject(IntPtr o);
		#endregion
		#region Private Helper Methods
		BitmapSource loadBitmap(System.Drawing.Bitmap source)
		{
			IntPtr ip = source.GetHbitmap();
			BitmapSource bs = null;
			try
			{
				bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(ip,
				   IntPtr.Zero, Int32Rect.Empty,
				   System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
			}
			finally
			{
				DeleteObject(ip);
			}

			return bs;
		}
		#endregion

		#region Subclasses
		class GroupIcon
		{
			public Label Label;
			public Image Image;
			public Grid Wrapper;
			public long Points;
			public GroupIcon(ImageSource imageSource, long points = 0)
			{
				Wrapper = new Grid();
				Label = new Label();
				Image = new Image();
				Image.Source = imageSource;
				Wrapper.Children.Add(Image);
				Wrapper.Children.Add(Label);
				SetPoints(points);
				Deactivate();
			}

			public void Activate()
			{
				Label.Opacity = 1;
				Image.Opacity = 1;
			}

			public void Deactivate()
			{
				Label.Opacity = 0.2;
				Image.Opacity = 0.2;
			}
			public void SetPoints(long points)
			{
                //Zetten 
				Points = points;
                if(Points == 1)
                    Label.Content = String.Format("{0} punt", Points);
                else
				    Label.Content = String.Format("{0} punten", Points);
			}

		}
		#endregion
	}


}
