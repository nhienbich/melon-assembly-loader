using System.IO;
using System.Text;
using MelonLoader;

namespace MelonScriptLoader
{
    internal sealed class LogWriter : TextWriter
    {
        private readonly MelonLogger.Instance log;
        private readonly StringBuilder sb = new StringBuilder();

        public LogWriter(MelonLogger.Instance log)
        {
            this.log = log;
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public override void Write(char value)
        {
            if (value == '\n')
            {
                log.Msg(sb.ToString());
                sb.Length = 0;
                return;
            }

            if (value != '\r')
                sb.Append(value);
        }
    }
}
