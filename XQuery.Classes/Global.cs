using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XQuery.Classes
{
	public enum QueryType { Insert, Delete, Drop, Select, Create, Import };
	
	public class InvalidQueryException : Exception
	{
		public InvalidQueryException() { }
		public InvalidQueryException(string message) : base(message) { }
	}


}

	
