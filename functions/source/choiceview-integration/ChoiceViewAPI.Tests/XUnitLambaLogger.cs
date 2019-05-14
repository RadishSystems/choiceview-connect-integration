using Amazon.Lambda.Core;
using Xunit.Abstractions;

namespace ChoiceViewAPI.Tests
{
    class XUnitLambaLogger : ILambdaLogger
    {
        private readonly ITestOutputHelper output;

        public XUnitLambaLogger(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Log(string message)
        {
            output.WriteLine(message);
        }

        public void LogLine(string message)
        {
            output.WriteLine(message);
        }
    }
}
