using MySql.Data.MySqlClient;
using Nobel.Economie.BeamerWindows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Nobel.Economie
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		BackgroundWorker updater = new BackgroundWorker();
		MySqlConnection connection;
        //ScrollerBeamerWindow bw = new ScrollerBeamerWindow();
        BeamerWindow bw;
        Dictionary<string, long> points = new Dictionary<string, long>();
		List<Timeslot> timeslots = new List<Timeslot>();
        Dictionary<string, int> pointSources = new Dictionary<string, int>();
		FileInfo timeslotsPath = new FileInfo("Data/timeslots.csv");
        FileInfo pointsourcesPath = new FileInfo("Data/pointsources.csv");
        
		Timer timerDatabase = new Timer(30000);
		Action updateDB;
		
        DateTime ecoStart;
        DateTime ecoEnd;
		TimeSpan timeslotLen = new TimeSpan(0, 20, 0);
		int brackets = 1;
		double bracketMP = 1;
        const double maxMP = 5;
		TimeSpan BracketLen;
		object _updaterLock = new object();
		int totalQueries = 0;
        

        Stopwatch performanceStopwatch = new Stopwatch();

		StringBuilder getQuery = new StringBuilder();
		StringBuilder getQueryTS = new StringBuilder();
		String getQueryStart = @"SELECT 
			SUM(bestelling.Bestelling_AantalS) as Totaal_Aantal, debiteur.Debiteur_Naam, prijs.Prijs_Naam";

			/*CASE
			 WHEN Bestelling_Time<?timebracket1 THEN '0'
			 WHEN Bestelling_Time>=?timebracket1 AND Bestelling_Time<?timebracket2 THEN '1'
			 WHEN Bestelling_Time>=?timebracket2 AND Bestelling_Time<?timebracket3 THEN '2'
			 ELSE '3'
			END AS Bracket*/
		String getQueryEnd = @"FROM
			bestelling
				LEFT JOIN
			(bon
			LEFT JOIN (debiteur) ON (Bon_Debiteur = Debiteur_ID)) ON (Bestelling_Bon = Bon_ID)
		  LEFT JOIN
			(prijs) ON (Bestelling_Wat = Prijs_ID)
		WHERE Prijs_Naam LIKE ?product AND Bestelling_Time>=?timestart AND Bestelling_Time<=?timeend GROUP BY Debiteur_ID";
		MySqlCommand getCmd;
		MySqlCommand getCmdTS;
		public MainWindow()
		{
			InitializeComponent();
            Window w = (Window)this;
#region Loading Data
            if (timeslotsPath.Exists)
			{
				using (StreamReader r = timeslotsPath.OpenText())
				{
					timeslots.Clear();
					timeSlotsListBox.Items.Clear();
					while (!r.EndOfStream)
					{
						String[] strings = r.ReadLine().Split(';');
						if (strings.Length % 2 != 0)
						{
							MessageBox.Show("Error in timeslots file. Format is 1 per line points per unit;unit;points per unit;unit.");
						}
						else
						{
							Dictionary<String, int> Items = new Dictionary<string, int>();
							StringBuilder sb = new StringBuilder();
							for (int i = 0; i < strings.Length; i += 2)
							{
								if (strings[i] == "" || strings[i + 1] == "")
								{
									MessageBox.Show("Error in timeslots file. Something is emtpy.");
								}
								else
								{
									int pointsperunit = Int32.Parse(strings[i]);
									String unit = strings[i + 1].Trim('"');
									Items.Add(unit, pointsperunit);
									totalQueries++;
									sb.AppendFormat("{1} ({0}), ", pointsperunit, unit);
								}
							}

							timeslots.Add(new Timeslot() { Items = Items });

							timeSlotsListBox.Items.Add(new ListBoxItem() { Content = sb.ToString() });
							sb = null;
						}
					}
				}
			}
			else
			{
				MessageBox.Show("No timeslots.csv found.");
			}
            if (pointsourcesPath.Exists)
            {
                using (StreamReader r = pointsourcesPath.OpenText())
                {
                    pointSources.Clear();
                    pointSourcesListBox.Items.Clear();
                    while (!r.EndOfStream)
                    {
                        String[] strings = r.ReadLine().Split(';');
                        if (strings.Length != 2)
                        {
                            MessageBox.Show("Error in pointsources file. Format is 1 per line points per unit;unit.");
                        }
                        else
                        {
                            if (strings[0] == "" || strings[1] == "")
                            {
                                MessageBox.Show("Error in timeslots file. Something is emtpy.");
                            }
                            else
                            {
                                int pointsperunit = Int32.Parse(strings[0]);
                                String unit = strings[1].Trim('"');
                                if (!pointSources.ContainsKey(unit))
                                {
                                    pointSources.Add(unit, pointsperunit);
                                }
                                totalQueries++;
                                pointSourcesListBox.Items.Add(new ListBoxItem() { Content = String.Format("{1} ({0})", pointsperunit, unit) });
                            }

                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("No pointsources.csv found.");
            }
#endregion
			updateDB = updateDatabase;
			updater.DoWork += updater_DoWork;
			updater.ProgressChanged += updater_ProgressChanged;
			updater.RunWorkerCompleted += updater_RunWorkerCompleted;
			updater.WorkerReportsProgress = true;
            
            timerDatabase.Elapsed += timerDatabase_Elapsed;
		}

		void updater_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
            updaterProgress.Value = 0;
            performanceStopwatch.Stop();
            //Outputs duration of the update, in Fixed point with two decimals
            writeOutputText(String.Format("Database update completed in {0:F2} seconds.", performanceStopwatch.Elapsed.TotalSeconds));
            lock (_updaterLock)
			{
                bw.updateDisplay(ref points);				
			}					
		}

		void updater_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			updaterProgress.Value = e.ProgressPercentage;
		}

		void updater_DoWork(object sender, DoWorkEventArgs e)
		{
			lock (_updaterLock)
			{
				int current = 0;
				int tsn = 0;
				int bracket = GetCurrentBracket();
                points.Clear();
                //stuff that gets you points the whole evening.
				
				foreach (KeyValuePair<String, int> kvp in pointSources)
                {
                    updater.ReportProgress((int)Math.Round(current / (double)totalQueries * 100.0));
                    try
                    {
                        getCmd.Parameters["product"].Value = kvp.Key;                        
                        using (MySqlDataReader dataReader = getCmd.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
								updateScores(dataReader, kvp);
                            }
                        }
                    }
                    catch (MySql.Data.MySqlClient.MySqlException ex)
                    {
                        MessageBox.Show("Query error " + ex.Number + " has occurred: " + ex.Message,
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                    current++;
                }
                //stuff that gets you points per timeslot.              
				foreach (Timeslot ts in timeslots)
				{
					DateTime start = ecoStart.Add(timeslotLen.Multiply(tsn));
					DateTime end = ecoStart.Add(timeslotLen.Multiply(tsn + 1));
                    if (start > DateTime.Now || start > ecoEnd)
					{
                        continue;
					}
					getCmdTS.Parameters["timestart"].Value = start.ToUnixTimestamp() * 1000;
					getCmdTS.Parameters["timeend"].Value = end.ToUnixTimestamp() * 1000;
                    foreach (KeyValuePair<String, int> kvp in ts.Items)
                    {
                        updater.ReportProgress((int)Math.Round(current / (double)totalQueries * 100.0));
                        try
                        {
							getCmdTS.Parameters["product"].Value = kvp.Key;
							using (MySqlDataReader dataReader = getCmdTS.ExecuteReader())
                            {
                                while (dataReader.Read())
                                {
                                    updateScores(dataReader, kvp);
                                }
                            }
                        }
                        catch (MySql.Data.MySqlClient.MySqlException ex)
                        {
                            MessageBox.Show("Query error " + ex.Number + " has occurred: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                        }
                        current++;
                    }
					tsn++;
				}
                var pointsSorted = from entry in points orderby entry.Value descending select entry;
                points = pointsSorted.ToDictionary(pair => pair.Key, pair => pair.Value);

			}
		}

		void updateScores(MySqlDataReader dataReader, KeyValuePair<String, int> kvp)
        {
            //Uitlezen Debiteur/Product/Aantal uit database
            String deb = dataReader["Debiteur_Naam"].ToString();
            String item = dataReader["Prijs_Naam"].ToString();
			int bracket = Int32.Parse(dataReader["Bracket"].ToString());
            int aantal = Int32.Parse(dataReader["Totaal_Aantal"].ToString());
            //Fles is 20 maal aantal punten
            if (item.Contains("fles", StringComparison.OrdinalIgnoreCase))
            {
                aantal *= 20;
            }
            if (points.ContainsKey(deb))
            {
                //Debiteur bestaat al: punten toevoegen
				points[deb] += (long)Math.Round(kvp.Value * aantal * GetBracketMultiplier(bracket));
            }

            else
            {
                //Debiteur bestaat nog niet: Toevoegen, plus punten
				points.Add(deb, (long)Math.Round(kvp.Value * aantal * GetBracketMultiplier(bracket)));
            }
        }

		void timerDatabase_Elapsed(object sender, ElapsedEventArgs e)
		{
			g.Dispatcher.BeginInvoke(DispatcherPriority.Send, updateDB);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
            if (bw != null) {
                bw.AllowClosing = true;
                bw.Close();
            }
            if (connection != null)
			{
				connection.Close();
			}
		}
        private void mysqlConnectButton_Click(object sender, RoutedEventArgs e)
        {
            MySqlConnectionStringBuilder csb = new MySqlConnectionStringBuilder();

            if (String.IsNullOrEmpty(serverTextBox.Text))
            {
                MessageBox.Show("Enter server.");
                return;
            }
            if (String.IsNullOrEmpty(userTextBox.Text))
            {
                MessageBox.Show("Enter username.");
                return;
            }
            if (String.IsNullOrEmpty(passwordBox.Password))
            {
                MessageBox.Show("Enter password.");
                return;
            }
            if (String.IsNullOrEmpty(databaseTextBox.Text))
            {
                MessageBox.Show("Enter password.");
                return;
            }

			if (datePicker.SelectedDate == null)
			{
				MessageBox.Show("Select date.");
				return;
			}
			int duration = 0;
			if (!Int32.TryParse(durationTextBox.Text, out duration))
			{
				MessageBox.Show("Enter duration in full hours.");
				return;
			}

			DateTime date = (DateTime)datePicker.SelectedDate;
            //De borrel begint hardcoded om 17:00
			ecoStart = new DateTime(date.Year, date.Month, date.Day, 17, 0, 0);
			ecoEnd = ecoStart + new TimeSpan(duration,0,0);

			writeOutputText(String.Format("Statistics start on {0} and end on {1}.", ecoStart, ecoEnd));

			writeOutputText(String.Format("Connecting...", ecoStart, ecoEnd));

            csb.Server = serverTextBox.Text;
            csb.Database = databaseTextBox.Text;
            csb.UserID = userTextBox.Text;
            csb.Password = passwordBox.Password;
            try
            {
                connection = new MySqlConnection(csb.ToString());
                connection.StateChange += connection_StateChange;
                connection.Open();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    MessageBox.Show("Not connected");
                }
                else
                {
					writeOutputText(String.Format("Connected.", ecoStart, ecoEnd));
                    mysqlConnectButton.IsEnabled = false;
                    forceUpdateButton.IsEnabled = true;
                    bracketMPTextBox.IsEnabled = false;
                    bracketsTextBox.IsEnabled = false;
                    timerDatabase.Start();
					getQuery.AppendLine(getQueryStart);
					getQueryTS.AppendLine(getQueryStart);

					if (!Double.TryParse(bracketMPTextBox.Text, out bracketMP))
					{
						bracketMP = 1;
						writeOutputText("Brackets multiplier invalid, enter valid Double.");
					}

					if(!Int32.TryParse(bracketsTextBox.Text, out brackets)){
						brackets = 1;
						writeOutputText("Number of brackets invalid, enter valid Int32.");
					}
					if(brackets > duration * 3){
						brackets = duration * 3;
						writeOutputText("Number of brackets too high, max is three per hour (each timeslot).");
					}
					if (brackets < 2)
					{
						getQuery.AppendLine(@", 0 as Bracket");
					} else {
						
						getQuery.AppendLine(@", CASE");
						getQuery.AppendLine(@"WHEN Bestelling_Time<?timebracket1 THEN 0");
						for (int i = 2; i < brackets; i++)
						{
							getQuery.AppendLine(String.Format(@"WHEN Bestelling_Time>=?timebracket{0} AND Bestelling_Time<?timebracket{1} THEN {0}",i-1,i));
						}
						getQuery.AppendLine(String.Format(@"ELSE '{0}'", brackets - 1));
						getQuery.AppendLine(@"END AS Bracket");						
					}
					getQuery.Append(getQueryEnd);
					getQueryTS.AppendLine(@", 0 as Bracket");
					getQueryTS.Append(getQueryEnd);
					getQuery.Append(@", Bracket");
                    getCmd = new MySqlCommand(getQuery.ToString(), connection);
					getCmdTS = new MySqlCommand(getQueryTS.ToString(), connection);
                    getCmd.Prepare();
					getCmdTS.Prepare();
                    getCmd.Parameters.AddWithValue("product", String.Empty);
					getCmdTS.Parameters.AddWithValue("product", String.Empty);
					
					getCmd.Parameters.AddWithValue("timestart", ecoStart.ToUnixTimestamp() * 1000);
					getCmdTS.Parameters.AddWithValue("timestart",0L);
					getCmd.Parameters.AddWithValue("timeend", ecoEnd.ToUnixTimestamp() * 1000);
					getCmdTS.Parameters.AddWithValue("timeend", 0L);

					BracketLen = TimeSpan.FromHours(duration / (double)brackets);
					if (brackets > 1)
					{
						for (int i = 1; i < brackets; i++)
						{
							DateTime BracketBoundary = ecoStart.Add(BracketLen.Multiply(i));
							getCmd.Parameters.AddWithValue(String.Format("timebracket{0}", i), BracketBoundary.ToUnixTimestamp() * 1000);
						}
					}
                    updateDatabase();
                }

            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                MessageBox.Show("Connection/Preparation error " + ex.Number + " has occurred: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        void connection_StateChange(object sender, System.Data.StateChangeEventArgs e)
        {
            writeOutputText(String.Format("Database connection state change: {1} -> {0}.", e.CurrentState, e.OriginalState));
        }
        void writeOutputText(String text)
        {
            outputTextBox.AppendText(String.Format("{0:HH:mm:ss} -> {1}.", DateTime.Now, text));
            outputTextBox.AppendText(Environment.NewLine);
            outputTextBox.ScrollToEnd();
        }
		void updateDatabase()
		{
			if (!updater.IsBusy && connection.State == System.Data.ConnectionState.Open)
			{
                performanceStopwatch.Reset();
                performanceStopwatch.Start();
                writeOutputText("Database update started.");
                updater.RunWorkerAsync();
			}
			int bracket = GetCurrentBracket();
			double pointMP = GetBracketMultiplier(bracket);
			bw.updateTimeslots(ref timeslots, ref pointSources, ecoStart, timeslotLen, pointMP);
            
		}
		public double GetBracketMultiplier(int bracket){

            double result = Math.Pow(bracketMP, bracket);
            if (result < maxMP)
                return result;
            else
                return maxMP;		
		}

		public int GetCurrentBracket()
		{
			int bracket = 0;
			TimeSpan br = DateTime.Now - ecoStart;
			if (br > TimeSpan.Zero)
			{
				bracket = (int)(br.Ticks / BracketLen.Ticks);
			}
			if (bracket < 0)
			{
				bracket = 0;
			}
			if (bracket >= brackets)
			{
				bracket = brackets - 1;
			}
			return bracket;
		}

        private void forceUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            updateDatabase();
        }

        private void ShowBeamerWindowButton_Click(object sender, RoutedEventArgs e) {
            if (BeamerWindowComboBox.SelectedItem != null) {
                if (((ComboBoxItem)BeamerWindowComboBox.SelectedItem).Content.ToString() == "IconBeamerWindow")
                    bw = new IconBeamerWindow();
                else if (((ComboBoxItem)BeamerWindowComboBox.SelectedItem).Content.ToString() == "ScrollerBeamerWindow")
                    bw = new ScrollerBeamerWindow();
                else {
                    writeOutputText("Unknown Beamer Window Type.");
                    return;
                }
                //Handle UI changes
                BeamerWindowComboBox.IsEnabled = false;
                mysqlConnectButton.IsEnabled = true;
                ShowBeamerWindowButton.IsEnabled = false;
                //Show window
                bw.Show();
				bw.MaximizeToSecondaryMonitor();
            } else {
                writeOutputText("No Beamer Window Selected.");
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // RoutedEventArgs ev = new RoutedEventArgs(new RoutedEvent());
            if (e.Key == System.Windows.Input.Key.Enter)
                if (ShowBeamerWindowButton.IsEnabled)
                    ShowBeamerWindowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else if (mysqlConnectButton.IsEnabled)
                    mysqlConnectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else if (forceUpdateButton.IsEnabled)
                    forceUpdateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }
    public class Timeslot
	{
		public Dictionary<String,int> Items;
	}
    public static class DateTimeExtensions
    {
        public static long ToUnixTimestamp(this DateTime d)
        {
            var duration = d.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0); //Make GMT timestamps.

            return (long)duration.TotalSeconds;
        }
    }
    public static class TimeSpanExtension
    {
		/// <summary>
		/// Multiplies a timespan by an integer value
		/// </summary>
		public static TimeSpan Multiply(this TimeSpan multiplicand, int multiplier)
		{
			return TimeSpan.FromTicks(multiplicand.Ticks * multiplier);
		}

		/// <summary>
		/// Multiplies a timespan by a double value
		/// </summary>
		public static TimeSpan Multiply(this TimeSpan multiplicand, double multiplier)
		{
			return TimeSpan.FromTicks((long)(multiplicand.Ticks * multiplier));
		}
	}
	public static class StringExtension
	{
		public static string UppercaseFirst(this string s)
		{
			// Check for empty string.
			if (string.IsNullOrEmpty(s))
			{
				return string.Empty;
			}
			// Return char and concat substring.
			return char.ToUpper(s[0]) + s.Substring(1);
		}
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }
	}
}
