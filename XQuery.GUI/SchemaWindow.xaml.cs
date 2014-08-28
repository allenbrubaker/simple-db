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
using System.Windows.Shapes;
using XQuery.Classes;
using System.Data;


namespace XQuery.GUI
{
	/// <summary>
	/// Interaction logic for SchemaWindow.xaml
	/// </summary>
	public partial class SchemaWindow : Window
	{
		QueryWindow QueryWindow;
		DateTime timer = DateTime.Now;
		public SchemaWindow(QueryWindow window = null)
		{
			InitializeComponent();
			QueryWindow = window;
			Refresh();
		}

		public void Refresh()
		{
			treeSchema.Items.Clear();
			foreach (DataTable table in DataHandler.DataSet.Tables)
			{
				TreeViewItem item = new TreeViewItem();
				item.Header = table.TableName + " (" + table.Rows.Count + ")";
				item.Tag = table.TableName;
				item.MouseRightButtonUp += treeitem_MouseRightButtonUp;
				foreach (DataColumn column in table.Columns)
				{
					TreeViewItem childItem = new TreeViewItem();
					childItem.Header = column.ColumnName;
					childItem.Tag = table.TableName + "." + column.ColumnName;
					childItem.MouseRightButtonUp += treeitem_MouseRightButtonUp;
					item.Items.Add(childItem);
				}
				treeSchema.Items.Add(item);
			}
		}

		private void treeitem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
		{

			if (DateTime.Now.Subtract(timer).Milliseconds <= 100)
				return;
			else
				timer = DateTime.Now;

			TreeViewItem item = sender as TreeViewItem;
			//item.IsSelected = true;
			if (QueryWindow != null && item != null)
			{
				string save = Clipboard.GetText();
				Clipboard.SetText(item.Tag + " ");
				QueryWindow.tbQuery.Focus();
				QueryWindow.tbQuery.Paste();
				Clipboard.SetText(save);	
			}
		}
	}
}
