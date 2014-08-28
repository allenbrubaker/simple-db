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

namespace XQuery.GUI
{
	/// <summary>
	/// Interaction logic for LogWindow.xaml
	/// </summary>
	public partial class LogWindow : Window
	{
		public LogWindow()
		{
			InitializeComponent();
			Refresh();
		}

		public void Refresh()
		{
			tbLog.Text = Log.Text;
			tbLog.ScrollToEnd(); 
		}
	}
}
