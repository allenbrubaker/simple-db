using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using XQuery.Classes;


namespace XQuery.GUI
{
    /// <summary>
    /// An extension of RichTextBox that allows only plain text input.
    /// This class auto-formats words in the document using a dictionary lookup.
    /// <remarks>
    /// One of the applications of such a class can be a code editor.
    /// Syntax highlight for keywords can be implemented using this approach.
    /// </remarks>
    /// </summary>
    public class MyRichTextBox : RichTextBox
    {
        // Ctor.
        public MyRichTextBox() : base()
        {
            this._words = new List<Word>();
			this.KeyUp += KeyUpEventHandler;
        }

        #region Public Properties

        #endregion

		private void KeyUpEventHandler(object source, KeyEventArgs e)
		{
			if (e.Key == Key.Space || e.Key == Key.V || e.Key == Key.Enter || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.Up)
				DoFormat();
		}

        private void DoFormat()
        {
            // Clear all formatting properties in the document.
            // This is necessary since a paste command could have inserted text inside or at boundaries of a keyword from dictionary.
            TextRange documentRange = new TextRange(this.Document.ContentStart, this.Document.ContentEnd);
            documentRange.ClearAllProperties();

            // Reparse the document to scan for matching words.
            TextPointer navigator = this.Document.ContentStart;
            while (navigator.CompareTo(this.Document.ContentEnd) < 0)
            {
                TextPointerContext context = navigator.GetPointerContext(LogicalDirection.Backward);
                if (context == TextPointerContext.ElementStart && navigator.Parent is Run)
                {
                    this.AddMatchingWordsInRun((Run)navigator.Parent);
                }
                navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
            }	
            // Format words found.
            this.FormatWords();
			
        }

		private Word MostRecentWord()
		{
			TextPointer navigator = this.Document.ContentEnd;
			Run lastRun = ((Run)navigator.Parent);
			string runText = lastRun.Text;
			int wordStartIndex = runText.Length;
			while (wordStartIndex >= 0)
			{
				if (Char.IsWhiteSpace(runText[wordStartIndex]) || SyntaxProvider.Separators.Contains(runText[wordStartIndex])) 
				{
					break;
				}
			}
			++wordStartIndex;
			
			// Check if the last word in the Run is a matching word.
            string lastWordInRun = runText.Substring(wordStartIndex, runText.Length - wordStartIndex);
            TextPointer wordStart = lastRun.ContentStart.GetPositionAtOffset(wordStartIndex, LogicalDirection.Forward);
            TextPointer wordEnd = lastRun.ContentStart.GetPositionAtOffset(runText.Length, LogicalDirection.Backward);
            return new Word(wordStart, wordEnd);
        }
			
   
		
        #region Private Methods

        /// <summary>
        /// Helper to apply formatting properties to matching words in the document.
        /// </summary>
        private void FormatWords()
        {
            // Applying formatting properties, triggers another TextChangedEvent. Remove event handler temporarily.
			this.KeyUp -= KeyUpEventHandler;

            // Add formatting for matching words.
            foreach (Word word in _words)
            {
                TextRange range = new TextRange(word.Start, word.End);
                //range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Blue));
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            }
            _words.Clear();

            // Add TextChanged handler back.
			this.KeyUp += KeyUpEventHandler;
        }

        /// <summary>
        /// Scans passed Run's text, for any matching words from dictionary.
        /// </summary>
        private void AddMatchingWordsInRun(Run run)
        {
            string runText = run.Text;
			runText += " ";
            int wordStartIndex = 0;
            int wordEndIndex = 0;
            for (int i = 0; i < runText.Length; i++)
            {
                if (Char.IsWhiteSpace(runText[i]))
                {
                    if (i > 0 && !Char.IsWhiteSpace(runText[i - 1]))
                    {
                        wordEndIndex = i - 1;
                        string wordInRun = runText.Substring(wordStartIndex, wordEndIndex - wordStartIndex + 1);

                        if (SyntaxProvider.IsKnownTag(wordInRun))
                        {
                            TextPointer wordStart = run.ContentStart.GetPositionAtOffset(wordStartIndex, LogicalDirection.Forward);
                            TextPointer wordEnd = run.ContentStart.GetPositionAtOffset(wordEndIndex + 1, LogicalDirection.Backward);
                            _words.Add(new Word(wordStart, wordEnd));
                        }
                    }
                    wordStartIndex = i + 1;
                }
            }
            
        }

        #endregion
		
        #region Private Types

        /// <summary>
        /// This class encapsulates a matching word by two TextPointer positions, 
        /// start and end, with forward and backward gravities respectively.
        /// </summary>
        private class Word
        {
            public Word(TextPointer wordStart, TextPointer wordEnd)
            {
                _wordStart = wordStart.GetPositionAtOffset(0, LogicalDirection.Forward);
                _wordEnd = wordEnd.GetPositionAtOffset(0, LogicalDirection.Backward);
            }

            public TextPointer Start
            {
                get
                {
                    return _wordStart;
                }
            }

            public TextPointer End
            {
                get
                {
                    return _wordEnd;
                }
            }

            private readonly TextPointer _wordStart;
            private readonly TextPointer _wordEnd;
        }

        #endregion



        // List of matching words found in the document.
        private List<Word> _words;

	}
}