using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XQuery.Classes
{
	public class Expression
	{
		public string Left;
		public string Operator;
		public string Right;

		public Expression(string expressionString)
		{
			//foreach (string op in SyntaxProvider.Operators)
			//{
			//    expressionString = expressionString.Replace(op, " " + op + " "); // Fix any operand=value to operand = value to correctly parse.
			//}
			//expressionString = expressionString.Replace("!", " ! "); // Ugh, anomaly with Replace().
			//List<string> list = expressionString.Split(SyntaxProvider.Separators.ToArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
			//list = Query.ReformStrings(list); // Consolidate words surrounded with [ ] into one word.


			List<string> list = expressionString.Split(SyntaxProvider.Operators.Select(e => " " + e + " ").ToArray(), StringSplitOptions.RemoveEmptyEntries).ToList();

			//// Combine operators:  > = into >=, etc.
			//while (list.Count > 3)
			//{
			//    list[1] += list[2];
			//    list.RemoveAt(2);
			//}

			if (list.Count != 3)
				throw new InvalidQueryException("Expressions should have exactly 3 arguments:  " + expressionString);
			if (!SyntaxProvider.Operators.Contains(list[1].ToLower()))
				throw new InvalidQueryException("Expression contains a disallowed operator.\r\nAllowed operators: " + string.Join(" ", SyntaxProvider.Operators.ToArray()));
			Left = list[0];
			Operator = list[1];
			Right = list[2];
		}
	}
}
