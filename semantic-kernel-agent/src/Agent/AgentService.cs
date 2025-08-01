using System;

namespace SemanticKernelAgent.Agent
{
    public class AgentService
    {
        private bool _isRunning;

        public void StartAgent()
        {
            if (!_isRunning)
            {
                _isRunning = true;
                Console.WriteLine("Agent started.");
                // Additional initialization logic here
            }
        }

        public void StopAgent()
        {
            if (_isRunning)
            {
                _isRunning = false;
                Console.WriteLine("Agent stopped.");
                // Additional cleanup logic here
            }
        }

        public void ExecuteTask(string task)
        {
            if (_isRunning)
            {
                Console.WriteLine($"Executing task: {task}");
                // Task execution logic here
            }
            else
            {
                Console.WriteLine("Agent is not running. Please start the agent first.");
            }
        }
    }
}