using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.FillTextToWindows
{
    /// <summary>
    /// 把 <c>ftw</c> 后面的那串文字切分成要依次粘贴的多段文本。
    /// </summary>
    internal static class Arguments
    {
        /// <summary>
        /// 解析查询内容。
        /// <para>
        /// 含 <c>|</c> 时按 <c>|</c> 切分（适合文本里带空格的情况），
        /// 否则按空白切分，并支持用双引号把带空格的内容包起来。
        /// </para>
        /// </summary>
        public static List<string> Parse(string search)
        {
            var values = new List<string>();

            if (string.IsNullOrWhiteSpace(search))
            {
                return values;
            }

            if (search.IndexOf('|') >= 0)
            {
                foreach (var part in search.Split('|'))
                {
                    var trimmed = part.Trim();
                    if (trimmed.Length > 0)
                    {
                        values.Add(trimmed);
                    }
                }

                return values;
            }

            return Tokenize(search);
        }

        /// <summary>
        /// 按空白切词，双引号包起来的内容视为一个整体（引号本身会被去掉）。
        /// </summary>
        private static List<string> Tokenize(string search)
        {
            var values = new List<string>();
            var buffer = new StringBuilder();
            var inQuotes = false;
            var hasContent = false;

            foreach (var c in search)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    hasContent = true;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (hasContent)
                    {
                        values.Add(buffer.ToString());
                        buffer.Clear();
                        hasContent = false;
                    }

                    continue;
                }

                buffer.Append(c);
                hasContent = true;
            }

            if (hasContent)
            {
                values.Add(buffer.ToString());
            }

            return values;
        }
    }
}
