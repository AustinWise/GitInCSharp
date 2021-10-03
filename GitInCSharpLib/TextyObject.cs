using System;
using System.Collections.Generic;
using System.Text;

namespace Austin.GitInCSharpLib
{
    /// <summary>
    /// Used as any intermediate step in parsing objects who have a
    /// textual representation, such as commits and tags.
    /// </summary>
    sealed class TextyObject
    {
        readonly static Encoding sEncoding = Encoding.UTF8;

        private TextyObject(List<KeyValuePair<string, string>> attributes, string body)
        {
            if (attributes == null)
                throw new ArgumentNullException(nameof(attributes));

            this.Attributes = attributes;
            this.Body = body;
        }

        public List<KeyValuePair<string, string>> Attributes { get; }
        public string Body { get; }

        public static TextyObject Parse(byte[] objectContents)
        {
            var attrs = new List<KeyValuePair<string, string>>();

            int? spaceNdx = null;
            int previousLineEnd = 0;
            for (int i = 0; i < objectContents.Length; i++)
            {
                if (objectContents[i] == ' ' && !spaceNdx.HasValue)
                    spaceNdx = i;

                if (objectContents[i] == '\n')
                {
                    byte next = objectContents[i + 1];
                    if (next == ' ')
                    {
                        //multi line value
                        continue;
                    }

                    if (!spaceNdx.HasValue)
                        throw new Exception("No space found.");

                    string key = sEncoding.GetString(objectContents, previousLineEnd, spaceNdx.Value - previousLineEnd);
                    string line = sEncoding.GetString(objectContents, spaceNdx.Value + 1, i - spaceNdx.Value - 1);
                    attrs.Add(new KeyValuePair<string, string>(key, line));

                    previousLineEnd = i + 1;
                    spaceNdx = null;

                    if (next == '\n')
                    {
                        //end of headers
                        previousLineEnd++;
                        break;
                    }
                }
            }

            string body = sEncoding.GetString(objectContents, previousLineEnd, objectContents.Length - previousLineEnd);

            return new TextyObject(attrs, body);
        }
    }
}
