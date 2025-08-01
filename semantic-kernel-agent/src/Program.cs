using System;

namespace SemanticKernelAgent
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Initializing Semantic Kernel Agent...");

            // Here you would typically set up your agent and start it
            var agentService = new Agent.AgentService();
            agentService.StartAgent();

            Console.WriteLine("Agent is running. Press any key to stop...");
            Console.ReadKey();

            agentService.StopAgent();
            Console.WriteLine("Agent has been stopped.");
        }
    }
}