using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace XQuery.Classes
{
	public static class Log
	{
		public static string Text
		{
			get 
			{ 
				if (!File.Exists("Data/Log.txt")) 
					return string.Empty; 
				else 
					using (StreamReader reader = new StreamReader("Data/Log.txt")) 
						return reader.ReadToEnd(); 
			}
		}

		static Log()
		{
			if (!Directory.Exists("Data"))
				Directory.CreateDirectory("Data");
		}

		public static void Add(string tableName, string entry)
		{
			using (StreamWriter logFile = new StreamWriter("Data/Log.txt", true))
			{
				logFile.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]\t" + tableName + ": " + entry);
				logFile.Flush();
			}
		}
	
		
	}


}
