using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sqlSense.AgentService
{
    public static class PromptBuilder
    {
        public static string BuildPrompt(string originalMessage, bool isFastMode)
        {
            if (isFastMode)
            {
                return "You are in fast mode. You must reply immediately without any chain of thought. Never output <think> or <thought> tags.\n\n" + originalMessage;
            }
            return originalMessage;
        }
    }
}
