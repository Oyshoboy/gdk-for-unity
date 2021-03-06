using System;
using Improbable.Gdk.CodeGeneration.Utils;

namespace Improbable.Gdk.CodeGeneration.CodeWriter
{
    /// <summary>
    /// Free-form text to write.
    /// </summary>
    public class Text : ICodeBlock
    {
        private readonly string snippet;

        internal Text(string snippet, bool trim = true)
        {
            this.snippet = trim ? snippet.Trim() : snippet;
        }

        public static Text New(string snippet)
        {
            return new Text(snippet);
        }

        public bool HasValue()
        {
            return !string.IsNullOrEmpty(snippet);
        }

        public string Format(int indentLevel)
        {
            var spaces = CommonGeneratorUtils.GetIndentSpaces(indentLevel);
            var indentedCode = snippet
                .Replace($"{Environment.NewLine}", $"{Environment.NewLine}{spaces}")
                .Replace($"{Environment.NewLine}{spaces}{Environment.NewLine}", $"{Environment.NewLine}{Environment.NewLine}");
            return $"{spaces}{indentedCode}";
        }
    }
}
