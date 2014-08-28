using System;
using System.Collections.Generic;
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
	/// Interaction logic for ResultsWindow.xaml
	/// </summary>
	public partial class ResultsWindow : Window
	{
		public ResultsWindow()
		{
			this.InitializeComponent();
		}

		public ResultsWindow(string title)
		{
			this.InitializeComponent();
			this.Title = title;
		}
	}
}