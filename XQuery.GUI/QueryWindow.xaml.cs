using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XQuery.Classes;
using System.Data;
using System.ComponentModel;
using System.IO;

namespace XQuery.GUI
{
	/// <summary>
	/// Interaction logic for QueryWindow.xaml
	/// </summary>
	public partial class QueryWindow : Window
	{
		List<ResultsWindow> ChildWindows = new List<ResultsWindow>();
		SchemaWindow SchemaWindow = new SchemaWindow();
		LogWindow LogWindow = new LogWindow();
		
		public QueryWindow()
		{
			InitializeComponent();
			tbQuery.Focus(); 
		}

		private void btnClear_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			tbQuery.Document.Blocks.Clear();
			tbQuery.Document.LineHeight = .5; // Reset the lineheight because it gets cleared.
			tbQuery.Focus();
		}

		private void btnQuery_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			string queryString;
			if (!tbQuery.Selection.IsEmpty)
				queryString = tbQuery.Selection.Text;
			else
				queryString = new TextRange(tbQuery.Document.ContentStart, tbQuery.Document.ContentEnd).Text;

			List<Query> queries = null;
			try
			{
				queries = Query.Parse(queryString);

				foreach (Window child in ChildWindows)
					child.Close();
				ChildWindows.Clear();

				foreach (Query query in queries)
				{
					DataTable table = query.DoQuery();

					// Display results for Select queries only.
					if (query.Type == QueryType.Select)
					{
						foreach (DataColumn column in table.Columns)
							column.ColumnName = column.ColumnName.Replace('.', '^'); // DataGrid croaks if a columnname contains a '.'

						ResultsWindow child = new ResultsWindow(query.QueryString);
						child.gridResult.ItemsSource = table.DefaultView;
						ChildWindows.Add(child);
						child.Show();
					}
				}

				SchemaWindow.Refresh();
				LogWindow.Refresh();
			}
			catch (Exception error)
			{
			    MessageBox.Show(error.Message, "Error");
			    tbQuery.Focus();
			}
		}

		private void btnSchema_Click(object sender, RoutedEventArgs e)
		{
			SchemaWindow.Close();
			SchemaWindow = new SchemaWindow(this);
			SchemaWindow.Show();
		}

		private void btnLog_Click(object sender, RoutedEventArgs e)
		{
			LogWindow.Close();
			LogWindow = new LogWindow();
			LogWindow.Show();
			LogWindow.tbLog.ScrollToEnd();
		}

		private void btnHelp_Click(object sender, RoutedEventArgs e)
		{
			if (File.Exists("XQuery.chm"))
				System.Diagnostics.Process.Start("XQuery.chm");
			else
				MessageBox.Show("Create XQuery.chm and place in application directory.  Use html files located in the help folder to facilitate.", "Error");
		}

	}
}
