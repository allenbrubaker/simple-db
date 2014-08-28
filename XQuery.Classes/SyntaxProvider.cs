using System;
using System.Collections.Generic;
using System.Text;

namespace XQuery.Classes
{
	public static class SyntaxProvider
	{
		public static List<string> SupportedTypes = new List<string>() { "string", "byte", "short", "int", "long", "float", "datetime", "double", "boolean", "bool", "char", "decimal", "timespan" };
		public static List<string> Commands = new List<string>() { "select", "from", "where", "orderby", "asc", "desc", "ascending", "descending", "insert", "delete", "drop", "create", "values", "on", "set", "import" };
		public static List<char> Separators = new List<char>() { ' ', ',', ')', '(', ';', '\n', '\t', '\r' };
		public static List<string> Operators = new List<string>() { "=", "!=", ">", ">=", "<", "<=" };
		public static List<string> StringOperators = new List<string>() { "startswith", "endswith", "contains", "!startswith", "!endswith", "!contains" };
		public static List<string> Aggregates = new List<string>() { "count", "sum", "max", "min", "avg" };
		public static List<string> Tags = new List<string>();

		static SyntaxProvider()
		{
			Tags.AddRange(SupportedTypes);
			Tags.AddRange(Commands);
			Operators.AddRange(StringOperators);
		}
		public static bool IsKnownTag(string tag)
        {
            return Tags.Exists(delegate(string s) { return s.ToLower().Equals(tag.ToLower()); });
        }
        public static List<string> TagCandidates(string tag)
        {
            return Tags.FindAll(delegate(string s) { return s.ToLower().StartsWith(tag.ToLower()); });
        }
			
		
	}
}