using System;

namespace SemanticKernelAgent.Plugins
{
    public class SamplePlugin : IPlugin
    {
        public string Name => "Sample Plugin";

        public void Execute()
        {
            Console.WriteLine("Executing Sample Plugin functionality.");
            // Add your plugin logic here
        }
    }

    public interface IPlugin
    {
        string Name { get; }
        void Execute();
    }
}