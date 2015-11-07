using System;
using System.Globalization;

namespace Austin.GitInCSharpLib
{
    public sealed class PersonTime
    {
        static readonly DateTime sEpoc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal PersonTime(string name, string email, DateTime time)
        {
            this.Name = name;
            this.Email = email;
            this.Time = time;
        }

        public string Name { get; }
        public string Email { get; }
        public DateTime Time { get; }

        public override string ToString()
        {
            return $"{Name} <{Email}> {Time:s}";
        }

        public static PersonTime Parse(string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            int openAngleBracket = str.IndexOf('<');
            if (openAngleBracket == -1)
                throw new ArgumentNullException("Missing angle bracket.");
            int closeAngleBracket = str.IndexOf('>', openAngleBracket + 1);
            if (closeAngleBracket == -1)
                throw new Exception("Missing close angle breacket.");
            if (str[openAngleBracket - 1] != ' ' || str[closeAngleBracket + 1] != ' ')
                throw new Exception("Missing spaces around email.");
            int spaceNdx = str.IndexOf(' ', closeAngleBracket + 2);
            if (spaceNdx == -1)
                throw new Exception("Could not find space between timespace and time zone.");

            string name = str.Substring(0, openAngleBracket - 1);
            string email = str.Substring(openAngleBracket + 1, closeAngleBracket - openAngleBracket - 1);
            string timestampStr = str.Substring(closeAngleBracket + 2, spaceNdx - closeAngleBracket - 2);
            string zoneOffsetStr = str.Substring(spaceNdx + 1);
            DateTime time = sEpoc.AddSeconds(int.Parse(timestampStr, CultureInfo.InvariantCulture));
            //TODO: do something with the time zone offset?
            return new PersonTime(name, email, time);
        }
    }
}
